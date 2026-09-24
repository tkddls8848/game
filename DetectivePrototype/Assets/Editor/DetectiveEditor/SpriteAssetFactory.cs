using System.IO;
using Detective.Art;
using UnityEditor;
using UnityEngine;

namespace DetectiveEditor
{
    /// <summary>
    /// 씬 빌더가 쓰는 스프라이트 에셋을 코드로 만든다.
    /// 런타임에 만든 Sprite는 씬에 직렬화되지 않아 저장하면 사라지므로,
    /// 실제 PNG 에셋을 디스크에 만들고 Sprite로 임포트한다(§18-9).
    /// 질감 자체는 런타임 코드(ProceduralTextures)가 그리므로 에디터와 게임이 같은 그림을 쓴다.
    /// </summary>
    public static class SpriteAssetFactory
    {
        public const string GeneratedFolder = "Assets/Art/Generated";
        public const string SquareSpritePath = GeneratedFolder + "/square_white.png";
        public const string DiscSpritePath = GeneratedFolder + "/disc.png";

        private const int TextureSize = 8;

        /// <summary>1 월드 유닛 크기의 흰색 정사각형 스프라이트. 색은 SpriteRenderer.color로 입힌다.</summary>
        public static Sprite GetOrCreateSquareSprite()
        {
            return GetOrCreate(SquareSpritePath, () =>
            {
                var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
                var pixels = new Color32[TextureSize * TextureSize];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
                texture.SetPixels32(pixels);
                texture.Apply();
                return texture;
            }, TextureSize, FilterMode.Point, TextureWrapMode.Clamp);
        }

        /// <summary>1 월드 유닛 지름의 원판(말/토큰). 색은 SpriteRenderer.color로 입힌다.</summary>
        public static Sprite GetOrCreateDiscSprite()
        {
            return GetOrCreate(DiscSpritePath, () => ProceduralTextures.Disc(128, 9f), 128, FilterMode.Bilinear, TextureWrapMode.Clamp);
        }

        /// <summary>
        /// 바닥 질감. 종류별로 한 번만 만든다. PPU는 "질감 한 장 = 1 월드 유닛"이 되도록 잡고,
        /// 실제 무늬 크기는 SpriteRenderer의 Tiled 모드 + 스케일로 정한다.
        /// </summary>
        public static Sprite GetOrCreateFloorSprite(string kind)
        {
            if (string.IsNullOrEmpty(kind)) kind = "wood";
            string path = GeneratedFolder + "/floor_" + kind + ".png";
            return GetOrCreate(path, () => ProceduralTextures.Floor(kind), ProceduralTextures.FloorSize, FilterMode.Bilinear, TextureWrapMode.Repeat);
        }

        private static Sprite GetOrCreate(string path, System.Func<Texture2D> make, int pixelsPerUnit, FilterMode filter, TextureWrapMode wrap)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            Directory.CreateDirectory(GeneratedFolder);

            Texture2D texture = make();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = pixelsPerUnit;
                importer.filterMode = filter;
                importer.wrapMode = wrap;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                // Tiled 모드로 그리려면 메시 타입이 Full Rect여야 한다.
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogError("[SpriteAssetFactory] " + path + " 를 Sprite로 임포트하지 못했다.");
            }
            return sprite;
        }
    }
}
