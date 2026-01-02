using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

public class RenderCaptureAndUpload : MonoBehaviour
{
    [Header("Capture")]
    public Camera captureCamera;          // camera da cui fare il render (se null -> Camera.main)
    public int captureWidth = 1024;       // risoluzione di render
    public int captureHeight = 1024;
    public bool transparentBackground = false; // se vuoi alpha (se la pipeline lo supporta)

    [Header("Server")]
    public string serverBaseUrl = "http://127.0.0.1:5000"; // IP del PC se in emulatore/device
    public string segmentEndpoint = "/segment";

    [Header("UI (opzionale)")]
    public Button captureButton;
    public TextMeshProUGUI statusText;

    void Start()
    {
        if (captureCamera == null) captureCamera = Camera.main;
        if (captureButton != null)
        {
            captureButton.onClick.RemoveAllListeners();
            captureButton.onClick.AddListener(() => StartCoroutine(CaptureAndUploadCoroutine()));
        }
    }

    IEnumerator CaptureAndUploadCoroutine()
    {
        if (captureCamera == null)
        {
            Log("No capture camera assigned.");
            yield break;
        }

        // crea RenderTexture temporanea
        var rt = new RenderTexture(captureWidth, captureHeight, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 1;
        var prevRT = captureCamera.targetTexture;
        captureCamera.targetTexture = rt;

        // aspetta fine frame per essere sicuri che la camera abbia renderizzato
        yield return new WaitForEndOfFrame();

        // forziamo il render (utile se la camera non è auto-render)
        captureCamera.Render();

        // leggi i pixel dalla RT
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(captureWidth, captureHeight, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
        tex.Apply();

        // pulizie rendertexture
        RenderTexture.active = null;
        captureCamera.targetTexture = prevRT;
        rt.Release();
        Destroy(rt);

        // opzionale: salva su disco (utile per debug)
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string saveName = $"capture_{timestamp}.png";
        string savePath = Path.Combine(Application.persistentDataPath, saveName);
        try
        {
            byte[] png = tex.EncodeToPNG();
            File.WriteAllBytes(savePath, png);
            Log($"Saved capture to: {savePath}");
        }
        catch (Exception e)
        {
            Log($"Error saving PNG: {e.Message}");
        }

        // invia al server (multipart/form-data)
        yield return StartCoroutine(UploadTextureToSegment(tex, Path.GetFileName(savePath)));

        // distruggi texture di lavoro
        Destroy(tex);
    }

    IEnumerator UploadTextureToSegment(Texture2D tex, string filename)
    {
        string url = serverBaseUrl.TrimEnd('/') + segmentEndpoint;
        byte[] png = tex.EncodeToPNG();

        Log($"Uploading {png.Length} bytes to {url} ...");

        WWWForm form = new WWWForm();
        form.AddBinaryData("image", png, filename, "image/png");

        using (UnityWebRequest www = UnityWebRequest.Post(url, form))
        {
            www.timeout = 120; // timeout in secondi
            yield return www.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
            if (www.result != UnityWebRequest.Result.Success)
#else
            if (www.isNetworkError || www.isHttpError)
#endif
            {
                Log($"Upload error: {www.responseCode} {www.error}\n{www.downloadHandler?.text}");
                yield break;
            }

            string response = www.downloadHandler.text;
            Log($"Server response: {response}");
        }
    }

    void Log(string msg)
    {
        Debug.Log(msg);
        if (statusText != null) statusText.text = msg + "\n" + statusText.text;
    }
}
