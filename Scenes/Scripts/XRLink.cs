using UnityEngine;

public class RecenterCanvas : MonoBehaviour
{
    public Transform centerEye;

    void Start()
    {
        if (centerEye == null)
            centerEye = GameObject.Find("CenterEyeAnchor").transform;

        Vector3 forward = centerEye.forward;
        forward.y = 0; // ignora inclinazione verticale
        transform.position = centerEye.position + forward.normalized * 1.2f; // 1.2m davanti
        transform.rotation = Quaternion.LookRotation(forward);
    }
}
