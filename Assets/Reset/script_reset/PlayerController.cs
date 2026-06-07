using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("重力控制")]
    [Range(1, 5)]
    public float highGravity = 2.5f;
    [Range(1, 4)]
    public float lowGravity = 1.5f;
    [Range(0, 10)]
    public float UpVelocity = 5f;
    [Range(0, 1)]
    public float timeLimit = 0.05f;
    public float jumpHoldTime = 0f;

    public Rigidbody rb;
    public Camera main_cam;

    [Header("移动控制")]
    [Range(1, 10)]
    public float moveVelocity = 5f;
    public float lowRotateSpeed = 10f;
    public float highRotateSpeed = 20f;
    [Header("动画控制")]
    public Animator animator;

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int IsJumpingHash = Animator.StringToHash("IsJumping");
    private int currentAnimStateHash;

    // 地面检测
    private bool isGrounded;
    public Transform groundCheck;  
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;

    void Start()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (main_cam == null) main_cam = Camera.main;
        if (animator == null) animator = GetComponent<Animator>();
        if (groundCheck == null)
        {
            GameObject go = new GameObject("GroundCheck");
            go.transform.SetParent(transform);
            go.transform.localPosition = new Vector3(0, -0.5f, 0);
            groundCheck = go.transform;
        }
    }

    void Update()
    {
        // ========= 1. 地面检测 =========
        isGrounded = Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundLayer);
        Debug.Log($"IsGrounded: {isGrounded}");
        if (isGrounded)
            jumpHoldTime = 0f;
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");
        bool hasMoveInput = Mathf.Abs(x) > 0.05f || Mathf.Abs(z) > 0.05f;

        Vector3 cameraForward = main_cam.transform.forward;
        Vector3 cameraRight = main_cam.transform.right;
        cameraForward.y = 0;
        cameraRight.y = 0;
        cameraForward.Normalize();
        cameraRight.Normalize();
        Vector3 moveDir = (cameraForward * z + cameraRight * x).normalized;

        rb.velocity = new Vector3(moveDir.x * moveVelocity, rb.velocity.y, moveDir.z * moveVelocity);
        if(moveDir!=Vector3.zero)
        {
            Quaternion targetRotation= Quaternion.LookRotation(moveDir)*Quaternion.Euler(0,180,0);
            float angle = Quaternion.Angle(transform.rotation, targetRotation);
            //if (angle > 90f)
            //    transform.rotation = targetRotation;
            float rotateSpeed=angle>90f?highRotateSpeed:lowRotateSpeed;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime*rotateSpeed);
        }

        bool isMoving = hasMoveInput && isGrounded && Mathf.Abs(rb.velocity.y) < 0.1f;

        bool isJumping = false;

        if (rb.velocity.y < 0)
        {
            rb.velocity += Physics.gravity * (highGravity - 1) * Time.deltaTime;
        }
        else if (rb.velocity.y > 0 && !Input.GetKey(KeyCode.Space))
        {
            rb.velocity += Physics.gravity * (lowGravity - 1) * Time.deltaTime;
        }

        if (Input.GetKey(KeyCode.Space) && isGrounded && jumpHoldTime < timeLimit)
        {
            rb.velocity = new Vector3(rb.velocity.x, UpVelocity, rb.velocity.z);
            jumpHoldTime += Time.deltaTime;
            isJumping = true; 
        }

        if (Input.GetKeyUp(KeyCode.Space))
        {
            jumpHoldTime = 0f;
        }

        if (!isGrounded || rb.velocity.y > 0.2f)
            isJumping = true;

        animator.SetBool(IsMovingHash, isMoving);
        animator.SetBool(IsJumpingHash, isJumping);

        int targetStateHash;
        if (isJumping)
            targetStateHash = Animator.StringToHash("Jump");
        else if (isMoving)
            targetStateHash = Animator.StringToHash("Walk");
        else
            targetStateHash = Animator.StringToHash("Idle");

        if (currentAnimStateHash != targetStateHash)
        {
            animator.CrossFade(targetStateHash, 0.1f);
            currentAnimStateHash = targetStateHash;
        }
    }

    // 辅助：在 Scene 视图中可视化地面检测范围
    //void OnDrawGizmosSelected()
    //{
    //    if (groundCheck != null)
    //    {
    //        Gizmos.color = Color.green;
    //        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    //    }
    //}
}