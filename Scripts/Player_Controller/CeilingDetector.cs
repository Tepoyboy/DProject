using UnityEngine;

namespace Player_Controller
{
    public class CeilingDetector : MonoBehaviour
    {
        public float ceilingAngleLimit = 5f;
        public float angle;
        public bool isInDebugMode;
        float debugDrawDuration = 2.0f;
        float ceilHitTimeStamp;
        float ceilHitDuration;
        float ceilHitDurationMax;
        bool ceilingStillHitting;
        bool ceilingWasHit;

        void OnCollisionEnter(Collision collision) => CheckForContact(collision);
        void OnCollisionStay(Collision collision) => CheckForStayContact(collision);

        void CheckForStayContact(Collision collision)
        {
            if (collision.contacts.Length == 0) return;

            angle = Vector3.Angle(-transform.up, collision.contacts[0].normal);

            ceilHitDuration = Time.time;

            if (angle > 50)
            {
                return;
            }
            else if (angle > 35)
            {
                ceilHitDurationMax = .033f;
            }
            else if (angle > 25f)
            {
                ceilHitDurationMax = .025f;
            }
            else if (angle > 15)
            {
                ceilHitDurationMax = .015f;
            }
            else if (angle > 5)
            {
                ceilHitDurationMax = .005f;
            }

            if (ceilHitDuration - ceilHitTimeStamp >= ceilHitDurationMax)
            {
                ceilingStillHitting = true;
            }
        }

        void CheckForContact(Collision collision)
        {
            if (collision.contacts.Length == 0) return;

            ceilHitTimeStamp = Time.time;


            angle = Vector3.Angle(-transform.up, collision.contacts[0].normal);

            if (angle < ceilingAngleLimit)
            {
                ceilingWasHit = true;
            }

            if (isInDebugMode)
            {
                Debug.DrawRay(collision.contacts[0].point, collision.contacts[0].normal, Color.red, debugDrawDuration);
            }
        }

        public bool HitCeiling() => ceilingWasHit;
        public bool HitCeilingOnStay() => ceilingStillHitting;
        public float HitCeilingOnStayAngle() => angle * .15f;
        public void Reset()
        {
            ceilingWasHit = false;
            ceilingStillHitting = false;
        }
    }
}