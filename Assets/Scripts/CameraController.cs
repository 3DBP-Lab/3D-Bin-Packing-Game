using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Camera Controls")]
    [SerializeField] private Vector3 targetPosition = Vector3.zero; // 摄像机围绕的中心点
    [SerializeField] private float rotationSpeed = 2.0f;
    [SerializeField] private float zoomSpeed = 2.0f;
    [SerializeField] private float panSpeed = 1.0f;

    [Header("Distance Limits")]
    [SerializeField] private float minZoomDistance = 10f;
    [SerializeField] private float maxZoomDistance = 150f;

    private void Update()
    {
        // 1. 缩放 (Zoom) - 鼠标滚轮
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        if (scrollInput != 0)
        {
            // 获取当前摄像机到目标的距离
            float currentDistance = Vector3.Distance(transform.position, targetPosition);
            // 根据滚轮输入调整距离，同时限制在最大和最小距离之间
            float newDistance = Mathf.Clamp(currentDistance - scrollInput * zoomSpeed * 10f, minZoomDistance, maxZoomDistance);
            // 将摄像机移动到新距离的位置
            transform.position = targetPosition + (transform.position - targetPosition).normalized * newDistance;
        }

        // 2. 环绕 (Orbit) - 按住鼠标中键拖动
        if (Input.GetMouseButton(2)) 
        {
            float mouseX = Input.GetAxis("Mouse X") * rotationSpeed;
            float mouseY = Input.GetAxis("Mouse Y") * rotationSpeed;

            // 水平环绕 (围绕Y轴)
            transform.RotateAround(targetPosition, Vector3.up, mouseX);
            // 垂直环绕 (围绕摄像机的right轴)
            transform.RotateAround(targetPosition, transform.right, -mouseY);
        }

        // 3. 平移 (Pan) - 按住鼠标右键拖动
        if (Input.GetMouseButton(1))
        {
            float panX = Input.GetAxis("Mouse X") * panSpeed;
            float panY = Input.GetAxis("Mouse Y") * panSpeed;

            // 根据摄像机的方向计算平移向量
            Vector3 move = -transform.right * panX - transform.up * panY;
            
            // 同时移动摄像机和目标点
            transform.position += move;
            targetPosition += move;
        }
    }
}