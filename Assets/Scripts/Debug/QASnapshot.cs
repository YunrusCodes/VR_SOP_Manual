using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Inspection.Debugging
{
    /// <summary>
    /// Captures the rendered game view to PNG. In URP, Camera.Render() does not reliably render
    /// WorldSpace UI (it bypasses URP's volume + UI render pass), so we capture the actual
    /// rendered game view via ScreenCapture which round-trips through the active render pipeline.
    ///
    /// For ScreenSpaceOverlay canvases the legacy code path used Camera.Render with a swapped-mode
    /// canvas; we keep that as a fallback for non-coroutine call sites that don't need URP support.
    /// </summary>
    public static class QASnapshot
    {
        /// <summary>
        /// Synchronous capture (non-coroutine). Works for ScreenSpaceOverlay UI by temporarily
        /// converting Overlay canvases to ScreenSpaceCamera. Does NOT reliably capture WorldSpace
        /// UI under URP — use <see cref="CaptureCoroutine"/> for that.
        /// </summary>
        public static string CaptureMainCamera(string outputPath, int width = 1920, int height = 1080)
        {
            var cam = Camera.main;
            if (cam == null) { Debug.LogError("[QASnapshot] no Main Camera"); return null; }

            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var swapped = new System.Collections.Generic.List<(Canvas c, RenderMode prevMode, Camera prevCam)>();
            foreach (var c in canvases)
            {
                if (!c.isRootCanvas) continue;
                if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    swapped.Add((c, c.renderMode, c.worldCamera));
                    c.renderMode = RenderMode.ScreenSpaceCamera;
                    c.worldCamera = cam;
                    c.planeDistance = 1f;
                }
            }
            Canvas.ForceUpdateCanvases();

            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var prevTarget = cam.targetTexture;
            var prevRT = RenderTexture.active;

            cam.targetTexture = rt;
            RenderTexture.active = rt;
            cam.Render();

            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            cam.targetTexture = prevTarget;
            RenderTexture.active = prevRT;
            RenderTexture.ReleaseTemporary(rt);

            foreach (var (c, mode, prevCam) in swapped)
            {
                c.renderMode = mode;
                c.worldCamera = prevCam;
            }
            Canvas.ForceUpdateCanvases();

            return Save(tex, outputPath);
        }

        /// <summary>
        /// URP-friendly capture using <c>RenderPipeline.SubmitRenderRequest</c>. Camera.Render()
        /// does not reliably render WorldSpace UI in URP, so we explicitly drive the URP render
        /// pipeline at our chosen RenderTexture target.
        /// </summary>
        public static IEnumerator CaptureCoroutine(string outputPath, int width = 1920, int height = 1080)
        {
            var cam = Camera.main;
            if (cam == null) { Debug.LogError("[QASnapshot] no Main Camera"); yield break; }

            // Wait for the next end-of-frame so any per-frame UI / Canvas updates settle.
            yield return new WaitForEndOfFrame();

            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);

            var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, req))
            {
                RenderPipeline.SubmitRenderRequest(cam, req);
            }
            else
            {
                Debug.LogWarning("[QASnapshot] URP SingleCameraRequest unsupported, falling back to cam.Render()");
                var prev = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = prev;
            }

            var prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);

            Save(tex, outputPath);
            Object.Destroy(tex);
        }

        static string Save(Texture2D tex, string outputPath)
        {
            var bytes = tex.EncodeToPNG();
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(outputPath, bytes);
            Debug.Log("[QASnapshot] saved " + outputPath + " (" + bytes.Length + " bytes)");
            // Note: caller owns the texture for the static path
            return outputPath;
        }
    }
}
