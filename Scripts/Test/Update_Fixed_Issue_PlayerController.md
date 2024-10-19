using System;
using UnityEngine;
using System.Collections;
using UnityUtils;
using ImprovedTimers;
using State_Machine;

namespace Player_Controller {
    [RequireComponent(typeof(PlayerMover))]
    public class PlayerController : MonoBehaviour {

#region FIELDS_REGION
        [SerializeField] InputReader input;
        [SerializeField] TurnTowardController turnToward;
        [SerializeField] Transform cameraTransform;
        
        Transform tr;
        PlayerMover mover;
        public CeilingDetector ceilingDetector;
        CountdownTimer timerMaxDashInRow;
        StateMachine stateMachine;
        CountdownTimer jumpTimer;

        public float movementSpeed = 7f;
        public float airControlRate = 2f;
        public float airFriction = 0.5f;
        public float groundFriction = 100f;
        public float gravity = 30f;
        public float slideGravity = 5f;
        public float slopeLimit = 30f;
        public bool useLocalMomentum;

        Vector3 momentum, savedVelocity, savedMovementVelocity;

    #region Jump_Dash_Fall
        public float jumpSpeed = 10f;
        private float jumpControlSpeed;
        public float jumpDuration = 0.2f;
        private int maxJumps = 2;
        private int jumpCount;

        public int dashCount;
        public bool canDash;
        public float dashSpeed = 25f;
        private int maxDash = 2;
        public float delayForOneDash = 10f;
        public float maxDashInBetweenDuration = 6f;

        bool jumpKeyIsPressed, dashKeyIsPressed;
        bool jumpKeyWasPressed, isDashing;
        bool jumpKeyWasLetGo;        
        bool jumpInputIsLocked, dashInputIsLocked;

        public float notGrounded;
        public float fallDuration;
        public float fallAcceleration = 2f;
        private bool isFallAdded;
        public float maxFallSpeed = 66f;
        private bool downKeyWasPressed;
        private bool downKeyPressedFrame;
    #endregion
        
    #region InputBuffer
        InputBuffer<PlayerInput> playerInputBuffer;
        public enum PlayerInput
        {
            DownKey,
            // Up,
            // MoveLeft,
            // MoveRight,
            // Jump,
            // Dash,
        } 
        public float playerInputBufferTime = .2f;
        private float playerInputBufferCounter;
        private bool isFastFallBuffered;
    
    #endregion

        public float debuffDelay = 5f;
        private bool isDebuff;
        //Consumable
        // float clearAll; //debuff, dash..
        // float clearDebuff;
        // float resetTimerDash; // +dashCount

    #region UnityEvent
        public event Action<Vector3> OnJump = delegate { };
        public event Action<Vector3> OnLand = delegate { };
    #endregion
       
#endregion
        void Awake() {
            tr = transform;
            mover = GetComponent<PlayerMover>();
            ceilingDetector = GetComponent<CeilingDetector>();
            
            jumpTimer = new CountdownTimer(jumpDuration);
            timerMaxDashInRow = new CountdownTimer(maxDashInBetweenDuration);

            playerInputBuffer = new InputBuffer<PlayerInput>(playerInputBufferTime);

            SetupStateMachine();
        }

        void Start() {
            input.EnablePlayerActions();
            input.Jump += HandleJumpKeyInput;
            input.Dash += HandleDashKeyInput;
            input.DownPressed += HandleDownInputBuffer;

            //OnTimerStop += () => ...;
            //PLAY ? : VFX(HUD) + SOUNDS => under fatigue end : timerMaxDashInRow.
        }

#region HANDLE_INPUT
        void HandleDownInputBuffer(bool downKeyIsPressed) {
            Debug.LogError("1 :" + downKeyIsPressed);
            
            // if(downKeyIsPressed && !downKeyWasPressed){
            //     downKeyWasPressed = true;
            //     downKeyPressedFrame = true;
            // }

            if(downKeyIsPressed && !downKeyWasPressed) {
                playerInputBuffer.AddInput(PlayerInput.DownKey);
                downKeyWasPressed = true;
            }
            if (!downKeyIsPressed && downKeyWasPressed) {
                downKeyWasPressed = false;
                // playerInputBuffer.ConsumeInput(out _);
            }          
        }

