using System;
using UnityEngine;
using System.Collections;
using UnityUtils;
using ImprovedTimers;
using State_Machine;

namespace Player_Controller {
    [RequireComponent(typeof(PlayerMover))]
    public class PlayerController : MonoBehaviour {
        #region Fields
        [SerializeField] InputReader input;
         //##CHANGEMENT##
        [SerializeField] TurnTowardController turnToward;
        //##CHANGEMENT_FIN##
        
        Transform tr;
        PlayerMover mover;
        CeilingDetector ceilingDetector;
        
        bool jumpKeyIsPressed;    // Tracks whether the jump key is currently being held down by the player
        bool jumpKeyWasPressed;   // Indicates if the jump key was pressed since the last reset, used to detect jump initiation
        bool jumpKeyWasLetGo;     // Indicates if the jump key was released since it was last pressed, used to detect when to stop jumping
        bool jumpInputIsLocked;   // Prevents jump initiation when true, used to ensure only one jump action per press

        public float movementSpeed = 7f;
        public float airControlRate = 2f;
        public float jumpSpeed = 10f;
        public float jumpDuration = 0.4f;
        public float airFriction = 0.5f;
        public float groundFriction = 100f;
        public float gravity = 30f;
        public float slideGravity = 5f;
        public float slopeLimit = 30f;
        private int maxJumps = 2;
        private int jumpCount = 0; 
        public bool useLocalMomentum;

        //##CHANGEMENT##

        private bool prevJump;
        public int dashCount;
        public float shortJumpDuration = 0.2f;
        private float pressJumpStartTime;
        public bool canDash;
        public float dashSpeed = 15f; 
        private int maxDash = 2;
        public float delayForOneDash = 7f;
        public float debuffDelay = 2f;

        //Consumable
        // float clearAll; //debuff, dash..
        // float clearDebuff;
        // float resetTimerDash; // +dashCount
        
        public float dashDuration = .2f;
        public float maxDashInBetweenDuration = 6f;
  
        CountdownTimer dashTimer;
        CountdownTimer timerMaxDashInRow;
        //##CHANGEMENTFIN##
        StateMachine stateMachine;
        CountdownTimer jumpTimer;
       
        
        [SerializeField] Transform cameraTransform;
        
        Vector3 momentum, savedVelocity, savedMovementVelocity;
        
        public event Action<Vector3> OnJump = delegate { };
        public event Action<Vector3> OnLand = delegate { };
        #endregion
        
        bool IsGrounded() => stateMachine.CurrentState is GroundedState or SlidingState;
        public Vector3 GetVelocity() => savedVelocity;
        public Vector3 GetMomentum() => useLocalMomentum ? tr.localToWorldMatrix * momentum : momentum;
        public Vector3 GetMovementVelocity() => savedMovementVelocity;

        void Awake() {
            tr = transform;
            mover = GetComponent<PlayerMover>();
            ceilingDetector = GetComponent<CeilingDetector>();
            
            jumpTimer = new CountdownTimer(jumpDuration);
            //##CHANGEMENT##
            timerMaxDashInRow = new CountdownTimer(maxDashInBetweenDuration);

            dashTimer = new CountdownTimer(dashDuration);
            //##CHANGEMENT_FIN##

            SetupStateMachine();
        }

        void Start() {
            input.EnablePlayerActions();
            input.Jump += HandleJumpKeyInput;
            input.Dash += HandleDashKeyInput;

            //OnTimerStop += () => ...;
            //PLAY ? : VFX(HUD) + SOUNDS => under fatigue end : timerMaxDashInRow.
        }

        void HandleDashKeyInput(bool isButtonPressed){
            
        }

        void HandleJumpKeyInput(bool isButtonPressed) {

            //##CHANGEMENT##
            if(prevJump && isButtonPressed && dashCount < maxDash && canDash && IsGrounded()) {
                mover.SetExtendSensorRange(false);
                
                //remplacer par state machine : HandleDash(); !!!

                return; // conséquence sur la suite, jumpkey... ?
            }
            //##CHANGEMENT_FIN##

            if (!jumpKeyIsPressed && isButtonPressed) {
                 if (jumpCount < maxJumps) {
                    pressJumpStartTime = Time.time;
                    jumpKeyWasPressed = true;
                    // jumpSpeed = INITIAL_JUMP_SPEED;
                }
            }

            if (jumpKeyIsPressed && !isButtonPressed) {
                jumpKeyWasLetGo = true;
                jumpInputIsLocked = false;
            }
            
            //##CHANGEMENT##
            if (jumpKeyWasLetGo && jumpCount == 1 && TakeTime(pressJumpStartTime) < shortJumpDuration) {
                mover.SetExtendSensorRange(false);
                prevJump = true;
                canDash = true;

                //Time.timeScale = .7f;
                StartCoroutine(TimingToDash());

                //PLAY : HUD + VFX + SOUNDS => SLOW_TIME_SCREEN
            }
            //##CHANGEMENT_FIN##
            
            jumpKeyIsPressed = isButtonPressed;
        }

