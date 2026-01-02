using UnityEngine;
using UnityEngine.XR;

public class XRDebug : MonoBehaviour
{
    void Start()
    {
        if (XRSettings.isDeviceActive)
            Debug.Log($"✅ XR attivo con device: {XRSettings.loadedDeviceName}");
        else
            Debug.Log("❌ Nessun device XR attivo");
    }
}