        void HandleDashKeyInput(bool isButtonPressed) {
            if (!dashKeyIsPressed && isButtonPressed) {
                if (dashCount < maxDash && IsGrounded()) {
                    isDashing = true;
                }
            }

            if (dashKeyIsPressed && !isButtonPressed) {
                dashInputIsLocked = false;
            }

            dashKeyIsPressed = isButtonPressed;
        }

        void HandleJumpKeyInput(bool isButtonPressed) {
            if (!jumpKeyIsPressed && isButtonPressed) {
                 if (jumpCount < maxJumps) {
                    jumpKeyWasPressed = true;
                }
            }

            if (jumpKeyIsPressed && !isButtonPressed) {
                jumpKeyWasLetGo = true;
                jumpInputIsLocked = false;
            }
            
            jumpKeyIsPressed = isButtonPressed;
        }
#endregion

        void ResetActionsKeys() {
            jumpKeyWasLetGo = false;
            jumpKeyWasPressed = false;

            // downKeyPressedFrame = false;
            // isDashing = false;
        }

#region JUMP_REGION
        public void OnJumpStart() {
            if (jumpCount >= maxJumps) return;
            
            if (useLocalMomentum) momentum = tr.localToWorldMatrix * momentum;

            if (jumpCount == 1 && isDebuff) return;

            jumpCount++;
            momentum += tr.up * jumpControlSpeed;
            jumpInputIsLocked = true;
            jumpTimer.Start();
        
            //PLAY : ANIMATIONS * 2 + SOUNDS * 2 => JUMP(1 & 2)_PLAYER + JUMPCOUNT(sounds) 1 & 2 
            OnJump.Invoke(momentum);
            
            if (useLocalMomentum) momentum = tr.worldToLocalMatrix * momentum;
        }

        void HandleJumping() {
            if (jumpCount == 1 && isDebuff) return;
            
            if (jumpCount == 1) {
                jumpControlSpeed = jumpSpeed; 
            } else {
                jumpControlSpeed = jumpSpeed * .7f;
            }

            momentum = VectorMath.RemoveDotVector(momentum, tr.up);
            momentum += tr.up * jumpControlSpeed;
        }
#endregion

#region DASH_REGION
        public void HandleDash(){  
            if (dashCount >= maxDash || dashInputIsLocked) return;

            if (useLocalMomentum) momentum = tr.localToWorldMatrix * momentum;

            dashInputIsLocked = true;

            if (dashCount == 0) timerMaxDashInRow.Start();
            
            if (dashCount <= maxDash) PlayDash();

            if (dashCount == maxDash && timerMaxDashInRow.IsRunning) StartCoroutine(DebuffTimer());

            if (useLocalMomentum) momentum = tr.worldToLocalMatrix * momentum;
        }
         void PlayDash(){
            Vector3 dashDirection = GetMovementVelocity();
            
            if (dashDirection == Vector3.zero) {
                dashDirection = turnToward.transform.forward;
            }

            momentum += dashDirection.normalized * dashSpeed;
            dashCount++;
            StartCoroutine(ResetOneDash());

            isDashing = false;
            //PLAY : ANIMATIONS, VFX, CAM & SOUNDS => DASH_PLAYER : SHAKE_CAM  
            //HUD : DASHCOUNT_CONSTANT
         }
#endregion

#region IENUMERATOR_REGION
        IEnumerator DebuffTimer(){
            //PLAY : ANIMATIONS + VFX + HUD + SOUNDS => TIRED_PLAYER + HUD_TIMER
            ////attackSpeed /= 2;
            isDebuff = true;
            movementSpeed /= 2;
            
            yield return new WaitForSeconds(debuffDelay);

            if(IsGrounded()){
                isDebuff = false;
                movementSpeed *= 2;
            } else {
                yield return new WaitForSeconds(1f);
                isDebuff = false;
                movementSpeed *= 2;
            }
           
            //PLAY : ANIMATIONS + VFX + HUD + SOUNDS => RECOVERY_PLAYER + VANISH_HUD_TIMER(sounds)
        }
        IEnumerator ResetOneDash() {
            yield return new WaitForSeconds(delayForOneDash);
            dashCount--;

            //PLAY : HUD + VFX + SOUNDS => DASHRESET_HUD
        }
#endregion

