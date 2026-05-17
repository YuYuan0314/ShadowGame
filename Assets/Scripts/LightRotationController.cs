using UnityEngine;
using System.Collections;

public class LightRotationController : MonoBehaviour
{
    [Header("Light Reference")]
    public Light directionalLight;

    [Header("Keyboard Snap Rotation")]
    public KeyCode rotateLeftKey = KeyCode.G;
    public KeyCode rotateRightKey = KeyCode.H;
    public float snapAngle = 45f;
    public float snapDuration = 0.5f;

    [Header("Gamepad Trigger Rotation")]
    public float triggerRotateSpeed = 60f;

    [Header("Interaction")]
    public float interactionRange = 3f;

    private bool playerInRange;
    private bool isRotating;

    void Update()
    {
        if (!playerInRange || isRotating) return;

        // Keyboard snap rotation (G/H)
        if (Input.GetKeyDown(rotateLeftKey))
            StartCoroutine(SnapRotate(snapAngle));
        else if (Input.GetKeyDown(rotateRightKey))
            StartCoroutine(SnapRotate(-snapAngle));

        // Gamepad trigger continuous rotation (LT/RT)
        float lt = Input.GetAxis("LightRotateL");
        float rt = Input.GetAxis("LightRotateR");

        if (lt > 0.1f)
            directionalLight.transform.rotation *= Quaternion.Euler(0, -triggerRotateSpeed * Time.deltaTime * lt, 0);

        if (rt > 0.1f)
            directionalLight.transform.rotation *= Quaternion.Euler(0, triggerRotateSpeed * Time.deltaTime * rt, 0);
    }

    IEnumerator SnapRotate(float angleY)
    {
        isRotating = true;

        Quaternion startRotation = directionalLight.transform.rotation;
        Quaternion endRotation = Quaternion.Euler(0, angleY, 0) * startRotation;

        float elapsed = 0;
        while (elapsed < snapDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / snapDuration;
            directionalLight.transform.rotation = Quaternion.Slerp(startRotation, endRotation, t);
            yield return null;
        }

        directionalLight.transform.rotation = endRotation;
        isRotating = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) playerInRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player")) playerInRange = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
