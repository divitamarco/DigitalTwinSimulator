using UnityEngine;
using UnityEngine.XR.Management;
using System.Collections;

public class ForceXRStart : MonoBehaviour
{
    IEnumerator Start()
    {
        var settings = XRGeneralSettings.Instance.Manager;
        if (settings.activeLoader == null)
        {
            Debug.Log("🟡 Forzo inizializzazione XR...");
            yield return settings.InitializeLoader();
        }

        if (settings.activeLoader != null)
        {
            Debug.Log("✅ XR Loader attivato manualmente");
            settings.StartSubsystems();
        }
        else
        {
            Debug.LogError("❌ Impossibile inizializzare XR!");
        }
    }
}
