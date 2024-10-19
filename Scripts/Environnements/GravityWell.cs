using UnityEngine;
using UnityUtils;

[RequireComponent(typeof(TriggerArea))]
public class GravityWell : MonoBehaviour
{
    TriggerArea triggerArea;

    void Start()
    {
        triggerArea = GetComponent<TriggerArea>();
    }

    void FixedUpdate()
    {
        for (var i = 0; i < triggerArea.RigidBodies.Count; i++)
        {
            var rb = triggerArea.RigidBodies[i];

            var directionToRb = rb.transform.position - transform.position;
            Debug.DrawLine(transform.position, rb.transform.position, Color.red);

            var projection = Vector3.Project(directionToRb, transform.forward);
            Debug.DrawLine(transform.position, transform.position + projection, Color.blue);

            var center = projection + transform.position;
            Debug.DrawLine(transform.position, center, Color.green);

            var directionToCenter = center - rb.transform.position;
            Debug.DrawLine(rb.transform.position, center, Color.yellow);

            RotateRigidbody(rb, directionToCenter);
        }
    }

    void OnTriggerExit(Collider col)
    {
        if (col.TryGetComponent(out Rigidbody rb))
        {
            RotateRigidbody(rb, Vector3.up);
            var eulerAngles = rb.rotation.eulerAngles.With(x: 0f, z: 0f);
            rb.MoveRotation(Quaternion.Euler(eulerAngles));
        }
    }

    void RotateRigidbody(Rigidbody rb, Vector3 targetDirection)
    {
        targetDirection.Normalize();

        var rotationDifference = Quaternion.FromToRotation(rb.transform.up, targetDirection);
        var finalRotation = rotationDifference * rb.transform.rotation;

        rb.MoveRotation(finalRotation);
    }
}