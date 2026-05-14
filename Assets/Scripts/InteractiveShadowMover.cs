using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class InteractiveShadowMover : MonoBehaviour, IShadowMover
{
    [Header("·������")]
    public List<Transform> waypoints;
    public float moveSpeed = 5f;
    public float resetDelay = 1f;

    [Header("��������")]
    public GameObject fallbackSource;

    [Header("��������")]
    public bool autoStart = false; // �Ƿ��Զ���ʼ�ƶ�
    private bool isMoving = false;
    private int currentIndex = 0;
    private Vector3 originalScale;

    void Start()
    {
        originalScale = transform.localScale;
        if (waypoints != null && waypoints.Count > 0)
            transform.position = waypoints[0].position;

        isMoving = autoStart; // �����ѡ���Զ���ʼ�����ʼ�����ƶ�
    }

    void Update()
    {
        if (!isMoving || waypoints == null || waypoints.Count < 2) return;

        Vector3 target = waypoints[currentIndex].position;
        transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, target) < 0.01f)
        {
            currentIndex++;
            // �����յ�
            if (currentIndex >= waypoints.Count)
            {
                StartCoroutine(ResetSequence());
            }
        }
    }

    // ��������������ť�ű�����
    public void ActivateMovement()
    {
        if (!isMoving)
        {
            isMoving = true;
        }
    }

    IEnumerator ResetSequence()
    {
        isMoving = false;
        // Ӱ����ʧ��������������߼�
        transform.localScale = Vector3.zero;

        yield return new WaitForSeconds(resetDelay);

        // ����״̬
        currentIndex = 0;
        if (waypoints.Count > 0) transform.position = waypoints[0].position;
        transform.localScale = originalScale;

        // ע�⣺���ú��Ƿ�����ƶ���
        // ���ϣ����һ�ΰ�ťֻ��һ�֣����ﱣ�� isMoving = false
        // ���ϣ��������Ϳ�ʼѭ����������Ϊ isMoving = true
        isMoving = false;
    }

    public GameObject GetFallbackTarget() => fallbackSource;
}