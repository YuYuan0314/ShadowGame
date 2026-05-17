using UnityEngine;

public class InteractionButton : MonoBehaviour
{
    public InteractiveShadowMover targetMover; // ָ��Ҫ���������� B
    public float interactionRange = 3f;        // ��������
    private bool playerInRange = false;

    void Update()
    {
        // ֻ������ڷ�Χ���Ұ��� F ��ʱ����
        if (playerInRange && (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.JoystickButton2)))
        {
            if (targetMover != null)
            {
                targetMover.ActivateMovement();
                Debug.Log("�Ѵ��������ƶ���");
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

    // ��ѡ���ڱ༭���ﻭ��������Χ
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}