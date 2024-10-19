using System;
using UnityEngine;
using System.Collections;
using UnityUtils;
using ImprovedTimers;
using State_Machine;
using System.Collections.Generic;
using System.Linq;

namespace Player_Controller {
    [RequireComponent(typeof(PlayerMover))]
    public class PlayerController : MonoBehaviour {

#region FIELDS_REGION
        [Header("General Settings")]
        [SerializeField] InputReader input;
        [SerializeField] Transform cameraTransform;
        [SerializeField] TurnTowardController turnToward;
        
        Transform tr;
        PlayerMover mover;
        StateMachine stateMachine;
        public CeilingDetector ceilingDetector;
        InputBuffer<PlayerInput> playerInputBuffer;

        CountdownTimer jumpTimer;
        CountdownTimer dashTimer;
        CountdownTimer timerMaxDashInRow;    
        // CountdownTimer bufferInputRefresh;

        public event Action<Vector3> OnJump = delegate { };
        public event Action<Vector3> OnLand = delegate { };

        [Header("General Momentum Set")]
        public bool useLocalMomentum;
        Vector3 momentum, savedVelocity, savedMovementVelocity;

        public float movementSpeed = 7f;
        public float jumpSpeed = 10f;
        public float dashSpeed = 35f;
        public float maxFallSpeed = 77f;
        public float fallAcceleration = 2f;
        public float airControlRate = 2f;
        public float airFriction = 0.5f;
        public float groundFriction = 100f;
        public float gravity = 30f;
        public float slideGravity = 5f;

        [Header("Slide Set")]
        public float slopeLimit = 30f;

        [Header("Jump Set")]
        public float jumpDuration = 0.2f;
        public float jumpCountdown;
        private int maxJumps = 2;
        private int jumpCount;
        private float jumpControlSpeed;

    #region CENTER_FIELD
        private bool jumpKeyIsPressed, dashKeyIsPressed;
        private bool jumpKeyWasPressed;
        private bool jumpKeyWasLetGo;        
        private bool jumpInputIsLocked, dashInputIsLocked;
        private const string DASH = "DASH";
        private const string SMASHGROUND = "SMASHGROUND";
        public float fallDuration;
    #endregion

        [Header("Dash Set")]
    #region DASH_FIELD
        public float dashDuration = .1f;
        public float delayForResetDash = 10f;
        private int dashCount;
        private int maxDash = 2;
        public float maxDashInBetweenDuration = 6f;
        public float debuffDelay = 5f;
        private bool isDebuff;
        //Consumable
        // float clearAll; //debuff, dash..
        // float clearDebuff;
        // float resetTimerDash; // +dashCount
    #endregion
        
        [Header("SmashGround Set")]
        public float delayForSmashGroundAgain = 1f;
        public bool lockRotation;
        private bool canSmashGroundAgain = true;
        private bool isSmashingGround;
        private bool downKeyWasPressed;
        
        [Header("InputBuffer Set")]
        public float setPlayerInputBufferTime = .1f;
        public enum PlayerInput
        {
            DownKey,
            // Up,
            // MoveLeft,
            // MoveRight,
            Jump,
            // Dash,
        }
        private List<PlayerInput> requiredInputs = new List<PlayerInput> { PlayerInput.DownKey, PlayerInput.Jump };
#endregion

#region AWAKE_START
        void Awake() {
            tr = transform;
            jumpControlSpeed = jumpSpeed;

            mover = GetComponent<PlayerMover>();
            ceilingDetector = GetComponent<CeilingDetector>();

            playerInputBuffer = new InputBuffer<PlayerInput>(setPlayerInputBufferTime);
            // bufferInputRefresh = new CountdownTimer(setPlayerInputBufferTime * 1.1f);
            
            jumpTimer = new CountdownTimer(jumpDuration);
            dashTimer = new CountdownTimer(dashDuration);
            timerMaxDashInRow = new CountdownTimer(maxDashInBetweenDuration);
            
            SetupStateMachine();
        }

