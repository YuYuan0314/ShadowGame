using System;
using System.Collections.Generic;
using UnityEngine;

public class move : MonoBehaviour, IShadowMover
{
    public enum EndMode
    {
        Stop,
        Loop,
        PingPong,
        OnceAgain
    }

    [Serializable]
    public class MoveNode
    {
        public Transform point;
        [Tooltip("Speed from this node to the next node. Use 0 to use Default Speed.")]
        [Min(0f)] public float speedToNext = 0f;
        [Min(0f)] public float waitTime = 0f;
    }

    [Header("Path Nodes")]
    [Tooltip("Drag empty GameObjects here in the order the object should move.")]
    public List<MoveNode> nodes = new List<MoveNode>();

    [Header("Movement")]
    [Min(0.01f)] public float defaultSpeed = 2f;
    public EndMode endMode = EndMode.Loop;
    public bool playOnStart = true;
    public bool moveToFirstNodeOnStart = true;

    [Header("Turning")]
    public bool faceMoveDirection = true;
    [Min(0.01f)] public float turnSpeed = 8f;
    public bool smoothTurnAtCorners = true;
    [Min(0f)] public float turnLookAheadDistance = 1f;
    public float yawOffset = 180f;
    public bool keepUpright = true;

    [Header("Shadow Reset")]
    [Tooltip("When the player leaves this moving object's shadow, reset to this object's shadow instead.")]
    public GameObject fallbackSource;

    private int currentIndex;
    private int direction = 1;
    private bool isMoving;
    private float waitTimer;

    private void Start()
    {
        if (moveToFirstNodeOnStart && nodes.Count > 0 && nodes[0].point != null)
        {
            transform.position = nodes[0].point.position;
            currentIndex = 0;
        }

        isMoving = playOnStart;
    }

