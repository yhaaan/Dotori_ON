using DOTORION.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DOTORION.Events.Subjugation
{
    /// <summary>Cache real Korean UI font glyphs for the independent native pixel surface.</summary>
    public static class SubjugationText
    {
        public static SubjugationFrame Rasterize(Font font, string value, int width, int height, int size, FontStyle style = FontStyle.Normal)
        {
            var host = new GameObject("Event text cache", typeof(RectTransform), typeof(Canvas));
            var cameraHost = new GameObject("Event text camera", typeof(Camera));
            var rt = RenderTexture.GetTemporary(width * 2, height * 2, 16, RenderTextureFormat.ARGB32);
            var old = RenderTexture.active;
            Texture2D readable = null;
            try
            {
                host.layer = 31; host.transform.position = new Vector3(10000, 10000, 0);
                var camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false; camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
                camera.targetTexture = rt; camera.transform.position = host.transform.position - Vector3.forward * 10;
                var canvas = host.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                var label = UiFactory.CreateText("Text", host.transform, font, size * 2, TextAnchor.MiddleCenter, Color.white, style);
                label.gameObject.layer = 31; label.text = value; UiFactory.Stretch(label.rectTransform);
                Canvas.ForceUpdateCanvases(); camera.Render();
                RenderTexture.active = rt;
                readable = new Texture2D(width * 2, height * 2, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); readable.Apply();
                return new SubjugationFrame(readable.GetPixels32(), rt.width, rt.height, rt.width, rt.width);
            }
            finally
            {
                RenderTexture.active = old; RenderTexture.ReleaseTemporary(rt);
                SubjugationPixels.Release(readable); SubjugationPixels.Release(host); SubjugationPixels.Release(cameraHost);
            }
        }
    }
}
