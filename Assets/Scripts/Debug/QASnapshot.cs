using System.IO;
using UnityEngine;

namespace Inspection.Debugging
{
    /// <summary>
    /// Captures the Main Camera + WorldSpace + temporarily-converted ScreenSpaceCamera UI to PNG.
    /// Synchronous: renders into a temp RT, reads pixels, encodes. ScreenSpaceOverlay canvases
    /// would normally not render through Camera.Render, so we briefly switch them into
    /// ScreenSpaceCamera mode for the capture, then restore.
    /// </summary>
    public static class QASnapshot
    {
        public static string CaptureMainCamera(string outputPath, int width = 1920, int height = 1080)
        {
            var cam = Camera.main;
            if (cam == null) { Debug.LogError("[QASnapshot] no Main Camera"); return null; }

            // Swap any ScreenSpaceOverlay canvases to ScreenSpaceCamera so they're rendered
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
            // force all canvas updates to apply the new mode
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

            // Restore canvas modes
            foreach (var (c, mode, prevCam) in swapped)
            {
                c.renderMode = mode;
                c.worldCamera = prevCam;
            }
            Canvas.ForceUpdateCanvases();

            var bytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(outputPath, bytes);
            Debug.Log("[QASnapshot] saved " + outputPath + " (" + bytes.Length + " bytes)");
            return outputPath;
        }
    }
}
