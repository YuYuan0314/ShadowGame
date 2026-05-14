using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ShadowCasterMover : MonoBehaviour, IShadowMover
{
    [Header("·������")]
    public List<Transform> waypoints;
    public float moveSpeed = 5f;
    public float resetDelay = 1f;

    [Header("��������")]
    [Tooltip("����ҴӴ�������Ⱥ���������ʧʱ�����صı�ѡͶ����")]
    public GameObject fallbackSource;

    private int currentIndex = 0;
    private bool isMoving = true;
    private Vector3 originalScale;

    void Start()
    {
        originalScale = transform.localScale;
        if (waypoints != null && waypoints.Count > 0)
            transform.position = waypoints[0].position;
    }

    void Update()
    {
        if (!isMoving || waypoints == null || waypoints.Count < 2) return;

        Vector3 target = waypoints[currentIndex].position;
        transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, target) < 0.01f)
        {
            currentIndex++;
            if (currentIndex >= waypoints.Count) StartCoroutine(ResetSequence());
        }
    }

    IEnumerator ResetSequence()
    {
        isMoving = false;
        transform.localScale = Vector3.zero;
        yield return new WaitForSeconds(resetDelay);

        currentIndex = 0;
        if (waypoints.Count > 0) transform.position = waypoints[0].position;
        transform.localScale = originalScale;
        isMoving = true;
    }

    public GameObject GetFallbackTarget() => fallbackSource;
}