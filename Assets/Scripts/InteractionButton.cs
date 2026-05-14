using UnityEngine;

public class InteractionButton : MonoBehaviour
{
    public InteractiveShadowMover targetMover; // 指定要触发的物体 B
    public float interactionRange = 3f;        // 交互距离
    private bool playerInRange = false;

    void Update()
    {
        // 只有玩家在范围内且按下 F 键时触发
        if (playerInRange && Input.GetKeyDown(KeyCode.F))
        {
            if (targetMover != null)
            {
                targetMover.ActivateMovement();
                Debug.Log("已触发物体移动！");
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) playerInRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player")) playerInRange = false;
    }

    // 可选：在编辑器里画出交互范围
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}