        void HandleJumping() {

            //INITIAL_JUMP_SPEED

            //##CHANGEMENT##
            if(!mover.IsGrounded() && jumpCount == 2 && TakeTime(pressJumpStartTime) > shortJumpDuration) {
                momentum += tr.up * jumpSpeed;

                //PLAY : CAM + VFX + SOUNDS => CAM_TINY_ZOOM
            }

            if(!mover.IsGrounded() && jumpCount == 2) {
                momentum += tr.up * jumpSpeed/2;

                //PLAY : CAM + VFX + SOUNDS => CAM_TINY_ZOOM
            }
            //##CHANGEMENT_FIN##


            momentum = VectorMath.RemoveDotVector(momentum, tr.up);
            momentum += tr.up * jumpSpeed;
        }

        void ResetJumpKeys() {
            jumpKeyWasLetGo = false;
            jumpKeyWasPressed = false;
        }

        
        public void OnJumpStart() {
            if (jumpCount >= maxJumps) return;
            
            if (useLocalMomentum) momentum = tr.localToWorldMatrix * momentum;

            momentum += tr.up * jumpSpeed;
            jumpTimer.Start();
            jumpCount++;
            jumpInputIsLocked = true;

            Debug.Log(jumpCount + " jumpCount");

            //PLAY : ANIMATIONS * 2 + SOUNDS * 2 => JUMP(1 & 2)_PLAYER + JUMPCOUNT(sounds) 1 & 2 
            OnJump.Invoke(momentum);
            
            if (useLocalMomentum) momentum = tr.worldToLocalMatrix * momentum;
        }

         //##CHANGEMENT##

        public void OnDashStart(){            
            //PLAY ? : VFX(HUD) & SOUNDS => still under fatigue
            if(dashCount == 0) {
                timerMaxDashInRow.Start();
            }
            
            if(dashCount <= maxDash) {
                PlayDash();
            }

            if(dashCount == maxDash && timerMaxDashInRow.IsRunning) {
                //attackSpeed /= 2; //changement possible de posture...
                StartCoroutine(DebuffTimer());
                
            }            
        }
         void PlayDash(){
            Vector3 dashDirection = CalculateMovementDirection();
            
            if (dashDirection == Vector3.zero) {
                dashDirection = turnToward.transform.forward;
            }

            momentum += dashDirection.normalized * dashSpeed + new Vector3(0, 0.25f, 0);;   

            dashTimer.Start();  
            prevJump = false;
            dashCount++;

            StartCoroutine(ResetOneDash());
            //PLAY : ANIMATIONS, VFX, CAM & SOUNDS => DASH_PLAYER : SHAKE_CAM  
            //HUD : DASHCOUNT_CONSTANT
         }

        IEnumerator TimingToDash(){
            Debug.Log("TimingToDash canDash !");
            
            yield return new WaitForSeconds(1.5f);
            canDash = false;

            Debug.Log("TimingToDash : OVER !"); 
        }

        IEnumerator ResetOneDash() {
            yield return new WaitForSeconds(delayForOneDash);
            dashCount--;

            Debug.Log("ResetDash " + dashCount);

            //PLAY : HUD + VFX + SOUNDS => DASHRESET_HUD
        }

        IEnumerator DebuffTimer(){
            //PLAY : ANIMATIONS + VFX + HUD + SOUNDS => TIRED_PLAYER + HUD_TIMER
            Debug.Log("Debuff Fatigue ! " + dashCount);

            movementSpeed /= 2;
            yield return new WaitForSeconds(debuffDelay);
             movementSpeed *= 2;
            //attackSpeed...
           
            //PLAY : ANIMATIONS + VFX + HUD + SOUNDS => RECOVERY_PLAYER + VANISH_HUD_TIMER(sounds)
        }
        //##CHANGEMENT_FIN##
        
        public void OnGroundContactRegained() {
            Vector3 collisionVelocity = useLocalMomentum ? tr.localToWorldMatrix * momentum : momentum;

            //PLAY : ANIMATIONS + VFX + CAM_EFFECT => ONLAND_PLAYER + 
            //TODO : variations suivant hauteur
            OnLand.Invoke(collisionVelocity);

            jumpCount = 0;

            //##CHANGEMENT##
            if(Time.timeScale != 1) {
                Time.timeScale = 1;
            }

            mover.SetExtendSensorRange(true);

            // jumpSpeed = INITIAL_JUMP_SPEED;

            //##CHANGEMENT_FIN##
        }

