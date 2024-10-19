using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityUtils;

[RequireComponent(typeof(TriggerArea))]
public class MovingPlatform : MonoBehaviour
{
    [SerializeField] float movementSpeed = 5f;
    [SerializeField] float waitTime = 2f;
    [SerializeField] bool reverseDirection;
    [SerializeField] List<Transform> waypoints = new();

    bool isWaiting;
    int currentWaypointIndex;
    Transform currentWaypoint;

    TriggerArea triggerArea;
    Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        triggerArea = GetComponent<TriggerArea>();

        rb.freezeRotation = true;
        rb.useGravity = false;
        rb.isKinematic = true;

        if (waypoints.Count <= 0)
        {
            Debug.LogWarning($"No waypoints have been assigned to {name}!");
        }
        else
        {
            currentWaypoint = waypoints[currentWaypointIndex];
        }

        StartCoroutine(WaitRoutine());
        StartCoroutine(LateFixedUpdate());
    }

    IEnumerator WaitRoutine()
    {
        var duration = Helpers.GetWaitForSeconds(waitTime);
        while (true)
        {
            if (isWaiting)
            {
                yield return duration;
                isWaiting = false;
            }

            yield return null;
        }
    }

    IEnumerator LateFixedUpdate()
    {
        while (true)
        {
            yield return WaitFor.FixedUpdate;
            MovePlatform();
        }
    }

    void UpdateWaypoint()
    {
        currentWaypointIndex += reverseDirection ? -1 : 1;

        currentWaypointIndex = (currentWaypointIndex + waypoints.Count) % waypoints.Count;

        currentWaypoint = waypoints[currentWaypointIndex];
        isWaiting = true;
    }

    void MovePlatform()
    {
        if (waypoints.Count <= 0 || isWaiting) return;

        var toNextWaypoint = currentWaypoint.position - transform.position;
        var movement = toNextWaypoint.normalized * (movementSpeed * Time.deltaTime);

        if (movement.magnitude >= toNextWaypoint.magnitude || movement.magnitude == 0f)
        {
            rb.transform.position = currentWaypoint.position;
            UpdateWaypoint();
        }
        else
        {
            rb.transform.position += movement;
        }

        for (var i = 0; i < triggerArea.RigidBodies.Count; i++)
        {
            var rigidBody = triggerArea.RigidBodies[i];
            rigidBody.MovePosition(rigidBody.position + movement);
        }
    }
}