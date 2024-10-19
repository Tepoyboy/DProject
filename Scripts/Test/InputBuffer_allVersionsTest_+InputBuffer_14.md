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
        [SerializeField] InputReader input;
        [SerializeField] TurnTowardController turnToward;
        [SerializeField] Transform cameraTransform;
        
        Transform tr;
        PlayerMover mover;
        public CeilingDetector ceilingDetector;
        StateMachine stateMachine;
        CountdownTimer jumpTimer;
        CountdownTimer timerMaxDashInRow;
        CountdownTimer bufferInputRefresh;
       

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
        public bool isDashing;
        public float dashSpeed = 35f;
        private int maxDash = 2;
        public float delayForOneDash = 10f;
        public float maxDashInBetweenDuration = 6f;

        bool jumpKeyIsPressed, dashKeyIsPressed;
        bool jumpKeyWasPressed;
        bool jumpKeyWasLetGo;        
        bool jumpInputIsLocked, dashInputIsLocked;

        public float maxFallSpeed = 77f;
        public float fallAcceleration = 2f;
        public float maxTimerBeforeCanFastFall = .9f;
        private bool downKeyWasPressed;
        public float startAirTime;
        public float fallDuration;
        // private bool isFastFallAdded;
        
    #endregion
        
    #region InputBuffer
        InputBuffer<PlayerInput> playerInputBuffer;
        public enum PlayerInput
        {
            DownKey,
            // Up,
            // MoveLeft,
            // MoveRight,
            Jump,
            // Dash,
        } 
        public float playerInputBufferTime = .2f;
        List<PlayerInput> bufferInput = new List<PlayerInput>();
        List<PlayerInput> requiredInputs = new List<PlayerInput> { PlayerInput.DownKey, PlayerInput.Jump };
        private bool isAirSmashing;
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

            playerInputBuffer = new InputBuffer<PlayerInput>(playerInputBufferTime);
            // bufferInputRefresh = new CountdownTimer(playerInputBufferTime * 2);
            
            jumpTimer = new CountdownTimer(jumpDuration);
            timerMaxDashInRow = new CountdownTimer(maxDashInBetweenDuration);
            
            SetupStateMachine();
        }

        void Start() {
            input.EnablePlayerActions();
            input.Jump += HandleJumpKeyInput;
            input.Dash += HandleDashKeyInput;
            input.KeyDownPressed += HandleDownInputBuffer;

            // bufferInputRefresh.OnTimerStop += () => {
            //     if(playerInputBuffer.bufferList.Count > 0) {
            //         playerInputBuffer.InputCleaner();
            //     }
            // };
            
            StartCoroutine(BufferInputCleaner());
            //OnTimerStop += () => ...;
            //PLAY ? : VFX(HUD) + SOUNDS => under fatigue end : timerMaxDashInRow.
        }

