using UnityEngine;
using UnityUtils;

namespace Player_Controller
{
    public class TurnTowardController : MonoBehaviour
    {
        [SerializeField] PlayerController controller;
        public float turnSpeed = 1300f;

        public Transform tr;
        float currentYRotation;
        const float fallOffAngle = 90f;

        void Start()
        {
            tr = transform;

            currentYRotation = tr.localEulerAngles.y;
        }

        void LateUpdate()
        {
            if (controller.lockRotation) return;
            Vector3 velocity = Vector3.ProjectOnPlane(controller.GetMovementVelocity(), tr.parent.up);
            if (velocity.magnitude < 0.001f) return;

            float angleDifference = VectorMath.GetAngle(tr.forward, velocity.normalized, tr.parent.up);

            float step = Mathf.Sign(angleDifference) *
            Mathf.InverseLerp(0f, fallOffAngle, Mathf.Abs(angleDifference)) *
            Time.deltaTime * turnSpeed;

            currentYRotation += Mathf.Abs(step) > Mathf.Abs(angleDifference) ? angleDifference : step;

            tr.localRotation = Quaternion.Euler(0f, currentYRotation, 0f);
        }
    }
}