        public void OnGroundContactRegained() {
            Vector3 collisionVelocity = useLocalMomentum ? tr.localToWorldMatrix * momentum : momentum;

            //PLAY : ANIMATIONS + VFX + CAM_EFFECT => ONLAND_PLAYER 
            // => TODO : variations suivant hauteur
            OnLand.Invoke(collisionVelocity);

            jumpCount = 0;
            jumpControlSpeed = jumpSpeed;
            isFallAdded = false;
        }

#region STATE_MACHINE
        void SetupStateMachine() {
            stateMachine = new StateMachine();
            
            var grounded = new GroundedState(this);
            var falling = new FallingState(this);
            var sliding = new SlidingState(this);
            var rising = new RisingState(this);
            var jumping = new JumpingState(this);
            
            At(grounded, rising, () => IsRising());
            At(grounded, sliding, () => mover.IsGrounded() && IsGroundTooSteep());
            At(grounded, falling, () => !mover.IsGrounded());
            At(grounded, jumping, () => (jumpKeyIsPressed || jumpKeyWasPressed) && !jumpInputIsLocked);
            
            At(falling, rising, () => IsRising());
            At(falling, grounded, () => mover.IsGrounded() && !IsGroundTooSteep());
            At(falling, sliding, () => mover.IsGrounded() && IsGroundTooSteep());
            At(falling, jumping, () => (jumpKeyIsPressed || jumpKeyWasPressed) && jumpCount < maxJumps && !jumpInputIsLocked);
            
            At(sliding, rising, () => IsRising());
            At(sliding, falling, () => !mover.IsGrounded());
            At(sliding, grounded, () => mover.IsGrounded() && !IsGroundTooSteep());
            
            At(rising, grounded, () => mover.IsGrounded() && !IsGroundTooSteep());
            At(rising, sliding, () => mover.IsGrounded() && IsGroundTooSteep());
            At(rising, falling, () => IsFalling());
            At(rising, falling, () => ceilingDetector != null && (ceilingDetector.HitCeiling() || ceilingDetector.HitCeilingOnStay()));
            At(rising, falling, () => playerInputBuffer.IsRunning() && GetDuration(notGrounded) > .2f);
            // At(rising, falling, () => downKeyPressedFrame && GetDuration(notGrounded) > .2f);

            //TEST RISING, JUMPING
            // At(rising, jumping, () => GetDuration(pressStartJump) > .02f && jumpTimer.IsRunning);
            
            At(jumping, rising, () => jumpTimer.IsFinished || jumpKeyWasLetGo);
            At(jumping, falling, () => ceilingDetector != null && (ceilingDetector.HitCeiling() || ceilingDetector.HitCeilingOnStay()));
            // At(jumping, falling, () => playerInputBuffer.ConsumeInput(out PlayerInput inputAction));
            
            stateMachine.SetState(falling);
        }

        void At(IState from, IState to, Func<bool> condition) => stateMachine.AddTransition(from, to, condition);
        void Any<T>(IState to, Func<bool> condition) => stateMachine.AddAnyTransition(to, condition);
#endregion
        
        Vector3 CalculateMovementVelocity() => CalculateMovementDirection() * movementSpeed;
        
        Vector3 CalculateMovementDirection() {
            Vector3 direction = cameraTransform == null 
                ? tr.right * input.Direction.x + tr.forward * input.Direction.y 
                : Vector3.ProjectOnPlane(cameraTransform.right, tr.up).normalized * input.Direction.x + 
                  Vector3.ProjectOnPlane(cameraTransform.forward, tr.up).normalized * input.Direction.y;
            
            return direction.magnitude > 1f ? direction.normalized : direction;
        }

#region GET_REGION
        public Vector3 GetVelocity() => savedVelocity;
        public Vector3 GetMomentum() => useLocalMomentum ? tr.localToWorldMatrix * momentum : momentum;
        public Vector3 GetMovementVelocity() => savedMovementVelocity;
        float GetDuration(float time) => Time.time - time;

        bool IsGrounded() => stateMachine.CurrentState is GroundedState or SlidingState;
        bool IsRising() => VectorMath.GetDotProduct(GetMomentum(), tr.up) > 0f;
        bool IsFalling() => VectorMath.GetDotProduct(GetMomentum(), tr.up) < 0f;
        bool IsGroundTooSteep() => !mover.IsGrounded() || Vector3.Angle(mover.GetGroundNormal(), tr.up) > slopeLimit;
#endregion