    private void Update()
    {
        if (!isMoving || nodes.Count == 0)
        {
            return;
        }

        if (waitTimer > 0f)
        {
            if (faceMoveDirection)
            {
                int waitTargetIndex = GetNextIndex();
                if (waitTargetIndex >= 0 && nodes[waitTargetIndex].point != null)
                {
                    RotateTowardsPath(waitTargetIndex, nodes[waitTargetIndex].point.position);
                }
            }

            waitTimer -= Time.deltaTime;
            return;
        }

        int targetIndex = GetNextIndex();
        if (targetIndex < 0 || nodes[targetIndex].point == null)
        {
            isMoving = false;
            return;
        }

        Vector3 targetPosition = nodes[targetIndex].point.position;

        if (faceMoveDirection)
        {
            RotateTowardsPath(targetIndex, targetPosition);
        }

        float speed = GetCurrentSpeed();
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetPosition) <= 0.001f)
        {
            currentIndex = targetIndex;

            if (endMode == EndMode.OnceAgain && currentIndex >= nodes.Count - 1)
            {
                ResetToFirstNodeForOnceAgain();
                return;
            }

            waitTimer = nodes[currentIndex].waitTime;
        }
    }

    public void Play()
    {
        isMoving = true;
    }

    public void Pause()
    {
        isMoving = false;
    }

    public void StopMove()
    {
        isMoving = false;
        currentIndex = 0;
        direction = 1;
        waitTimer = 0f;

        if (nodes.Count > 0 && nodes[0].point != null)
        {
            transform.position = nodes[0].point.position;
        }
    }

    public GameObject GetFallbackTarget()
    {
        return fallbackSource;
    }

    private int GetNextIndex()
    {
        if (nodes.Count <= 1)
        {
            return -1;
        }

        int nextIndex = currentIndex + direction;

        if (nextIndex >= 0 && nextIndex < nodes.Count)
        {
            return nextIndex;
        }

        switch (endMode)
        {
            case EndMode.Loop:
                return direction > 0 ? 0 : nodes.Count - 1;

            case EndMode.PingPong:
                direction *= -1;
                nextIndex = currentIndex + direction;
                return nextIndex >= 0 && nextIndex < nodes.Count ? nextIndex : -1;

            case EndMode.OnceAgain:
                return -1;

            default:
                return -1;
        }
    }

    private int PeekNextIndexFrom(int index)
    {
        if (nodes.Count <= 1)
        {
            return -1;
        }

        int nextIndex = index + direction;

        if (nextIndex >= 0 && nextIndex < nodes.Count)
        {
            return nextIndex;
        }

        switch (endMode)
        {
            case EndMode.Loop:
                return direction > 0 ? 0 : nodes.Count - 1;

            case EndMode.PingPong:
                nextIndex = index - direction;
                return nextIndex >= 0 && nextIndex < nodes.Count ? nextIndex : -1;

            case EndMode.OnceAgain:
                return -1;

            default:
                return -1;
        }
    }

    private void ResetToFirstNodeForOnceAgain()
    {
        if (nodes.Count == 0 || nodes[0].point == null)
        {
            isMoving = false;
            return;
        }

        transform.position = nodes[0].point.position;
        currentIndex = 0;
        direction = 1;
        waitTimer = 0f;

        int nextIndex = GetNextIndex();
        if (faceMoveDirection && nextIndex >= 0 && nodes[nextIndex].point != null)
        {
            SnapRotationTowards(nodes[nextIndex].point.position - transform.position);
        }
    }

    private void SnapRotationTowards(Vector3 faceDirection)
    {
        if (keepUpright)
        {
            faceDirection = Vector3.ProjectOnPlane(faceDirection, Vector3.up);
        }

        if (faceDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(faceDirection.normalized, Vector3.up) * Quaternion.Euler(0f, yawOffset, 0f);
    }

    private void RotateTowardsPath(int targetIndex, Vector3 targetPosition)
    {
        if (targetIndex < 0)
        {
            return;
        }

        Vector3 toTarget = targetPosition - transform.position;
        Vector3 faceDirection = toTarget;

        if (smoothTurnAtCorners && turnLookAheadDistance > 0f)
        {
            int nextIndex = PeekNextIndexFrom(targetIndex);
            if (nextIndex >= 0 && nodes[nextIndex].point != null)
            {
                Vector3 nextSegmentDirection = nodes[nextIndex].point.position - targetPosition;
                float distanceToTarget = toTarget.magnitude;
                float turnAmount = Mathf.Clamp01(1f - distanceToTarget / turnLookAheadDistance);

                if (nextSegmentDirection.sqrMagnitude > 0.0001f && turnAmount > 0f)
                {
                    faceDirection = toTarget.sqrMagnitude > 0.0001f
                        ? Vector3.Slerp(toTarget.normalized, nextSegmentDirection.normalized, turnAmount)
                        : nextSegmentDirection;
                }
            }
        }

        if (keepUpright)
        {
            faceDirection = Vector3.ProjectOnPlane(faceDirection, Vector3.up);
        }

        if (faceDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(faceDirection.normalized, Vector3.up) * Quaternion.Euler(0f, yawOffset, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
    }

    private float GetCurrentSpeed()
    {
        if (currentIndex >= 0 && currentIndex < nodes.Count && nodes[currentIndex].speedToNext > 0f)
        {
            return nodes[currentIndex].speedToNext;
        }

        return defaultSpeed;
    }

    private void OnDrawGizmos()
    {
        if (nodes == null || nodes.Count == 0)
        {
            return;
        }

        Gizmos.color = Color.cyan;

        for (int i = 0; i < nodes.Count; i++)
        {
            Transform point = nodes[i].point;
            if (point == null)
            {
                continue;
            }

            Gizmos.DrawSphere(point.position, 0.15f);

            int nextIndex = i + 1;
            if (nextIndex < nodes.Count && nodes[nextIndex].point != null)
            {
                Gizmos.DrawLine(point.position, nodes[nextIndex].point.position);
            }
        }

        if (endMode == EndMode.Loop && nodes.Count > 1)
        {
            Transform first = nodes[0].point;
            Transform last = nodes[nodes.Count - 1].point;
            if (first != null && last != null)
            {
                Gizmos.DrawLine(last.position, first.position);
            }
        }
    }
}
