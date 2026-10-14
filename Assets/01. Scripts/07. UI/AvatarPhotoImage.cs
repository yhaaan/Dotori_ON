using DOTORION.Core;
using UnityEngine;

namespace DOTORION.UI
{
    /// <summary>
    /// Turns a picture file into the small square PNG a profile photo is stored
    /// as. Everything here runs on the GPU through <see cref="Graphics.Blit"/>:
    /// a phone photo is twelve megapixels, and scaling that on the CPU would hang
    /// the overlay for a noticeable moment.
    /// </summary>
    public static class AvatarPhotoImage
    {
        /// <summary>
        /// The longest side the crop editor works with. It is far more than a
        /// 64 pixel result needs, and keeps a huge photo from costing a huge
        /// texture for as long as the editor is open.
        /// </summary>
        public const int EditingMaximumSide = 1024;

        /// <summary>
        /// Decodes a PNG or JPEG and shrinks it to <see cref="EditingMaximumSide"/>.
        /// Returns null for anything Unity cannot decode.
        /// </summary>
        public static Texture2D LoadForEditing(byte[] fileBytes)
        {
            if (fileBytes == null || fileBytes.Length == 0)
            {
                return null;
            }

            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!decoded.LoadImage(fileBytes) || decoded.width < 1 || decoded.height < 1)
            {
                Object.Destroy(decoded);
                return null;
            }

            var longest = Mathf.Max(decoded.width, decoded.height);
            if (longest <= EditingMaximumSide)
            {
                Prepare(decoded);
                return decoded;
            }

            var scale = (float)EditingMaximumSide / longest;
            var width = Mathf.Max(1, Mathf.RoundToInt(decoded.width * scale));
            var height = Mathf.Max(1, Mathf.RoundToInt(decoded.height * scale));
            var result = Resample(decoded, new Rect(0f, 0f, 1f, 1f), width, height);
            Object.Destroy(decoded);
            return result;
        }

        /// <summary>
        /// A quarter turn clockwise. JPEGs from phones often rely on an EXIF flag
        /// for their orientation, which Unity's decoder ignores, so the person
        /// needs a way to stand the picture up again.
        /// </summary>
        public static Texture2D RotateClockwise(Texture2D source)
        {
            var width = source.width;
            var height = source.height;
            var pixels = source.GetPixels32();
            var rotated = new Color32[pixels.Length];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    // Texture rows run bottom to top, so clockwise sends (x, y)
                    // to (y, width - 1 - x) in the height-by-width result.
                    rotated[(width - 1 - x) * height + y] = pixels[y * width + x];
                }
            }

            var result = new Texture2D(height, width, TextureFormat.RGBA32, false);
            result.SetPixels32(rotated);
            result.Apply();
            Prepare(result);
            return result;
        }

        /// <summary>
        /// The part of <paramref name="source"/> under <paramref name="uv"/>,
        /// scaled to <see cref="AvatarPhotoKey.Size"/> square and encoded as PNG.
        /// </summary>
        public static byte[] CropToPng(Texture2D source, Rect uv)
        {
            var size = AvatarPhotoKey.Size;
            var result = Resample(source, uv, size, size);
            var png = result.EncodeToPNG();
            Object.Destroy(result);
            return png;
        }

        /// <summary>
        /// Scales by halves first and only then to the exact size. A bilinear
        /// blit reads four texels per output pixel, so one big step from 1024 to
        /// 64 would skip most of the picture and shimmer; halving averages all
        /// of them.
        /// </summary>
        private static Texture2D Resample(Texture2D source, Rect uv, int width, int height)
        {
            var previousFilter = source.filterMode;
            source.filterMode = FilterMode.Bilinear;
            var currentWidth = Mathf.Max(1, Mathf.RoundToInt(source.width * uv.width));
            var currentHeight = Mathf.Max(1, Mathf.RoundToInt(source.height * uv.height));
            var current = RenderTexture.GetTemporary(currentWidth, currentHeight, 0, RenderTextureFormat.ARGB32);
            current.filterMode = FilterMode.Bilinear;
            Graphics.Blit(source, current, uv.size, uv.position);
            source.filterMode = previousFilter;

            while (currentWidth >= width * 2 && currentHeight >= height * 2)
            {
                currentWidth /= 2;
                currentHeight /= 2;
                var half = RenderTexture.GetTemporary(currentWidth, currentHeight, 0, RenderTextureFormat.ARGB32);
                half.filterMode = FilterMode.Bilinear;
                Graphics.Blit(current, half);
                RenderTexture.ReleaseTemporary(current);
                current = half;
            }

            var target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(current, target);
            RenderTexture.ReleaseTemporary(current);

            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var result = new Texture2D(width, height, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            result.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            Prepare(result);
            return result;
        }

        private static void Prepare(Texture2D texture)
        {
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
        }
    }
}
