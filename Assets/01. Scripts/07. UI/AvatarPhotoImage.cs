using DOTORION.Core;
using UnityEngine;

namespace DOTORION.UI
{
    /// <summary>
    /// Turns a picture file into the small square PNG a profile photo is stored
    /// as.
    ///
    /// Two steps, each chosen so it does not soften the picture. Loading only
    /// ever halves, on the GPU, until the picture fits the editor: a halving is
    /// an exact 2x2 average, while a blit to any other size reads four texels
    /// per pixel, skips the rest, and blurs whatever it lands between. The final
    /// crop is then averaged on the CPU by area - every source pixel under an
    /// output pixel counts in proportion to how much of it is covered - which is
    /// how a photo editor shrinks a picture well. That step reads at most a
    /// couple of million pixels once, on 적용, so it costs a blink.
    /// </summary>
    public static class AvatarPhotoImage
    {
        /// <summary>
        /// The longest side the crop editor works with. Pictures larger than this
        /// are halved until they fit, so the editing copy ends up between half
        /// this and this - plenty for a 96 pixel result, and it keeps a twelve
        /// megapixel photo from sitting in memory while the editor is open.
        /// </summary>
        public const int EditingMaximumSide = 2048;

        /// <summary>
        /// Decodes a PNG or JPEG and halves it until it fits
        /// <see cref="EditingMaximumSide"/>. Returns null for anything Unity
        /// cannot decode.
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

            if (Mathf.Max(decoded.width, decoded.height) <= EditingMaximumSide)
            {
                Prepare(decoded);
                return decoded;
            }

            var width = decoded.width;
            var height = decoded.height;
            decoded.filterMode = FilterMode.Bilinear;
            var current = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(decoded, current);
            Object.Destroy(decoded);
            while (Mathf.Max(width, height) > EditingMaximumSide)
            {
                width = Mathf.Max(1, width / 2);
                height = Mathf.Max(1, height / 2);
                var half = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
                current.filterMode = FilterMode.Bilinear;
                Graphics.Blit(current, half);
                RenderTexture.ReleaseTemporary(current);
                current = half;
            }

            var previous = RenderTexture.active;
            RenderTexture.active = current;
            var result = new Texture2D(width, height, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            result.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(current);
            Prepare(result);
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
        /// area-averaged to <see cref="AvatarPhotoKey.Size"/> square and encoded
        /// as PNG.
        /// </summary>
        public static byte[] CropToPng(Texture2D source, Rect uv)
        {
            var size = AvatarPhotoKey.Size;
            var pixels = AreaResample(
                source.GetPixels32(),
                source.width,
                source.height,
                uv.x * source.width,
                uv.y * source.height,
                uv.width * source.width,
                uv.height * source.height,
                size);
            var result = new Texture2D(size, size, TextureFormat.RGBA32, false);
            result.SetPixels32(pixels);
            result.Apply();
            var png = result.EncodeToPNG();
            Object.Destroy(result);
            return png;
        }

        /// <summary>
        /// Shrinks the rectangle (<paramref name="left"/>, <paramref name="bottom"/>,
        /// <paramref name="spanX"/> x <paramref name="spanY"/>, in texels) to
        /// <paramref name="size"/> square by area: one pass across, one pass down.
        ///
        /// Averaging happens in linear light. sRGB values are not proportional to
        /// brightness, so averaging them directly darkens fine bright detail -
        /// hair against a window, text on a shirt - and that is exactly the kind
        /// of loss that makes a small photo look duller than the original. Colour
        /// is also weighted by alpha, so transparent pixels do not bleed their
        /// (meaningless) colour into the edge of a cut-out.
        /// </summary>
        private static Color32[] AreaResample(
            Color32[] source,
            int sourceWidth,
            int sourceHeight,
            float left,
            float bottom,
            float spanX,
            float spanY,
            int size)
        {
            var toLinear = new float[256];
            for (var index = 0; index < 256; index++)
            {
                var value = index / 255f;
                toLinear[index] = value <= 0.04045f ? value / 12.92f : Mathf.Pow((value + 0.055f) / 1.055f, 2.4f);
            }

            var firstRow = Mathf.Clamp(Mathf.FloorToInt(bottom), 0, sourceHeight - 1);
            var lastRow = Mathf.Clamp(Mathf.CeilToInt(bottom + spanY) - 1, firstRow, sourceHeight - 1);
            var rows = lastRow - firstRow + 1;

            // Across: every source row in range becomes `size` premultiplied
            // linear samples (r, g, b, a).
            var across = new float[rows * size * 4];
            var stepX = spanX / size;
            for (var row = 0; row < rows; row++)
            {
                var sourceRow = (firstRow + row) * sourceWidth;
                for (var column = 0; column < size; column++)
                {
                    var from = left + column * stepX;
                    var to = from + stepX;
                    float r = 0f, g = 0f, b = 0f, a = 0f, weight = 0f;
                    for (var x = Mathf.FloorToInt(from); x < to; x++)
                    {
                        var coverage = Mathf.Min(to, x + 1) - Mathf.Max(from, x);
                        if (coverage <= 0f)
                        {
                            continue;
                        }

                        var pixel = source[sourceRow + Mathf.Clamp(x, 0, sourceWidth - 1)];
                        var alpha = pixel.a / 255f * coverage;
                        r += toLinear[pixel.r] * alpha;
                        g += toLinear[pixel.g] * alpha;
                        b += toLinear[pixel.b] * alpha;
                        a += alpha;
                        weight += coverage;
                    }

                    var at = (row * size + column) * 4;
                    var scale = weight > 0f ? 1f / weight : 0f;
                    across[at] = r * scale;
                    across[at + 1] = g * scale;
                    across[at + 2] = b * scale;
                    across[at + 3] = a * scale;
                }
            }

            // Down: the same, over the rows, straight into the output.
            var result = new Color32[size * size];
            var stepY = spanY / size;
            for (var outputRow = 0; outputRow < size; outputRow++)
            {
                var from = bottom + outputRow * stepY;
                var to = from + stepY;
                for (var column = 0; column < size; column++)
                {
                    float r = 0f, g = 0f, b = 0f, a = 0f, weight = 0f;
                    for (var y = Mathf.FloorToInt(from); y < to; y++)
                    {
                        var coverage = Mathf.Min(to, y + 1) - Mathf.Max(from, y);
                        if (coverage <= 0f)
                        {
                            continue;
                        }

                        var row = Mathf.Clamp(y, firstRow, lastRow) - firstRow;
                        var at = (row * size + column) * 4;
                        r += across[at] * coverage;
                        g += across[at + 1] * coverage;
                        b += across[at + 2] * coverage;
                        a += across[at + 3] * coverage;
                        weight += coverage;
                    }

                    if (weight <= 0f || a <= 0f)
                    {
                        result[outputRow * size + column] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    result[outputRow * size + column] = new Color32(
                        ToSrgb(r / a),
                        ToSrgb(g / a),
                        ToSrgb(b / a),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(a / weight * 255f), 0, 255));
                }
            }

            return result;
        }

        private static byte ToSrgb(float linear)
        {
            linear = Mathf.Clamp01(linear);
            var value = linear <= 0.0031308f ? linear * 12.92f : 1.055f * Mathf.Pow(linear, 1f / 2.4f) - 0.055f;
            return (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255f), 0, 255);
        }

        private static void Prepare(Texture2D texture)
        {
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
        }
    }
}
