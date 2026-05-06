using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Inspection.Net
{
    public static class ImageLoader
    {
        public static async Task<Texture2D> LoadAsync(string url, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(url)) throw new ArgumentException("url required", nameof(url));
            using var req = UnityWebRequestTexture.GetTexture(url);
            var op = req.SendWebRequest();
            while (!op.isDone)
            {
                if (ct.IsCancellationRequested)
                {
                    req.Abort();
                    ct.ThrowIfCancellationRequested();
                }
                await Task.Yield();
            }
            if (req.result != UnityWebRequest.Result.Success)
                throw new ApiException(url, (int)req.responseCode, req.error ?? "image load failed");
            return DownloadHandlerTexture.GetContent(req);
        }
    }
}
