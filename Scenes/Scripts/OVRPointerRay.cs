using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class OVRPointerRay : MonoBehaviour
{
    public float rayLength = 5f;
    private LineRenderer lr;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.enabled = true;
    }

    void Update()
    {
        Vector3 start = transform.position;
        Vector3 end = start + transform.forward * rayLength;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
    }
}
