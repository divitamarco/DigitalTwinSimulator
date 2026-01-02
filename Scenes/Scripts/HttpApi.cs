using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public static class HttpApi
{
    // Cambia se il tuo Flask gira altrove
    public static string BaseUrl = "http://127.0.0.1:5000";

    // POST /segment (multipart form-data)
    public static async Task<string> PostSegment(byte[] imageBytes, string filename)
    {
        var url = $"{BaseUrl}/segment";
        var form = new WWWForm();
        form.AddBinaryData("image", imageBytes, filename, "image/png");

        using var req = UnityWebRequest.Post(url, form);
        req.downloadHandler = new DownloadHandlerBuffer();

#if UNITY_2022_3_OR_NEWER
        await req.SendWebRequest();
#else
        var op = req.SendWebRequest();
        while (!op.isDone) await Task.Yield();
#endif

        if (req.result != UnityWebRequest.Result.Success)
            throw new System.Exception($"Segment error: {req.error}\n{req.downloadHandler.text}");

        return req.downloadHandler.text; // JSON
    }

    // POST /generate3d (JSON { "image_path": "<nome file in test_images/>" })
    public static async Task<string> PostGenerate3D(string imageFileNameInTestImages)
    {
        var url = $"{BaseUrl}/generate3d";
        var payload = $"{{\"image_path\":\"{imageFileNameInTestImages}\"}}";
        var bodyRaw = Encoding.UTF8.GetBytes(payload);

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

#if UNITY_2022_3_OR_NEWER
        await req.SendWebRequest();
#else
        var op = req.SendWebRequest();
        while (!op.isDone) await Task.Yield();
#endif

        if (req.result != UnityWebRequest.Result.Success)
            throw new System.Exception($"Generate3D error: {req.error}\n{req.downloadHandler.text}");

        return req.downloadHandler.text; // JSON { "model_path": "/static/.../something.glb" }
    }

    // GET raw bytes (per scaricare .glb)
    public static async Task<byte[]> GetBytes(string url)
    {
        using var req = UnityWebRequest.Get(url);
        req.downloadHandler = new DownloadHandlerBuffer();

#if UNITY_2022_3_OR_NEWER
        await req.SendWebRequest();
#else
        var op = req.SendWebRequest();
        while (!op.isDone) await Task.Yield();
#endif

        if (req.result != UnityWebRequest.Result.Success)
            throw new System.Exception($"GET {url} error: {req.error}");

        return req.downloadHandler.data;
    }
}