        void Update() {
            stateMachine.Update();
            TimerManager.UpdateTimers();
        }

        void FixedUpdate() {
            stateMachine.FixedUpdate();
            mover.CheckForGround();
            HandleMomentum();
            Vector3 velocity = stateMachine.CurrentState is GroundedState ? CalculateMovementVelocity() : Vector3.zero;
            velocity += useLocalMomentum ? tr.localToWorldMatrix * momentum : momentum;

            if(!isDashing) {
                mover.SetExtendSensorRange(IsGrounded());
            } else {
                mover.SetExtendSensorRange(false);
            }

            mover.SetVelocity(velocity);
            
            savedVelocity = velocity;
            savedMovementVelocity = CalculateMovementVelocity();
            
            ResetActionsKeys();
          
            if (ceilingDetector != null) ceilingDetector.Reset();
        }

#region MOMENTUM
        void HandleMomentum() {
            if (useLocalMomentum) momentum = tr.localToWorldMatrix * momentum;
            
            Vector3 verticalMomentum = VectorMath.ExtractDotVector(momentum, tr.up);
            Vector3 horizontalMomentum = momentum - verticalMomentum;

            if (stateMachine.CurrentState is FallingState) {

                float falling = GetDuration(fallDuration);
                Debug.LogWarning("2) inAir :" + GetDuration(notGrounded) + "Falling Time :" + falling);
                //    Debug.Log("notgrounded time :" + notGrounded);

                //    Debug.Log("falling time :" + falling + " grounded "+ GetDuration(notGrounded));
                
                // Debug.Log("3 : Input IsRunning ? : " + playerInputBuffer.IsRunning());
                // Debug.LogWarning("4) GetDuration(notGrounded) > .2f ? ");
                // Debug.LogWarning(GetDuration(notGrounded) > .2f);
                // Debug.LogWarning("5 :");
                // Debug.LogError(playerInputBuffer.IsRunning() && GetDuration(notGrounded) > .2f);

                // if (playerInputBuffer.ConsumeInput(out PlayerInput inputAction) && !isFallAdded && falling > jumpDuration * 2) {
                //      if (inputAction == PlayerInput.DownKey) {
                //         tr.forward *= .02f;

                //         gravity = Mathf.Clamp(gravity * fallAcceleration, 30, 100);
                //         isFallAdded = true;
                //         Debug.Log("SPEED FALL QUICK !");                   
                //      }
                // }

                Debug.LogError("6 : " + isFallAdded);
                if (playerInputBuffer.IsRunning() && !isFallAdded && falling > jumpDuration * 2) {
                    if(playerInputBuffer.PeekInput(out PlayerInput inputAction) && inputAction == PlayerInput.DownKey){
                        horizontalMomentum *= 0;
                        gravity = Mathf.Clamp(gravity * fallAcceleration, 30, 100);
                        isFallAdded = true;
                        Debug.LogError("SPEED FALL QUICK !");
                        playerInputBuffer.ConsumeInput(out _);  
                    }                        
                }

                // Issue Update # FixedUpdate : ce qu'il peut se passer, étant donné que plusieurs appel à Update peuvent se produire avant le prochain FixedUpdate, c'est une mauvaise synchronisation et moins de précision => update downkeypressed => fixed ok; mais si j'appui quand FixedUpdate est activé en premier.. fixedUpdate étant lance 50 fois sur 60 en premier, et comme il s'agit d'une press une frame...
                //  if (downKeyPressedFrame && !isFallAdded && falling > .1) {
                
                //         // tr.forward *= 0;
                //         horizontalMomentum *= 0;
                //         gravity = Mathf.Clamp(gravity * fallAcceleration, 30, 100);
                //         isFallAdded = true;
                //         Debug.LogError("SPEED FALL QUICK !");
                //         playerInputBuffer.ConsumeInput(out _);  
                //     }                        
                // }

                if (isFallAdded) {
                    gravity = Mathf.Lerp(gravity, 100, Time.deltaTime * fallAcceleration);
                } else {
                    gravity = Mathf.Lerp(30, 100, Time.deltaTime * fallAcceleration * falling);
                }

                verticalMomentum.y = Mathf.Clamp(verticalMomentum.y, maxFallSpeed, -maxFallSpeed);
                     
                // Debug.Log(gravity + " " + verticalMomentum);

            } else {
                gravity = 30f;
                // isFallAdded = false;
            }

            if (!isDashing) {
                verticalMomentum -= tr.up * (gravity * Time.deltaTime);
            }

            if (stateMachine.CurrentState is GroundedState && VectorMath.GetDotProduct(verticalMomentum, tr.up) < 0f) {
                verticalMomentum = Vector3.zero;
            }

            if (!IsGrounded()) {
                AdjustHorizontalMomentum(ref horizontalMomentum, CalculateMovementVelocity());
            }

            if (stateMachine.CurrentState is SlidingState) {
                HandleSliding(ref horizontalMomentum);
            }

            if (!isDashing) {
                float friction = stateMachine.CurrentState is GroundedState ? groundFriction : airFriction;

                 horizontalMomentum = Vector3.MoveTowards(horizontalMomentum, Vector3.zero, friction * Time.deltaTime);
            }
            
            momentum = horizontalMomentum + verticalMomentum;

            if (stateMachine.CurrentState is JumpingState) HandleJumping();

            if (isDashing) HandleDash();
            
            if (stateMachine.CurrentState is SlidingState) {
                momentum = Vector3.ProjectOnPlane(momentum, mover.GetGroundNormal());
                if (VectorMath.GetDotProduct(momentum, tr.up) > 0f) {
                    momentum = VectorMath.RemoveDotVector(momentum, tr.up);
                }
            
                Vector3 slideDirection = Vector3.ProjectOnPlane(-tr.up, mover.GetGroundNormal()).normalized;
                momentum += slideDirection * (slideGravity * Time.deltaTime);
            }
            
            if (useLocalMomentum) momentum = tr.worldToLocalMatrix * momentum;
        }

