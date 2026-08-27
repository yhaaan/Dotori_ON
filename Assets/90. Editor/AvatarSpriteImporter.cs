using System;
using UnityEditor;
using UnityEngine;

namespace TeamOverlay.Editor
{
    /// <summary>
    /// Imports everything in the avatar folder as a UI sprite, so dropping a PNG
    /// in is the whole job. Without this the first icon a project adds silently
    /// comes in as a plain texture and never shows up in the picker, which looks
    /// like a broken feature rather than an import setting.
    ///
    /// Smooth artwork and pixel art want opposite settings, and there is no
    /// reading one from the file, so the folder decides: anything under
    /// <c>Pixel/</c> is left hard-edged and everything else is filtered.
    /// </summary>
    internal sealed class AvatarSpriteImporter : AssetPostprocessor
    {
        /// <summary>Icons in here keep their pixel grid instead of being smoothed.</summary>
        internal const string PixelArtSubfolder = "Pixel";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(TeamOverlayPrefabBuilder.AvatarSpriteFolder + "/", StringComparison.Ordinal))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;

            // A handful of small icons, so the artwork is worth more than the few
            // hundred kilobytes block compression would save - and DXT on a hard
            // edged icon is exactly where the blocks show.
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var isPixelArt = assetPath.Contains(
                "/" + PixelArtSubfolder + "/",
                StringComparison.Ordinal);
            if (isPixelArt)
            {
                // Bilinear would blend a 32x32 drawing into mush at the size it is
                // shown, and a mip map is a pre-blurred copy of the same mistake.
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                return;
            }

            importer.filterMode = FilterMode.Bilinear;

            // Smooth artwork is authored several times larger than the 32px it is
            // drawn at. Without mip maps that minification aliases into sparkle on
            // every edge.
            importer.mipmapEnabled = true;
        }
    }
}
