using System.IO;
using UnityEditor;
using UnityEngine;

namespace DetectiveEditor
{
    /// <summary>
    /// 씬 빌더가 쓰는 스프라이트 에셋을 코드로 만든다.
    /// 런타임에 만든 Sprite는 씬에 직렬화되지 않아 저장하면 사라지므로,
    /// 실제 PNG 에셋을 디스크에 만들고 Sprite로 임포트한다(§18-9).
    /// </summary>
    public static class SpriteAssetFactory
    {
        public const string GeneratedFolder = "Assets/Art/Generated";
        public const string SquareSpritePath = GeneratedFolder + "/square_white.png";

        private const int TextureSize = 8;

        /// <summary>1 월드 유닛 크기의 흰색 정사각형 스프라이트. 색은 SpriteRenderer.color로 입힌다.</summary>
        public static Sprite GetOrCreateSquareSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(SquareSpritePath);
            if (existing != null) return existing;

            Directory.CreateDirectory(GeneratedFolder);

            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
            var pixels = new Color32[TextureSize * TextureSize];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            texture.SetPixels32(pixels);
            texture.Apply();

            File.WriteAllBytes(SquareSpritePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var importer = AssetImporter.GetAtPath(SquareSpritePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                // 텍스처 8px / PPU 8 = 정확히 1 월드 유닛. Transform 스케일이 곧 방 크기가 된다.
                importer.spritePixelsPerUnit = TextureSize;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SquareSpritePath);
            if (sprite == null)
            {
                Debug.LogError("[SpriteAssetFactory] " + SquareSpritePath + " 를 Sprite로 임포트하지 못했다.");
            }
            return sprite;
        }
    }
}
