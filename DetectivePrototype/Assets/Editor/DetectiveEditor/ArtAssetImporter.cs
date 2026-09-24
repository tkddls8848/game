using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DetectiveEditor
{
    /// <summary>
    /// Assets/Resources 아래 연출 에셋(바닥 질감·초상화·단서 그림·소리)의 임포트 설정을 코드로 맞춘다.
    ///
    /// 손으로 맞추면 반드시 빠뜨리는 것이 둘 있다(§18-2: 손으로만 만들 수 있는 에셋을 만들지 않는다).
    ///  * 바닥 질감: Wrap Mode=Repeat + Mesh Type=Full Rect 가 아니면 SpriteRenderer의 Tiled 모드가 한 장만 늘린다.
    ///  * 바닥 질감의 PPU: "질감 한 장 = 1 월드 유닛"이어야 art.json의 tileSize가 무늬 크기가 된다
    ///    (SpriteAssetFactory가 만드는 생성 질감과 같은 약속).
    ///
    /// 처음 임포트될 때(.meta가 아직 없을 때)만 손대므로, 그 뒤 에디터에서 사람이 고친 값은 그대로 둔다.
    /// 다만 프로젝트를 처음 열 때는 이 스크립트보다 에셋이 먼저 임포트될 수 있어, 에디터가 뜰 때 한 번 더 훑어 고친다(FixOnLoad).
    /// 전부 다시 맞추려면 Tools/Detective/Reimport Art Assets.
    /// </summary>
    public class ArtAssetImporter : AssetPostprocessor
    {
        public const string FloorsFolder = "Assets/Resources/Art/Floors";
        public const string PortraitsFolder = "Assets/Resources/Art/Portraits";
        public const string EvidenceFolder = "Assets/Resources/Art/Evidence";
        public const string BgmFolder = "Assets/Resources/Audio/BGM";
        public const string AmbientFolder = "Assets/Resources/Audio/Ambient";
        public const string SfxFolder = "Assets/Resources/Audio/SFX";

        /// <summary>바닥 질감 한 변의 픽셀 수. PPU와 같아야 질감 한 장이 1 월드 유닛이 된다.</summary>
        public const int FloorTextureSize = 512;

        /// <summary>UI 액자에 걸리는 그림(초상화 264px, 단서 260px) 크기. 그보다 크게 들어와도 여기서 줄인다.</summary>
        public const int PictureTextureSize = 256;

        /// <summary>전체 사각형 스프라이트의 꼭짓점 수. 이보다 많으면 외곽이 잘린 메시다.</summary>
        private const int FullRectVertexCount = 4;

        // Mesh Type(Full Rect) 이야기 — Unity 6000.0.81f1 에서 실측한 것.
        //
        // Tiled 로 깔리려면 스프라이트 메시가 전체 사각형이어야 한다고 알려져 있는데, 이 값은 스크립트로 바꿀 수 없다.
        //   * TextureImporterSettings.spriteMeshType 에 FullRect 를 넣고 SetTextureSettings 를 불러도
        //     .meta 에는 0(Tight)으로 남는다. 게다가 ReadTextureSettings 는 그 뒤로도 FullRect 를 돌려주므로
        //     읽기로 확인하면 멀쩡해 보인다.
        //   * SerializedObject 로 m_SpriteMeshType 에 직접 써도 ApplyModifiedPropertiesWithoutUndo 가
        //     false 를 돌려주고 값이 그대로다(임포터 프로퍼티 쓰기가 거부된다).
        //
        // 다행히 값을 강제할 필요가 없다. Tight 는 불투명한 부분의 외곽을 딴 것이라,
        // 투명 영역이 없는 그림은 Tight 여도 꼭짓점 4개짜리 사각형이 그대로 나온다(실측 확인).
        // 그래서 설정을 밀어 넣는 대신 결과를 검사한다 — OnPostprocessSprites 참고.

        // ----- 자동 적용(첫 임포트) ---------------------------------------------

        private void OnPreprocessTexture()
        {
            var importer = assetImporter as TextureImporter;
            if (importer == null || !importer.importSettingsMissing) return;
            ApplyTextureSettings(importer);
        }

        private void OnPreprocessAudio()
        {
            var importer = assetImporter as AudioImporter;
            if (importer == null || !importer.importSettingsMissing) return;
            ApplyAudioSettings(importer);
        }

        /// <summary>바닥 질감이 PPU와 다른 크기로 들어오면 무늬 크기가 tileSize와 어긋난다. 그때만 알린다.</summary>
        private void OnPostprocessTexture(Texture2D texture)
        {
            if (!InFolder(assetPath, FloorsFolder) || texture == null) return;
            if (texture.width == FloorTextureSize && texture.height == FloorTextureSize) return;
            Debug.LogWarning("[ArtAssetImporter] " + assetPath + " 는 " + texture.width + "x" + texture.height
                + " 다. 바닥 질감은 " + FloorTextureSize + "x" + FloorTextureSize + " 정사각형이어야 art.json의 tileSize가 무늬 크기가 된다"
                + " (Tools/Detective/Reimport Art Assets 를 돌리면 실제 크기에 맞춰 PPU를 다시 잡는다).");
        }

        /// <summary>
        /// 바닥은 Tiled 로 깔리므로 스프라이트 메시가 전체 사각형이어야 한다.
        /// Mesh Type 은 스크립트로 강제할 수 없으니(위 주석) 만들어진 결과를 검사한다.
        /// 불투명한 그림은 Tight 여도 꼭짓점 4개짜리 사각형이라 문제가 없고, 투명 영역이 있으면 잘려 나간다.
        /// </summary>
        private void OnPostprocessSprites(Texture2D texture, Sprite[] sprites)
        {
            if (!InFolder(assetPath, FloorsFolder) || sprites == null) return;
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] == null || sprites[i].vertices.Length == FullRectVertexCount) continue;
                Debug.LogWarning("[ArtAssetImporter] " + assetPath + ": 스프라이트 메시의 꼭짓점이 "
                    + sprites[i].vertices.Length + "개다(전체 사각형이 아니다). 바닥은 Tiled 로 깔리므로 잘린 채 반복된다."
                    + " 인스펙터에서 Mesh Type을 Full Rect로 바꿀 것 — 투명한 부분이 있는 그림은 Tight로 외곽이 잘린다.");
            }
        }

        // ----- 다시 맞추기(메뉴 · 자동 복구) ---------------------------------------

        /// <summary>
        /// 프로젝트를 처음 열 때는 이 스크립트가 컴파일되기 전에 에셋이 임포트될 수 있고,
        /// 그러면 바닥이 기본값(Wrap=Clamp)으로 들어와 Tiled가 한 장만 늘린다. 시작할 때 한 번 훑어 고친다.
        /// 이미 맞는 파일은 건드리지 않으므로 다시 임포트가 반복되지 않는다.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void FixOnLoad()
        {
            // 임포트가 끝난 뒤로 미룬다(로드 도중 SaveAndReimport를 부르지 않는다).
            EditorApplication.delayCall += () =>
            {
                int fixedCount = 0;
                foreach (string path in FindAssets("t:Texture2D", FloorsFolder, PortraitsFolder, EvidenceFolder))
                {
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null || !NeedsFixing(importer)) continue;
                    if (!ApplyTextureSettings(importer)) continue;
                    importer.SaveAndReimport();
                    fixedCount++;
                }
                foreach (string path in FindAssets("t:AudioClip", BgmFolder, AmbientFolder, SfxFolder))
                {
                    var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                    if (importer == null || !NeedsFixing(importer)) continue;
                    if (!ApplyAudioSettings(importer)) continue;
                    importer.SaveAndReimport();
                    fixedCount++;
                }
                if (fixedCount > 0)
                    Debug.Log("[ArtAssetImporter] 기본값으로 들어와 있던 연출 에셋 " + fixedCount + "개의 임포트 설정을 고쳤다.");
            };
        }

        /// <summary>확실히 저장되는 값만 본다. 여기서 false면 손대지 않아 무한 재임포트가 없다.</summary>
        private static bool NeedsFixing(TextureImporter importer)
        {
            if (importer.textureType != TextureImporterType.Sprite) return true;
            if (InFolder(importer.assetPath, FloorsFolder) && importer.wrapMode != TextureWrapMode.Repeat) return true;
            return false;
        }

        private static bool NeedsFixing(AudioImporter importer)
        {
            bool music = InFolder(importer.assetPath, BgmFolder) || InFolder(importer.assetPath, AmbientFolder);
            AudioClipLoadType wanted = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            return importer.defaultSampleSettings.loadType != wanted || !importer.forceToMono;
        }

        [MenuItem("Tools/Detective/Reimport Art Assets")]
        public static void ReimportFromMenu()
        {
            int touched = 0;
            foreach (string path in FindAssets("t:Texture2D", FloorsFolder, PortraitsFolder, EvidenceFolder))
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null || !ApplyTextureSettings(importer)) continue;

                // 이미 임포트된 그림이라 실제 크기를 알 수 있다. 바닥은 그 크기에 PPU를 맞춘다.
                if (InFolder(path, FloorsFolder))
                {
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (texture != null && texture.width > 0) importer.spritePixelsPerUnit = texture.width;
                }
                importer.SaveAndReimport();
                touched++;
            }

            foreach (string path in FindAssets("t:AudioClip", BgmFolder, AmbientFolder, SfxFolder))
            {
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null || !ApplyAudioSettings(importer)) continue;
                importer.SaveAndReimport();
                touched++;
            }

            Debug.Log("[ArtAssetImporter] 연출 에셋 " + touched + "개의 임포트 설정을 다시 맞췄다.");
        }

        // ----- 설정 -------------------------------------------------------------

        /// <summary>연출 폴더의 그림이면 설정을 맞추고 true. 그 밖의 그림은 건드리지 않는다.</summary>
        public static bool ApplyTextureSettings(TextureImporter importer)
        {
            string path = importer.assetPath;
            bool floor = InFolder(path, FloorsFolder);
            bool picture = InFolder(path, PortraitsFolder) || InFolder(path, EvidenceFolder);
            if (!floor && !picture) return false;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;

            if (floor)
            {
                // 질감 한 장 = 1 월드 유닛. 실제 무늬 크기는 art.json의 tileSize가 정한다.
                importer.spritePixelsPerUnit = FloorTextureSize;
                importer.maxTextureSize = FloorTextureSize;
                importer.wrapMode = TextureWrapMode.Repeat; // 이게 없으면 Tiled가 한 장만 늘린다
                importer.mipmapEnabled = true;              // 바닥은 카메라에서 멀어지면 줄어든다
            }
            else
            {
                importer.spritePixelsPerUnit = 100f;        // UI 액자에 preserveAspect로 들어가므로 값 자체는 무관
                importer.maxTextureSize = PictureTextureSize;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;             // UI라 축소가 없다
            }

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteExtrude = 0;
            importer.SetTextureSettings(settings);

            // spriteMeshType 은 여기서 넣어도 Unity 6 에서 .meta 에 저장되지 않는다.
            // 대신 만들어진 스프라이트가 전체 사각형인지 OnPostprocessSprites 에서 검사한다.
            return true;
        }

        /// <summary>연출 폴더의 소리면 설정을 맞추고 true.</summary>
        public static bool ApplyAudioSettings(AudioImporter importer)
        {
            string path = importer.assetPath;
            bool music = InFolder(path, BgmFolder) || InFolder(path, AmbientFolder);
            bool sfx = InFolder(path, SfxFolder);
            if (!music && !sfx) return false;

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            if (music)
            {
                // 길다. 통째로 메모리에 올리지 않고 흘려보낸다.
                settings.loadType = AudioClipLoadType.Streaming;
                settings.quality = 0.6f;
            }
            else
            {
                // 짧고 자주 겹쳐 난다. 미리 풀어 두어야 누를 때 밀리지 않는다.
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.quality = 0.7f;
            }
            importer.defaultSampleSettings = settings;
            importer.forceToMono = true; // 전부 2D로만 쓴다
            return true;
        }

        // ----- 도우미 -----------------------------------------------------------

        private static bool InFolder(string assetPath, string folder)
        {
            return !string.IsNullOrEmpty(assetPath) && assetPath.Replace('\\', '/').StartsWith(folder + "/");
        }

        private static List<string> FindAssets(string filter, params string[] folders)
        {
            var paths = new List<string>();
            var existing = new List<string>();
            for (int i = 0; i < folders.Length; i++)
            {
                if (AssetDatabase.IsValidFolder(folders[i])) existing.Add(folders[i]);
            }
            if (existing.Count == 0) return paths;

            string[] guids = AssetDatabase.FindAssets(filter, existing.ToArray());
            for (int i = 0; i < guids.Length; i++) paths.Add(AssetDatabase.GUIDToAssetPath(guids[i]));
            paths.Sort(System.StringComparer.Ordinal);
            return paths;
        }
    }
}