#region HANDLE_INPUT
        void HandleDownInputBuffer(bool downKeyIsPressed) {
            if(downKeyIsPressed && !downKeyWasPressed) {
                playerInputBuffer.AddInput(PlayerInput.DownKey);

                Debug.LogWarning("add DownKey ? " + playerInputBuffer.bufferList.Any(b => b.Input == PlayerInput.DownKey)+ " Count : " + playerInputBuffer.bufferList.Count);

                downKeyWasPressed = true;
            }
            if (!downKeyIsPressed && downKeyWasPressed) {
                downKeyWasPressed = false;
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
                playerInputBuffer.AddInput(PlayerInput.Jump);

                Debug.LogWarning("add Jump ? " + playerInputBuffer.bufferList.Any(b => b.Input == PlayerInput.Jump) + "Count : " + playerInputBuffer.bufferList.Count);

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
        }

#region JUMP_FALL
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

#region IENUMERATOR

        IEnumerator BufferInputCleaner() {
            while (true)
            {
                // bufferInputRefresh.Start(); 
                yield return new WaitForSeconds(playerInputBufferTime * 2);
                // IReadOnlyCollection<InputBuffer<PlayerInput>.BufferedInput> bufferQueue = playerInputBuffer.BufferQueue;
                if (playerInputBuffer.BufferQueue.Count > 0) {
                    playerInputBuffer.InputCleaner();
                }
                // bufferInput.Clear();
            }
        }

        IEnumerator DebuffTimer(){
            //PLAY : ANIMATIONS + VFX + HUD + SOUNDS => TIRED_PLAYER + HUD_TIMER
            //attackSpeed /= 2;
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

            isFastFallAdded = false;
            jumpCount = 0;
            jumpControlSpeed = jumpSpeed;
        }

#region STATEMACHINE
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
            At(falling, jumping, () => (jumpKeyIsPressed || jumpKeyWasPressed) && jumpCount < maxJumps && !jumpInputIsLocked && !IsDownKeyLastInput());
            
            At(sliding, rising, () => IsRising());
            At(sliding, falling, () => !mover.IsGrounded());
            At(sliding, grounded, () => mover.IsGrounded() && !IsGroundTooSteep());
            
            At(rising, grounded, () => mover.IsGrounded() && !IsGroundTooSteep());
            At(rising, sliding, () => mover.IsGrounded() && IsGroundTooSteep());
            At(rising, falling, () => IsFalling());
            At(rising, falling, () => ceilingDetector != null && (ceilingDetector.HitCeiling() || ceilingDetector.HitCeilingOnStay()));
            // At(rising, falling, () => playerInputBuffer.AreRunning() && GetDuration(startAirTime) > maxTimerBeforeCanFastFall);
            
            At(jumping, rising, () => jumpTimer.IsFinished || jumpKeyWasLetGo);
            At(jumping, falling, () => ceilingDetector != null && (ceilingDetector.HitCeiling() || ceilingDetector.HitCeilingOnStay()));
            
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
        bool IsDownKeyLastInput() {
            return playerInputBuffer.ReturnLastInput() == PlayerInput.DownKey;
        }

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
#endregion

/*
void HandleAirSmash() {
        Debug.LogWarning("HANDLE SMASH ENTER");

        int bufferInputCount = playerInputBuffer.bufferList.Count;

        Debug.LogWarning("bufferInputCount : " + bufferInputCount);

        for(int i = 0; i < playerInputBuffer.bufferList.Count; i++) {
            playerInputBuffer.PeekInput(out PlayerInput inputAction, i);
            // Debug.Log(inputAction);
            bufferInput.Add(inputAction);
        }

        Debug.Log("Contenu de la liste copié :");
        foreach (var input in bufferInput)
        {
            Debug.Log(input);
        }
        Debug.Log("Contenu de la liste depuis BUfferInput:");
        foreach (var input in playerInputBuffer.bufferList)
        {
            Debug.Log(input);
        }

        if(requiredInputs.All(bufferInput.Contains)) {
            gravity = Mathf.Clamp(gravity * fallAcceleration, 30, 100);
            isFastFallAdded = true;
            Debug.LogError("SPEED FALL QUICK !");
            isAirSmashing = true;

            bufferInput.Clear();

            for(int i = 0; i < 2 ; i++) {
                playerInputBuffer.ConsumeInput(out _);
            }

        } else {
            isAirSmashing = false;
        }
    } else {
        return;
    }
} */

bool CheckAirSmash() {
    if (playerInputBuffer.BufferQueue.Count < requiredInputs.Count) return false;
    
    bool hasDownKey = playerInputBuffer.BufferQueue.Any(b => b.Input == PlayerInput.DownKey);
    bool hasJump = playerInputBuffer.BufferQueue.Any(b => b.Input == PlayerInput.Jump);
    
    return hasDownKey && hasJump;
}

#region MOMENTUM
        void HandleMomentum() {
            if (useLocalMomentum) momentum = tr.localToWorldMatrix * momentum;
            
            Vector3 verticalMomentum = VectorMath.ExtractDotVector(momentum, tr.up);
            Vector3 horizontalMomentum = momentum - verticalMomentum;

            // if (!IsGrounded() && playerInputBuffer.bufferList.Any(b => b.Input == PlayerInput.DownKey) && playerInputBuffer.bufferList.Any(b => b.Input == PlayerInput.Jump)) {
            if(!IsGrounded() && CheckAirSmash() && !isAirSmashing){
                // HandleAirSmash();
                Debug.LogError("ENTER HANDLE DASH ! SPEED FALL !");
                gravity = Mathf.Clamp(gravity * fallAcceleration, 30, 100);
                // isFastFallAdded = true;
                Debug.LogError("SPEED FALL QUICK !");
                isAirSmashing = true;

                horizontalMomentum *= 0;
            }   

            if (stateMachine.CurrentState is FallingState) {
                float falling = GetDuration(fallDuration);

                if (isAirSmashing) {
                    gravity = Mathf.Lerp(gravity, 100, Time.deltaTime * fallAcceleration * falling);
                } else {
                    gravity = Mathf.Lerp(30, 100, Time.deltaTime * fallAcceleration * falling);
                }

                verticalMomentum.y = Mathf.Clamp(verticalMomentum.y, maxFallSpeed, -maxFallSpeed);
                // Debug.Log(gravity + " " + verticalMomentum);
            } else {
                gravity = 30f;
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
} 

using UnityEngine;
using System.Linq; 
using System.Collections.Generic;

public class InputBuffer<T>
{
    public struct BufferedInput {
        public T Input;
        public float TimeStamp;
    }
    private Queue<BufferedInput> bufferQueue = new Queue<BufferedInput>();
    public IReadOnlyCollection<BufferedInput> BufferQueue => bufferQueue;
    // public List<BufferedInput> bufferList = new List<BufferedInput>();
    public float bufferTime;

    public InputBuffer(float bufferTime) {
        this.bufferTime = bufferTime;
    }

    public T ReturnLastInput() {
        return bufferQueue.Last().Input;
    }

    public void AddInput(T input) {
        bufferList.Add(new BufferedInput {
            Input = input, TimeStamp = Time.time 
        });
    }

     public void PeekInput(out T input, int i) {
        float currentTime = Time.time;

        if (currentTime - bufferList[i].TimeStamp <= bufferTime) {
            input = bufferList[i].Input;
        } else {
            bufferList.RemoveAt(i);
            input = default;
        }
    }

    public void InputCleaner() {
        float currentTime = Time.time;

        Debug.LogWarning("INPUT CLEANER : bufferList.Count = " + bufferList.Count);

        // int bufferListCount =  bufferList.Count;

        if(bufferList.Count > 0) { 
            // for (int i = 0; i < bufferListCount; i++)
            for (int i = bufferList.Count - 1; i >= 0; i--){
                float duration = currentTime - bufferList[i].TimeStamp;
                if (currentTime - bufferList[i].TimeStamp > bufferTime) {
                    Debug.Log(bufferList[i].Input + " is cleaning" + duration);
                    bufferList.RemoveAt(i);
                }
            }
        }
    }

    public bool IsRunning(){
        float currentTime = Time.time;
        for (int i = 0; i < bufferList.Count; i++) {
            if (currentTime - bufferList[i].TimeStamp <= bufferTime) {
                return true;
            }
        }
        return false;
    }

    public bool ConsumeInput(out T input) {
        float currentTime = Time.time;
        for (int i = 0; i < bufferList.Count; i++) {
            if (currentTime - bufferList[i].TimeStamp <= bufferTime) {
                input = bufferList[i].Input;
                bufferList.RemoveAt(i);
                return true;
            }
        }
        input = default;
        return false;
    }

    public void Clear() {
        bufferList.Clear();
    }
}
