using UnityEngine;
using UnityEngine.XR.Management;
using System.Collections;

public class XRInitializer : MonoBehaviour
{
    private bool xrStarted = false;

    IEnumerator Start()
    {
        var settings = XRGeneralSettings.Instance.Manager;

        if (settings == null)
        {
            Debug.LogError("XR Manager non trovato!");
            yield break;
        }

        yield return settings.InitializeLoader();

        if (settings.activeLoader == null)
        {
            Debug.LogWarning("⚠️ XR Loader non inizializzato — probabilmente stai usando Oculus Link o XR non attivo.");
            yield break;
        }

        settings.StartSubsystems();
        xrStarted = true;
        Debug.Log("✅ XR inizializzato correttamente.");
    }

    void OnDestroy()
    {
        var settings = XRGeneralSettings.Instance.Manager;
        if (settings != null && xrStarted)
        {
            settings.StopSubsystems();
            settings.DeinitializeLoader();
            Debug.Log("🛑 XR disattivato correttamente.");
        }
        else
        {
            Debug.Log("ℹ️ XR non inizializzato, quindi nessun arresto necessario.");
        }
    }
}
