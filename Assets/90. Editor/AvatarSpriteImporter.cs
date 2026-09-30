using System;
using UnityEditor;
using UnityEngine;

namespace DOTORION.Editor
{
    /// <summary>
    /// 아바타 폴더 안의 모든 이미지를 UI 스프라이트로 가져옵니다. PNG를 넣는 것만으로
    /// 끝나게 하려는 것입니다. 이게 없으면 프로젝트에 처음 넣은 아이콘이 조용히 일반
    /// 텍스처로 들어와 선택창에 나타나지 않고, 임포트 설정 문제가 아니라 기능이 고장
    /// 난 것처럼 보입니다.
    ///
    /// 부드러운 그림과 픽셀아트는 서로 반대 설정을 원하는데 파일만 보고는 구분할 수
    /// 없어서 폴더로 정합니다. <c>Pixel/</c> 아래는 가장자리를 그대로 두고, 나머지는
    /// 필터를 겁니다.
    /// </summary>
    internal sealed class AvatarSpriteImporter : AssetPostprocessor
    {
        /// <summary>이 안의 아이콘은 뭉개지 않고 픽셀 격자를 그대로 유지합니다.</summary>
        internal const string PixelArtSubfolder = "Pixel";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(DOTORIONPrefabBuilder.AvatarSpriteFolder + "/", StringComparison.Ordinal))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;

            // 작은 아이콘 몇 장이라 블록 압축으로 아끼는 수백 KB보다 그림이 더 중요합니다.
            // 가장자리가 딱딱한 아이콘에 DXT를 쓰면 블록이 가장 잘 드러납니다.
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var isPixelArt = assetPath.Contains(
                "/" + PixelArtSubfolder + "/",
                StringComparison.Ordinal);
            if (isPixelArt)
            {
                // Bilinear를 쓰면 실제로 그려지는 크기에서 32x32 그림이 뭉개지고, 밉맵은 같은
                // 실수를 미리 흐려 둔 사본일 뿐입니다.
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                return;
            }

            importer.filterMode = FilterMode.Bilinear;

            // 부드러운 그림은 그려지는 32px보다 몇 배 크게 그립니다. 밉맵이 없으면 축소할 때
            // 모든 가장자리가 반짝거립니다.
            importer.mipmapEnabled = true;
        }
    }
}
