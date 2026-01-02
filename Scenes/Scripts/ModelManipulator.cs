using UnityEngine;

public class ModelManipulator : MonoBehaviour
{
    public float rotationSpeed = 100f;
    public float moveSpeed = 0.3f;

    void Update()
    {
        if (Input.GetMouseButton(1))
        {
            float rotX = Input.GetAxis("Mouse X") * rotationSpeed * Time.deltaTime;
            float rotY = -Input.GetAxis("Mouse Y") * rotationSpeed * Time.deltaTime;
            transform.Rotate(Vector3.up, -rotX, Space.World);
            transform.Rotate(Vector3.right, rotY, Space.World);
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f)
            transform.position += Camera.main.transform.forward * scroll * moveSpeed;
    }
}
