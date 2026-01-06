using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using GLTFast;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class TwinWorkflow : MonoBehaviour
{
    // =========================
    // SERVER
    // =========================
    [Header("Server")]
    public string serverBaseUrl = "http://127.0.0.1:5000";

    // =========================
    // UI
    // =========================
    [Header("UI")]
    public TextMeshProUGUI logText;

    // =========================
    // STATE
    // =========================
    private string selectedImagePath;
    private bool isProcessing = false;

    // 🔒 XR / UI FRAME GUARD
    private int _lastUiEventFrame = -1;

    // =========================================================
    // FRAME GUARD (XR-safe, REQUIRED)
    // =========================================================
    private bool FrameGuard()
    {
        if (_lastUiEventFrame == Time.frameCount)
            return false;

        _lastUiEventFrame = Time.frameCount;
        return true;
    }

    // =========================================================
    // 1) CHOOSE IMAGE
    // =========================================================
    public void OnBrowseImage()
    {
#if UNITY_EDITOR
        if (!FrameGuard()) return;

        string path = EditorUtility.OpenFilePanel(
            "Select image", "", "png,jpg,jpeg");

        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            selectedImagePath = path;
            SafeLog($"[Browse] Selected: {path}");
        }
        else
        {
            SafeLog("[Browse] No file selected.");
        }
#else
        SafeLog("[Browse] Editor only.");
#endif
    }

    // =========================================================
    // 2) SEGMENT (SAM)
    // =========================================================
    public void OnSegment()
    {
        if (isProcessing) return;

        if (string.IsNullOrEmpty(selectedImagePath) || !File.Exists(selectedImagePath))
        {
            SafeLog("[Segment] Select an image first.");
            return;
        }

        StartCoroutine(SegmentCoroutine(selectedImagePath));
    }

    private IEnumerator SegmentCoroutine(string imagePath)
    {
        isProcessing = true;
        SafeLog($"[Segment] Sending: {imagePath}");

        string url = $"{serverBaseUrl.TrimEnd('/')}/segment";
        WWWForm form = new WWWForm();

        byte[] bytes = File.ReadAllBytes(imagePath);
        form.AddBinaryData("image", bytes, Path.GetFileName(imagePath), "image/png");

        using (UnityWebRequest www = UnityWebRequest.Post(url, form))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
                SafeLog($"[Segment] Error: {www.error}");
            else
                SafeLog("[Segment] Done ✔");
        }

        isProcessing = false;
    }

    // =========================================================
    // 3) GENERATE 3D
    // =========================================================
    public void OnGenerate3D()
    {
        if (isProcessing) return;

        if (string.IsNullOrEmpty(selectedImagePath) || !File.Exists(selectedImagePath))
        {
            SafeLog("[Generate3D] Select an image first.");
            return;
        }

        string fileName = Path.GetFileName(selectedImagePath);
        StartCoroutine(Generate3DCoroutine(fileName));
    }

    private IEnumerator Generate3DCoroutine(string fileName)
    {
        isProcessing = true;
        SafeLog($"[Generate3D] Starting for {fileName}");

        string url = $"{serverBaseUrl.TrimEnd('/')}/generate3d";
        var payload = new Generate3DRequest { image_path = fileName };
        byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));

        using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(body);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.timeout = 600;

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
                SafeLog($"[Generate3D] Error: {www.error}");
            else
                SafeLog("[Generate3D] Done ✔");
        }

        isProcessing = false;
    }

    // =========================================================
    // 4) LOAD GLB
    // =========================================================
    public void OnLoadGlb()
    {
#if UNITY_EDITOR
        if (!FrameGuard()) return;

        string path = EditorUtility.OpenFilePanel(
            "Select GLB model", "", "glb");

        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            SafeLog($"[LoadGLB] Selected: {path}");
            _ = LoadGlbAsync(path);
        }
        else
        {
            SafeLog("[LoadGLB] No file selected.");
        }
#else
        SafeLog("[LoadGLB] Editor only.");
#endif
    }

    // =========================================================
    // GLB LOADER (GLB_Model / ImportedModel)
    // =========================================================
    public async Task LoadGlbAsync(string glbPath)
    {
        try
        {
            GameObject glbRoot = GameObject.Find("GLB_Model");
            if (glbRoot == null)
            {
                SafeLog("[GLB] GLB_Model not found in Hierarchy.");
                return;
            }

            Transform container = glbRoot.transform.Find("ImportedModel");
            if (container == null)
            {
                GameObject go = new GameObject("ImportedModel");
                container = go.transform;
                container.SetParent(glbRoot.transform, false);
            }

            // Clear previous model ONLY
            for (int i = container.childCount - 1; i >= 0; i--)
                Destroy(container.GetChild(i).gameObject);

            byte[] data = await File.ReadAllBytesAsync(glbPath);

            var import = new GltfImport();
            if (!await import.Load(data))
            {
                SafeLog("[GLB] Load failed.");
                return;
            }

            if (!await import.InstantiateMainSceneAsync(container))
            {
                SafeLog("[GLB] Instantiate failed.");
                return;
            }

            if (container.childCount > 0)
            {
                Transform root = container.GetChild(0);
                root.localPosition = Vector3.zero;
                root.localRotation = Quaternion.identity;
                root.localScale = Vector3.one;
            }

            SafeLog("[GLB] Loaded ✔");
        }
        catch (Exception e)
        {
            SafeLog("[GLB] Error: " + e.Message);
        }
    }

    // =========================================================
    // UTIL
    // =========================================================
    private void SafeLog(string msg)
    {
        Debug.Log(msg);
        if (logText != null)
            logText.text = msg + "\n" + logText.text;
    }

    // =========================================================
    // DATA
    // =========================================================
    [Serializable]
    public class Generate3DRequest
    {
        public string image_path;
    }
}