        public void OnGroundContactLost() {
            if (useLocalMomentum) momentum = tr.localToWorldMatrix * momentum;
            
            Vector3 velocity = GetMovementVelocity();

            if (velocity.sqrMagnitude >= 0f && momentum.sqrMagnitude > 0f) {
                Vector3 projectedMomentum = Vector3.Project(momentum, velocity.normalized);
                float dot = VectorMath.GetDotProduct(projectedMomentum.normalized, velocity.normalized);
                
                if (projectedMomentum.sqrMagnitude >= velocity.sqrMagnitude && dot > 0f) velocity = Vector3.zero;
                else if (dot > 0f) velocity -= projectedMomentum;
            }
            momentum += velocity;
            
            if (useLocalMomentum) momentum = tr.worldToLocalMatrix * momentum;
        }

         public void OnFallStart() {
            var currentUpMomemtum = VectorMath.ExtractDotVector(momentum, tr.up);

            momentum = VectorMath.RemoveDotVector(momentum, tr.up);
            momentum -= tr.up * currentUpMomemtum.magnitude;
        }

        public void OnFallStartStay(){

            var currentUpMomemtum = VectorMath.ExtractDotVector(momentum, tr.up);

            momentum = VectorMath.RemoveDotVector(momentum, tr.up);
            momentum -= tr.up * currentUpMomemtum.magnitude/ceilingDetector.HitCeilingOnStayAngle();
        }

        void AdjustHorizontalMomentum(ref Vector3 horizontalMomentum, Vector3 movementVelocity) {
            if (horizontalMomentum.magnitude > movementSpeed) {
                if (VectorMath.GetDotProduct(movementVelocity, horizontalMomentum.normalized) > 0f) {
                    movementVelocity = VectorMath.RemoveDotVector(movementVelocity, horizontalMomentum.normalized);
                }
                horizontalMomentum += movementVelocity * (Time.deltaTime * airControlRate * 0.25f);
            }
            else {
                horizontalMomentum += movementVelocity * (Time.deltaTime * airControlRate);
                horizontalMomentum = Vector3.ClampMagnitude(horizontalMomentum, movementSpeed);
            }
        }

        void HandleSliding(ref Vector3 horizontalMomentum) {
            Vector3 pointDownVector = Vector3.ProjectOnPlane(mover.GetGroundNormal(), tr.up).normalized;
            Vector3 movementVelocity = CalculateMovementVelocity();
            movementVelocity = VectorMath.RemoveDotVector(movementVelocity, pointDownVector);
            horizontalMomentum += movementVelocity * Time.fixedDeltaTime;
        }
#endregion

    }