        void Start() {
            input.EnablePlayerActions();
            input.Jump += HandleJumpKeyInput;
            input.Dash += HandleDashKeyInput;
            input.KeyDownPressed += HandleDownInputBuffer;

            // bufferInputRefresh.OnTimerStop += () => StartCoroutine(BufferInputCleaner());
            dashTimer.OnTimerStop += () => {
                dashInputIsLocked = false;

                momentum /= 2f;

                // if(IsGrounded()){
                //     momentum /= 1.5f;
                // } else {
                    
                // }
            };

            StartCoroutine(BufferInputCleaner());
        }
#endregion

#region HANDLE_INPUT
        void HandleDownInputBuffer(bool downKeyIsPressed) {
            if(downKeyIsPressed && !downKeyWasPressed) {
                playerInputBuffer.AddInput(PlayerInput.DownKey);
                // if(!bufferInputRefresh.IsRunning){
                //     bufferInputRefresh.Start();
                //     StartCoroutine(BufferInputCleaner());
                // }

                // Debug.LogWarning("add DownKey ? " + playerInputBuffer.BufferQueue.Any(b => b.Input == PlayerInput.DownKey)+ " Count : " + playerInputBuffer.BufferQueue.Count);

                downKeyWasPressed = true;
            }
            if (!downKeyIsPressed && downKeyWasPressed) {
                downKeyWasPressed = false;
            }          
        }

        void HandleDashKeyInput(bool isButtonPressed) {
            if (!dashKeyIsPressed && isButtonPressed) {
                dashKeyIsPressed = true;
                // if (dashCount < maxDash && IsGrounded()) {
                //     isDashing = true;
                // }
            }

            if (dashKeyIsPressed && !isButtonPressed) {
                dashKeyIsPressed = false;
            }

            // dashKeyIsPressed = isButtonPressed;
        }

