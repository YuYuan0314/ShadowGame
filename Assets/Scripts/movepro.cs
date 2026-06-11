using System.Collections;
using UnityEngine;

public class movepro : MonoBehaviour
{
    public enum TriggerMode
    {
        AutoOnEnter,
        Interact
    }

    [Header("Target Move")]
    public move targetMover;
    public bool keepTargetStoppedOnStart = true;
    public bool resetTargetBeforePlay = false;
    public bool triggerOnce = true;

    [Header("Trigger")]
    public TriggerMode triggerMode = TriggerMode.AutoOnEnter;
    public bool requirePlayerTag = false;
    public string playerTag = "Player";

    [Header("Interact Input")]
    public KeyCode keyboardInteractKey = KeyCode.F;
    public KeyCode gamepadInteractKey = KeyCode.JoystickButton2;

    private bool playerInRange;
    private bool hasTriggered;

    private void Awake()
    {
        if (keepTargetStoppedOnStart && targetMover != null)
        {
            targetMover.playOnStart = false;
        }
    }

    private IEnumerator Start()
    {
        yield return null;

        if (keepTargetStoppedOnStart && targetMover != null && !hasTriggered)
        {
            targetMover.Pause();
        }
    }

    private void Update()
    {
        if (triggerMode != TriggerMode.Interact || !playerInRange)
        {
            return;
        }

        if (Input.GetKeyDown(keyboardInteractKey) || Input.GetKeyDown(gamepadInteractKey))
        {
            TriggerMove();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleEnter(other.gameObject);
    }

    private void OnTriggerExit(Collider other)
    {
        HandleExit(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleEnter(collision.gameObject);
    }

    private void OnCollisionExit(Collision collision)
    {
        HandleExit(collision.gameObject);
    }

    private void HandleEnter(GameObject other)
    {
        if (!IsPlayer(other))
        {
            return;
        }

        playerInRange = true;

        if (triggerMode == TriggerMode.AutoOnEnter)
        {
            TriggerMove();
        }
    }

    private void HandleExit(GameObject other)
    {
        if (IsPlayer(other))
        {
            playerInRange = false;
        }
    }

    public void TriggerMove()
    {
        if (targetMover == null)
        {
            Debug.LogWarning("movepro has no Target Mover assigned.", this);
            return;
        }

        if (triggerOnce && hasTriggered)
        {
            return;
        }

        hasTriggered = true;

        if (resetTargetBeforePlay)
        {
            targetMover.StopMove();
        }

        targetMover.Play();
    }

    private bool IsPlayer(GameObject other)
    {
        if (other == null)
        {
            return false;
        }

        if (requirePlayerTag && !other.CompareTag(playerTag))
        {
            return false;
        }

        return other.GetComponentInParent<PlayerRbController>() != null
            || (!requirePlayerTag && other.CompareTag(playerTag));
    }
}
