using State_Machine;
using UnityEngine;

namespace Player_Controller
{
    public class GroundedState : IState
    {
        readonly PlayerController controller;

        public GroundedState(PlayerController controller)
        {
            this.controller = controller;
        }

        public void OnEnter()
        {
            // controller.inAirTimer = 0;
            controller.OnGroundContactRegained();
        }

        public void OnExit()
        {
            // controller.inAirTimer = Time.time;
        }
    }

    public class SlidingState : IState
    {
        readonly PlayerController controller;

        public SlidingState(PlayerController controller)
        {
            this.controller = controller;
        }

        public void OnEnter()
        {
            // controller.inAirTimer = 0;
            controller.OnGroundContactLost();
        }

        public void OnExit()
        {
            // controller.inAirTimer = Time.time;
        }
    }

    public class FallingState : IState
    {
        readonly PlayerController controller;

        public FallingState(PlayerController controller)
        {
            this.controller = controller;
        }

        public void OnEnter()
        {
            controller.fallDuration = Time.time;

            if (controller.ceilingDetector.HitCeilingOnStay())
            {
                controller.OnFallStartStay();
            }
            else
            {
                controller.OnFallStart();
            }
        }

        public void OnExit()
        {
            controller.fallDuration = 0;
        }
    }

    public class RisingState : IState
    {
        readonly PlayerController controller;

        public RisingState(PlayerController controller)
        {
            this.controller = controller;
        }

        public void OnEnter()
        {
            controller.OnGroundContactLost();
        }
    }

    public class JumpingState : IState
    {
        readonly PlayerController controller;

        public JumpingState(PlayerController controller)
        {
            this.controller = controller;
        }

        public void OnEnter()
        {
            controller.jumpCountdown = Time.time;
            // controller.jumpStateDuration = Time.time;
            controller.OnGroundContactLost();
            controller.OnJumpStart();
        }

        public void OnExit()
        {
            // controller.jumpStateDuration = 0;
        }
    }

    public class SmashingGroundState : IState
    {
        readonly PlayerController controller;

        public SmashingGroundState(PlayerController controller)
        {
            this.controller = controller;
        }

        public void OnEnter()
        {
            controller.fallDuration = Time.time;
            controller.HandleSmashGround();
        }

        public void OnExit()
        {
            controller.fallDuration = 0;
        }
    }

    public class DashingState : IState
    {
        readonly PlayerController controller;

        public DashingState(PlayerController controller)
        {
            this.controller = controller;
        }

        public void OnEnter()
        {
            // controller.fallDuration = Time.time;
            controller.HandleDash();
        }
        // public void OnExit() {
        //     controller.fallDuration = 0;
        // }
    }
}