        void HandleJumpKeyInput(bool isButtonPressed) {
            if (!jumpKeyIsPressed && isButtonPressed) {
                playerInputBuffer.AddInput(PlayerInput.Jump);
                // if (!bufferInputRefresh.IsRunning){
                //     bufferInputRefresh.Start();
                //     StartCoroutine(BufferInputCleaner());
                // }

                // Debug.LogWarning("add Jump ? " + playerInputBuffer.BufferQueue.Any(b => b.Input == PlayerInput.Jump) + "Count : " + playerInputBuffer.BufferQueue.Count);

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

        void ResetActionsKeys() {
            jumpKeyWasLetGo = false;
            jumpKeyWasPressed = false;
        }
#endregion

#region JUMP
        public void OnJumpStart() {
            if (jumpCount >= maxJumps || isDebuff) return;

            // if (jumpCount == 1 && isDebuff) return;
            
            if (useLocalMomentum) momentum = tr.localToWorldMatrix * momentum;

            jumpCount++;
            momentum += tr.up * jumpControlSpeed;
            // tr.up *= jumpControlSpeed; 
            jumpInputIsLocked = true;
            jumpTimer.Start();
        
            //PLAY : ANIMATIONS * 2 + SOUNDS * 2 => JUMP(1 & 2)_PLAYER + JUMPCOUNT(sounds) 1 & 2 
            OnJump.Invoke(momentum);
            
            if (useLocalMomentum) momentum = tr.worldToLocalMatrix * momentum;
        }

        void HandleJumping() {
            if (isDebuff) return;
            // if (jumpCount == 1 && isDebuff) return;
            
            if (jumpCount == 1) {
                jumpControlSpeed = jumpSpeed; 
            } else {
                jumpControlSpeed = jumpSpeed * .7f;
            }

            momentum = VectorMath.RemoveDotVector(momentum, tr.up);
            momentum += tr.up * jumpControlSpeed;
        }
#endregion

#region FALL
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
#endregion

#region DASH
        public bool CanDash(){  
            if (dashCount >= maxDash || dashInputIsLocked && !isDebuff) return false;
            // || canDashAgain + dashCount ; DashTimer dashDuration

            dashInputIsLocked = true;

            if (dashCount == 0) timerMaxDashInRow.Start();
            
// HERE !!
// add timer canDash = isDashing = 0 application gravité : add horizontalmomentum save le verticalmomentum

            //  ??
            //if (dashCount <= maxDash) {
            // }
            return true;
        }
         public void HandleDash(){
            if (useLocalMomentum) momentum = tr.localToWorldMatrix * momentum;

            dashTimer.Start();
            // isDashing = true;
            dashCount ++;

            if (dashCount == maxDash && timerMaxDashInRow.IsRunning) StartCoroutine(DebuffTimer());

            // Vector3 dashDirection = GetMovementVelocity();
            // if(dashDirection == Vector3.zero) dashDirection = tr.forward;
        
            momentum.y *= 0;

            if(IsGrounded()) {
                momentum = turnToward.tr.forward * dashSpeed * 1.2f;
            } else {
                // momentum.y *= 0 + jumpSpeed/2;
                momentum = turnToward.tr.forward * dashSpeed + tr.up * jumpControlSpeed/2;
            }
            
            Debug.LogError($"momentum Dash : {momentum}");
            //momentum -= tr.up * currentUpMomemtum.magnitude;
            
            // if (dashDirection == Vector3.zero) {
            //     dashDirection = turnToward.transform.forward;
            // }

            // ??
            // dashCount++;
            StartCoroutine(ResetAbility(delayForResetDash, DASH));

            // isDashing = false;
            //PLAY : ANIMATIONS, VFX, CAM & SOUNDS => DASH_PLAYER : SHAKE_CAM  
            //HUD : DASHCOUNT_CONSTANT

            if (useLocalMomentum) momentum = tr.worldToLocalMatrix * momentum;
         }
#endregion

#region SMASH_GROUND
        bool CanSmashGround() {
            // if (playerInputBuffer.BufferQueue.Count < requiredInputs.Count) return false;

            if(!canSmashGroundAgain || IsGroundPreviousState()) return false;

            // Debug.Log($"{jumpCountdown} & {Time.time} : {GetDuration(jumpCountdown)} < jumpDuration ? {GetDuration(jumpCountdown) < jumpDuration/3}");

            // if(GetDuration(jumpCountdown) < jumpDuration/3) return false;
            // if (GetDuration(jumpCountdown) < jumpDuration) {
            //     Debug.Log("Cannot SmashGround yet: Duration is less than threshold.");
            //     return false;
            // }
            // Debug.LogWarning($"Can Smash ? {canSmashGroundAgain}");

            // mover.SetExtendSensorRange(true);
                
            bool hasDownKey = playerInputBuffer.BufferQueue.Any(b => b.Input == PlayerInput.DownKey);
            bool hasJump = playerInputBuffer.BufferQueue.Any(b => b.Input == PlayerInput.Jump);
            
            return hasDownKey && hasJump;
        }

        public void HandleSmashGround() {
            // var currentUpMomemtum = VectorMath.ExtractDotVector(momentum, tr.up);
            momentum.x *= 0;
            // momentum -= tr.up * currentUpMomemtum.magnitude;
            momentum -= tr.up * gravity/3;
            Debug.LogError($"SMASHING GROUND {momentum}");
            
            gravity = Mathf.Clamp(gravity * fallAcceleration, 30, 100);    

            lockRotation = true;
            canSmashGroundAgain = false;
            isSmashingGround = true;

            StartCoroutine(ResetAbility(delayForSmashGroundAgain, SMASHGROUND));
        }
#endregion     

#region IENUMERATOR
        IEnumerator BufferInputCleaner() {
            while (true) {
                // bufferInputRefresh.Start(); 
                yield return new WaitForSeconds(setPlayerInputBufferTime * 1.05f);
                playerInputBuffer.InputCleaner();
            }
        } 

        IEnumerator DebuffTimer() {
            //PLAY : ANIMATIONS + VFX + HUD + SOUNDS => TIRED_PLAYER + HUD_TIMER
            //attackSpeed /= 2;
            isDebuff = true;
            movementSpeed /= 2f;
            
            yield return new WaitForSeconds(debuffDelay);

            if(IsGrounded()) {
                isDebuff = false;
                movementSpeed *= 2f;
            } else {
                yield return new WaitForSeconds(1f);
                isDebuff = false;
                movementSpeed *= 2f;
            }         
            //PLAY : ANIMATIONS + VFX + HUD + SOUNDS => RECOVERY_PLAYER + VANISH_HUD_TIMER(sounds)
        }
        IEnumerator ResetAbility(float resetDelay, string ability) {

            if(ability ==  "SMASHGROUND"){
                Debug.LogError("SMASHGROUND IMPOSSIBLE");
            }

            yield return new WaitForSeconds(resetDelay);
            
            switch(ability) {
                case "DASH":
                    dashCount--;
                    break;
                case "SMASHGROUND":
                    canSmashGroundAgain = true;
                    break;
            }
            //PLAY : HUD + VFX + SOUNDS => DASHRESET_HUD
        }
#endregion

#region STATEMACHINE
        void SetupStateMachine() {
            stateMachine = new StateMachine();
            
            var grounded = new GroundedState(this);
            var falling = new FallingState(this);
            var sliding = new SlidingState(this);
            var rising = new RisingState(this);
            var jumping = new JumpingState(this);
            var dashing = new DashingState(this);
            var SmashingGround = new SmashingGroundState(this);
            
            At(grounded, rising, () => IsRising());
            At(grounded, sliding, () => mover.IsGrounded() && IsGroundTooSteep());
            At(grounded, falling, () => !mover.IsGrounded());
            At(grounded, jumping, () => (jumpKeyIsPressed || jumpKeyWasPressed) && !jumpInputIsLocked && !IsDashPreviousState());
            At(grounded, dashing, () => dashKeyIsPressed && CanDash());
            
            At(falling, rising, () => IsRising());
            At(falling, grounded, () => mover.IsGrounded() && !IsGroundTooSteep());
            At(falling, sliding, () => mover.IsGrounded() && IsGroundTooSteep());
            // - !CanSmashGround()
            At(falling, jumping, () => (jumpKeyIsPressed || jumpKeyWasPressed) && jumpCount < maxJumps && !jumpInputIsLocked && !CanSmashGround() && !IsDashPreviousState());
            At(falling, SmashingGround, () => CanSmashGround() && !mover.IsGrounded() && !IsJumpPreviousState());
            At(falling, dashing, () => dashKeyIsPressed && CanDash());
            
            At(sliding, rising, () => IsRising());
            At(sliding, falling, () => !mover.IsGrounded());
            At(sliding, grounded, () => mover.IsGrounded() && !IsGroundTooSteep());
            
            At(rising, grounded, () => mover.IsGrounded() && !IsGroundTooSteep());
            At(rising, sliding, () => mover.IsGrounded() && IsGroundTooSteep());
            At(rising, falling, () => IsFalling());
            At(rising, falling, () => ceilingDetector != null && (ceilingDetector.HitCeiling() || ceilingDetector.HitCeilingOnStay()));
            At(rising, falling, () => CanSmashGround());
            At(rising, dashing, () => dashKeyIsPressed && CanDash());
            
            At(jumping, rising, () => jumpTimer.IsFinished || jumpKeyWasLetGo);
            At(jumping, falling, () => ceilingDetector != null && (ceilingDetector.HitCeiling() || ceilingDetector.HitCeilingOnStay()));
            At(jumping, falling, () => CanSmashGround());
            At(jumping, dashing, () => dashKeyIsPressed && CanDash());
            // At(jumping, SmashingGround, () => !IsJumpPreviousState());

            At(SmashingGround, grounded, () => mover.IsGrounded() && !IsGroundTooSteep());

            //J'ai fais une Yannis, j'ai set tous les autres sauf moi : on reste bloqué au dash state :x
            At(dashing, grounded, () => mover.IsGrounded() && !IsGroundTooSteep());
            At(dashing, falling, () => dashTimer.IsFinished);
            
            stateMachine.SetState(falling);
        }

        void At(IState from, IState to, Func<bool> condition) => stateMachine.AddTransition(from, to, condition);
        void Any<T>(IState to, Func<bool> condition) => stateMachine.AddAnyTransition(to, condition);
#endregion
        
#region GET_REGION
        Vector3 CalculateMovementVelocity() => CalculateMovementDirection() * movementSpeed;
        Vector3 CalculateMovementDirection() {
            Vector3 direction = cameraTransform == null 
                ? tr.right * input.Direction.x + tr.forward * input.Direction.y 
                : Vector3.ProjectOnPlane(cameraTransform.right, tr.up).normalized * input.Direction.x + 
                  Vector3.ProjectOnPlane(cameraTransform.forward, tr.up).normalized * input.Direction.y;
            
            return direction.magnitude > 1f ? direction.normalized : direction;
        }

        public Vector3 GetVelocity() => savedVelocity;
        public Vector3 GetMomentum() => useLocalMomentum ? tr.localToWorldMatrix * momentum : momentum;
        public Vector3 GetMovementVelocity() => savedMovementVelocity;
        float GetDuration(float time) => Time.time - time;

        bool IsGrounded() => stateMachine.CurrentState is GroundedState or SlidingState;
        bool IsRising() => VectorMath.GetDotProduct(GetMomentum(), tr.up) > 0f;
        bool IsFalling() => VectorMath.GetDotProduct(GetMomentum(), tr.up) < 0f;
        bool IsGroundTooSteep() => !mover.IsGrounded() || Vector3.Angle(mover.GetGroundNormal(), tr.up) > slopeLimit;
        bool IsDashPreviousState() {
            return stateMachine.previousState is DashingState && !IsGrounded();
        }
        bool IsJumpPreviousState() { 
            return stateMachine.previousState is JumpingState;
        }
        bool IsGroundPreviousState() { 
            return stateMachine.previousState is GroundedState;
        }
        /*
        bool IsCanJumpLastInput() {
            return playerInputBuffer.BufferQueue.Count == requiredInputs.Count && playerInputBuffer.ReturnLastInput() == PlayerInput.DownKey || playerInputBuffer.ReturnLastInput() == PlayerInput.Jump ? true : false;

            // return playerInputBuffer.ReturnLastInput() == PlayerInput.DownKey;
            // && playerInputBuffer.ReturnFirstInput() == PlayerInput.Jump;
        }
        */
#endregion

#region UPDATE_FIXED
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

            // if (stateMachine.CurrentState is DashingState) {
            //     mover.SetExtendSensorRange(false);
            // } else {
                mover.SetExtendSensorRange(IsGrounded());
            // }

            mover.SetVelocity(velocity);
            
            savedVelocity = velocity;
            savedMovementVelocity = CalculateMovementVelocity();
            
            ResetActionsKeys();
          
            if (ceilingDetector != null) ceilingDetector.Reset();
        }
#endregion

#region MOMENTUM
        void HandleMomentum() {
            if (useLocalMomentum) momentum = tr.localToWorldMatrix * momentum;
            
            Vector3 verticalMomentum = VectorMath.ExtractDotVector(momentum, tr.up);
            Vector3 horizontalMomentum = momentum - verticalMomentum;
           
            if (stateMachine.CurrentState is FallingState || stateMachine.CurrentState is SmashingGroundState) {
                float falling = GetDuration(fallDuration);
  
                if (isSmashingGround) {
                    horizontalMomentum *= 0;
                    gravity = Mathf.Lerp(gravity, 100, Time.deltaTime * fallAcceleration);
                } else {
                    gravity = Mathf.Lerp(30, 100, Time.deltaTime * fallAcceleration * falling);
                }

                verticalMomentum.y = Mathf.Clamp(verticalMomentum.y, -maxFallSpeed, maxFallSpeed);
                // Debug.Log(gravity + " " + verticalMomentum);
            } else {
                gravity = 30f;
            }                              

            if (stateMachine.CurrentState is DashingState) {
                verticalMomentum *= 0;
            } else {
                verticalMomentum -= tr.up * (gravity * Time.deltaTime);
            }

            if (stateMachine.CurrentState is GroundedState && VectorMath.GetDotProduct(verticalMomentum, tr.up) < 0f) {
                verticalMomentum = Vector3.zero;
            }

            //tout confondu avec les et les ou, ca fait quoi ca : ?
            if (!IsGrounded() && !isSmashingGround) {
                AdjustHorizontalMomentum(ref horizontalMomentum, CalculateMovementVelocity());
            }

            if (stateMachine.CurrentState is SlidingState) {
                HandleSliding(ref horizontalMomentum);
            }

            float friction = stateMachine.CurrentState is GroundedState ? groundFriction : airFriction;

            if (stateMachine.CurrentState is DashingState && mover.IsGrounded()) {
                friction = 0;
            } else if (stateMachine.CurrentState is DashingState && !mover.IsGrounded()) {
                friction = airFriction * 2;
            }

            // if (IsDashPreviousState()) {
            //     friction *= 2;
            // }

            horizontalMomentum = Vector3.MoveTowards(horizontalMomentum, Vector3.zero, friction * Time.deltaTime);
            
            momentum = horizontalMomentum + verticalMomentum;

            if (stateMachine.CurrentState is JumpingState) HandleJumping();
            
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

        void HandleSliding(ref Vector3 horizontalMomentum) {
            Vector3 pointDownVector = Vector3.ProjectOnPlane(mover.GetGroundNormal(), tr.up).normalized;

            Vector3 movementVelocity = CalculateMovementVelocity();

            movementVelocity = VectorMath.RemoveDotVector(movementVelocity, pointDownVector);
            horizontalMomentum += movementVelocity * Time.fixedDeltaTime;
        }

        void AdjustHorizontalMomentum(ref Vector3 horizontalMomentum, Vector3 movementVelocity) {
            if (horizontalMomentum.magnitude > movementSpeed) {
                if (VectorMath.GetDotProduct(movementVelocity, horizontalMomentum.normalized) > 0f) {
                    movementVelocity = VectorMath.RemoveDotVector(movementVelocity, horizontalMomentum.normalized);
                }
                horizontalMomentum += movementVelocity * (Time.deltaTime * airControlRate * 0.15f);
            }
            else {
                horizontalMomentum += movementVelocity * (Time.deltaTime * airControlRate);
                horizontalMomentum = Vector3.ClampMagnitude(horizontalMomentum, movementSpeed);
            }
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

         public void OnGroundContactRegained() {
            Vector3 collisionVelocity = useLocalMomentum ? tr.localToWorldMatrix * momentum : momentum;

            //PLAY : ANIMATIONS + VFX + CAM_EFFECT => ONLAND_PLAYER 
            // => TODO : variations suivant hauteur
            OnLand.Invoke(collisionVelocity);

            jumpCount = 0;
            jumpCountdown = 0;
            jumpControlSpeed = jumpSpeed;

            lockRotation = false;
            isSmashingGround = false;
        }
#endregion
    }
} 