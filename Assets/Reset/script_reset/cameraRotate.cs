using UnityEngine;

public class cameraRotate : MonoBehaviour
{
    public float sensitivity = 100f;
    public GameObject target;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        // 鼠标控制相机
        float x = Input.GetAxis("Mouse X") * sensitivity * Time.deltaTime;
        float y = Input.GetAxis("Mouse Y") * sensitivity * Time.deltaTime;
        transform.localRotation = Quaternion.Euler(transform.localRotation.eulerAngles.x - y, transform.localRotation.eulerAngles.y + x, 0);

        // ESC解锁鼠标
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ?
                CursorLockMode.None : CursorLockMode.Locked;
        }

    }
}