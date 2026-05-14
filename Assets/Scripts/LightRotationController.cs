using UnityEngine;
using System.Collections;

public class LightRotationController : MonoBehaviour
{
    [Header("引用设置")]
    public Light directionalLight;      // 拖入场景中的直射光
    public float rotationDuration = 0.5f; // 旋转动画持续时间

    [Header("交互设置")]
    public float interactionRange = 3f;
    private bool playerInRange = false;
    private bool isRotating = false; // 防止旋转动画重叠

    void Update()
    {
        if (playerInRange && !isRotating)
        {
            // 按 F 逆时针旋转 45 度 (XZ平面)
            if (Input.GetKeyDown(KeyCode.G))
            {
                StartCoroutine(RotateLight(45f));
            }
            // 按 G 顺时针旋转 45 度 (XZ平面)
            else if (Input.GetKeyDown(KeyCode.H))
            {
                StartCoroutine(RotateLight(-45f));
            }
        }
    }

    /// <summary>
    /// 平滑旋转灯光
    /// </summary>
    /// <param name="angleY">旋转的角度（Y轴偏移）</param>
    IEnumerator RotateLight(float angleY)
    {
        isRotating = true;

        Quaternion startRotation = directionalLight.transform.rotation;
        // 在当前旋转的基础上，绕世界坐标的 Y 轴进行旋转
        Quaternion endRotation = Quaternion.Euler(0, angleY, 0) * startRotation;

        float elapsed = 0;
        while (elapsed < rotationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / rotationDuration;

            // 使用平滑插值，让旋转看起来更自然
            directionalLight.transform.rotation = Quaternion.Slerp(startRotation, endRotation, t);
            yield return null;
        }

        directionalLight.transform.rotation = endRotation;
        isRotating = false;
    }

    // --- 玩家范围检测 ---
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