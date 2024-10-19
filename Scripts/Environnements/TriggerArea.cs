using System.Collections.Generic;
using UnityEngine;

public class TriggerArea : MonoBehaviour {
    readonly List<Rigidbody> rigidBodies = new();
    public IReadOnlyList<Rigidbody> RigidBodies => rigidBodies;

    void OnTriggerEnter(Collider col) {
        if (col.attachedRigidbody != null) {
            rigidBodies.Add(col.attachedRigidbody);
        }
    }

    void OnTriggerExit(Collider col) {
        if (col.attachedRigidbody != null) {
            rigidBodies.Remove(col.attachedRigidbody);
        }
    }
}