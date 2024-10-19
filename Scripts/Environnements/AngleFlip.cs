using UnityEngine;

public class AngleFlip : MonoBehaviour
{
    void OnTriggerEnter(Collider col)
    {
        if (col.attachedRigidbody != null)
        {
            FlipDirection(transform.forward, col.transform);
        }
    }

    void FlipDirection(Vector3 newUpDirection, Transform tr)
    {
        var angleBetweenUpDirections = Vector3.Angle(newUpDirection, tr.up);
        var angleThreshold = 0.001f;

        if (angleBetweenUpDirections < angleThreshold) return;

        var rotationDifference = Quaternion.FromToRotation(tr.up, newUpDirection);
        tr.rotation = rotationDifference * tr.rotation;
    }
}