        void SetupStateMachine() {
            stateMachine = new StateMachine();
            
            var grounded = new GroundedState(this);
            var falling = new FallingState(this);
            var sliding = new SlidingState(this);
            var rising = new RisingState(this);
            var jumping = new JumpingState(this);
            var dashing = new DashingState(this);
            
            At(grounded, rising, () => IsRising());
            At(grounded, sliding, () => mover.IsGrounded() && IsGroundTooSteep());
            At(grounded, falling, () => !mover.IsGrounded());
            At(grounded, jumping, () => (jumpKeyIsPressed || jumpKeyWasPressed) && !jumpInputIsLocked);
            At(grounded, dashing, () => prevJump && (jumpKeyIsPressed || jumpKeyWasPressed) && !jumpInputIsLocked && dashCount < maxDash && canDash && IsGrounded());
            
            At(falling, rising, () => IsRising());
            At(falling, grounded, () => mover.IsGrounded() && !IsGroundTooSteep());
            At(falling, sliding, () => mover.IsGrounded() && IsGroundTooSteep());
            At(falling, jumping, () => (jumpKeyIsPressed || jumpKeyWasPressed) && jumpCount < maxJumps && !jumpInputIsLocked);
            
            At(sliding, rising, () => IsRising());
            At(sliding, falling, () => !mover.IsGrounded());
            At(sliding, grounded, () => mover.IsGrounded() && !IsGroundTooSteep());
            // At(sliding, dashing, () => prevJump && (jumpKeyIsPressed || jumpKeyWasPressed) && !jumpInputIsLocked && dashCount < maxDash && canDash && IsGrounded());
            
            At(rising, grounded, () => mover.IsGrounded() && !IsGroundTooSteep());
            At(rising, sliding, () => mover.IsGrounded() && IsGroundTooSteep());
            At(rising, falling, () => IsFalling());
            At(rising, falling, () => ceilingDetector != null && ceilingDetector.HitCeiling());
            At(rising, jumping, () => jumpInputIsLocked && jumpCount < maxJumps);
            
            At(jumping, rising, () => jumpTimer.IsFinished || jumpKeyWasLetGo);
            At(jumping, falling, () => ceilingDetector != null && ceilingDetector.HitCeiling());
            // At(grounded, jumping, () => (jumpKeyIsPressed || jumpKeyWasPressed) && !jumpInputIsLocked);

            At(dashing, falling, () => dashTimer.IsFinished);
            At(dashing, grounded, () => mover.IsGrounded() && dashTimer.IsFinished);
            
            stateMachine.SetState(falling);
        }
        
        void At(IState from, IState to, Func<bool> condition) => stateMachine.AddTransition(from, to, condition);
        void Any<T>(IState to, Func<bool> condition) => stateMachine.AddAnyTransition(to, condition);
        
        bool IsRising() => VectorMath.GetDotProduct(GetMomentum(), tr.up) > 0f;
        bool IsFalling() => VectorMath.GetDotProduct(GetMomentum(), tr.up) < 0f;
        bool IsGroundTooSteep() => !mover.IsGrounded() || Vector3.Angle(mover.GetGroundNormal(), tr.up) > slopeLimit;
        float TakeTime(float time) => Time.time - time;
        
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
            
            mover.SetExtendSensorRange(IsGrounded());
            mover.SetVelocity(velocity);
            
            savedVelocity = velocity;
            savedMovementVelocity = CalculateMovementVelocity();
            
            ResetJumpKeys();
          
            // Debug.Log(jumpTimer.IsFinished + " " + jumpTimer.CurrentTime);
            if (ceilingDetector != null) ceilingDetector.Reset();
        }
        
        Vector3 CalculateMovementVelocity() { 
            //Debug.Log(savedMovementVelocity + " mov velocity");
            return CalculateMovementDirection() * movementSpeed;
        }

        Vector3 CalculateMovementDirection() {
            Vector3 direction = cameraTransform == null 
                ? tr.right * input.Direction.x + tr.forward * input.Direction.y 
                : Vector3.ProjectOnPlane(cameraTransform.right, tr.up).normalized * input.Direction.x + 
                  Vector3.ProjectOnPlane(cameraTransform.forward, tr.up).normalized * input.Direction.y;
            
            return direction.magnitude > 1f ? direction.normalized : direction;
        }

        void HandleMomentum() {
            if (useLocalMomentum) momentum = tr.localToWorldMatrix * momentum;
            
            Vector3 verticalMomentum = VectorMath.ExtractDotVector(momentum, tr.up);
            Vector3 horizontalMomentum = momentum - verticalMomentum;
            
            //dash gravity
            if (!(stateMachine.CurrentState is DashingState)) {
                verticalMomentum -= tr.up * (gravity * Time.deltaTime);
                if (stateMachine.CurrentState is GroundedState && VectorMath.GetDotProduct(verticalMomentum, tr.up) < 0f) {
                verticalMomentum = Vector3.zero;
                }
            }

            if (!IsGrounded()) {
                AdjustHorizontalMomentum(ref horizontalMomentum, CalculateMovementVelocity());
            }

            if (stateMachine.CurrentState is SlidingState) {
                HandleSliding(ref horizontalMomentum);
            }

            float friction = stateMachine.CurrentState is GroundedState ? groundFriction : airFriction;

            if (stateMachine.CurrentState is DashingState) {
                friction = 0f;
            }
            horizontalMomentum = Vector3.MoveTowards(horizontalMomentum, Vector3.zero, friction * Time.deltaTime);
            
            momentum = horizontalMomentum + verticalMomentum;

            if (stateMachine.CurrentState is JumpingState) {
                HandleJumping();
            }
            
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

        //RISING : ONGROUNDCONTACTLOST ?
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
    }
} 