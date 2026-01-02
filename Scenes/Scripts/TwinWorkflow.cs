using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
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
    // TARGET IN HIERARCHY
    // =========================
    [Header("Target (already grabbable in scene)")]
    public Transform grabbableRoot;          // <- trascina qui il prefab/GO già grabbabile
    public bool fitBoxColliderToModel = true;

    // =========================
    // STATE
    // =========================
    private string selectedImagePath;
    private bool isProcessing;

    private Transform _loadedWorld;          // riferimento al "World" attualmente caricato (se presente)
    private GameObject _tempLoadRoot;        // contenitore temporaneo del loader

    // =========================================================
    // 1) BROWSE IMAGE  (Unity Button OnClick)
    // =========================================================
    public void OnBrowseImage()
    {
#if UNITY_EDITOR
        string selectedPath = EditorUtility.OpenFilePanel("Seleziona immagine", "", "png,jpg,jpeg");
        if (!string.IsNullOrEmpty(selectedPath) && File.Exists(selectedPath))
        {
            selectedImagePath = selectedPath;
            SafeLog($"[Browse] File selezionato: {selectedPath}");
        }
        else
        {
            SafeLog("[Browse] Nessun file selezionato.");
        }
#else
        SafeLog("[Browse] Disponibile solo in Editor.");
#endif
    }

    // =========================================================
    // 2) SEGMENT (SAM)  (Unity Button OnClick)
    // =========================================================
    public void OnSegment()
    {
        if (isProcessing) return;

        if (string.IsNullOrEmpty(selectedImagePath) || !File.Exists(selectedImagePath))
        {
            SafeLog("[Segment] Seleziona prima un'immagine valida.");
            return;
        }

        StartCoroutine(SegmentCoroutine(selectedImagePath));
    }

    private IEnumerator SegmentCoroutine(string localImagePath)
    {
        isProcessing = true;
        SafeLog($"[Segment] Invio: {localImagePath}");

        string url = $"{serverBaseUrl.TrimEnd('/')}/segment";
        WWWForm form = new WWWForm();

        byte[] bytes = File.ReadAllBytes(localImagePath);
        form.AddBinaryData("image", bytes, Path.GetFileName(localImagePath), "image/png");

        using (UnityWebRequest www = UnityWebRequest.Post(url, form))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
                SafeLog($"[Segment] Errore: {www.error}");
            else
                SafeLog($"[Segment] OK ✔  {www.downloadHandler.text}");
        }

        isProcessing = false;
    }

    // =========================================================
    // 3) GENERATE 3D  (Unity Button OnClick)
    // =========================================================
    public void OnGenerate3D()
    {
        if (isProcessing) return;

        if (string.IsNullOrEmpty(selectedImagePath) || !File.Exists(selectedImagePath))
        {
            SafeLog("[Generate3D] Seleziona prima un'immagine valida.");
            return;
        }

        // Come nel tuo vecchio codice: mando il SOLO nome file
        string fileName = Path.GetFileName(selectedImagePath);
        StartCoroutine(Generate3DCoroutine(fileName));
    }

    private IEnumerator Generate3DCoroutine(string fileName)
    {
        isProcessing = true;
        SafeLog($"[Generate3D] Avvio per {fileName}");

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
            {
                SafeLog($"[Generate3D] HTTP {www.responseCode} - {www.error}");
                isProcessing = false;
                yield break;
            }

            SafeLog($"[Generate3D] OK ✔  {www.downloadHandler.text}");
        }

        isProcessing = false;
    }

    // =========================================================
    // 4) LOAD GLB (local file picker)  (Unity Button OnClick)
    // =========================================================
    public void OnLoadGlb()
    {
#if UNITY_EDITOR
        string selectedPath = EditorUtility.OpenFilePanel("Seleziona modello GLB", "", "glb");
        if (!string.IsNullOrEmpty(selectedPath) && File.Exists(selectedPath))
        {
            SafeLog($"[LoadGLB] File selezionato: {selectedPath}");
            _ = LoadGlbAsync(selectedPath);
        }
        else
        {
            SafeLog("[LoadGLB] Nessun file selezionato.");
        }
#else
        SafeLog("[LoadGLB] Disponibile solo in Editor.");
#endif
    }

    // =========================================================
    // GLB LOADER: prende SOLO "World" e lo mette sotto grabbableRoot
    // =========================================================
    public async Task LoadGlbAsync(string glbPath)
    {
        try
        {
            Debug.Log($"[GLB] Loading: {glbPath}");

            // 1) Trova la cartella GLB_Model ESISTENTE
            GameObject folderGO = GameObject.Find("GLB_Model");
            if (folderGO == null)
            {
                Debug.LogError("[GLB] GameObject 'GLB_Model' not found in Hierarchy.");
                return;
            }

            Transform glbFolder = folderGO.transform;

            // 2) Trova (o crea) la sotto-cartella dedicata SOLO al modello importato
            //    Così NON cancelli eventuali altri figli/oggetti fissi dentro GLB_Model
            Transform modelContainer = glbFolder.Find("ImportedModel");
            if (modelContainer == null)
            {
                var containerGO = new GameObject("ImportedModel");
                modelContainer = containerGO.transform;
                modelContainer.SetParent(glbFolder, false);
                modelContainer.localPosition = Vector3.zero;
                modelContainer.localRotation = Quaternion.identity;
                modelContainer.localScale = Vector3.one;
            }

            // 3) Cancella SOLO il contenuto del container (modello precedente)
            for (int i = modelContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(modelContainer.GetChild(i).gameObject);
            }

            // 4) Carica il file GLB
            byte[] glbData = await File.ReadAllBytesAsync(glbPath);

            var import = new GltfImport();
            if (!await import.Load(glbData))
            {
                Debug.LogError("[GLB] Load failed");
                return;
            }

            // 5) Instanzia il modello DIRETTAMENTE dentro ImportedModel
            if (!await import.InstantiateMainSceneAsync(modelContainer))
            {
                Debug.LogError("[GLB] Instantiate failed");
                return;
            }

            // 6) Reset del root istanziato (di solito il primo figlio, spesso "World")
            if (modelContainer.childCount > 0)
            {
                Transform root = modelContainer.GetChild(0);
                root.localPosition = Vector3.zero;
                root.localRotation = Quaternion.identity;
                root.localScale = Vector3.one;
            }

            Debug.Log("[GLB] Model loaded inside GLB_Model/ImportedModel ✔");
        }
        catch (Exception e)
        {
            Debug.LogError("[GLB] Error: " + e.Message);
        }
    }





    // =========================================================
    // CLEANUP MODEL PRECEDENTE
    // =========================================================
    private void CleanupPreviousLoadedModel()
    {
        if (_loadedWorld != null)
        {
            Destroy(_loadedWorld.gameObject);
            _loadedWorld = null;
        }

        if (_tempLoadRoot != null)
        {
            Destroy(_tempLoadRoot);
            _tempLoadRoot = null;
        }
    }

    // =========================================================
    // FIT COLLIDER: ridimensiona BoxCollider del grabbable sul bounds del modello
    // =========================================================
    private void FitBoxColliderToLoadedModel(Transform grabbable, Transform modelRoot)
    {
        if (grabbable == null || modelRoot == null) return;

        BoxCollider box = grabbable.GetComponent<BoxCollider>();
        if (box == null)
        {
            SafeLog("[GLB] fitBoxCollider: BoxCollider not found on grabbableRoot (skip).");
            return;
        }

        Renderer[] renderers = modelRoot.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            SafeLog("[GLB] fitBoxCollider: No Renderer found on model (skip).");
            return;
        }

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);

        Vector3 localCenter = grabbable.InverseTransformPoint(b.center);

        box.center = localCenter;
        box.size = b.size;

        // centra il modello rispetto al collider
        modelRoot.localPosition -= localCenter;
    }

    private Transform FindGlbFolder()
    {
        // Se hai già un riferimento pubblico, usa quello.
        // Qui invece cerco per nome nella scena:
        GameObject go = GameObject.Find("GLB_Model");
        return go != null ? go.transform : null;
    }

    private void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }


    // =========================================================
    // UTILITIES
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
    public class Generate3DRequest { public string image_path; }
}
