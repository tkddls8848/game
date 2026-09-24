using System.Collections.Generic;
using Detective.Data;
using UnityEngine;

namespace Detective.Art
{
    /// <summary>
    /// art.json을 읽고, 에셋이 있으면 그것을, 없으면 코드가 만든 대체물을 돌려주는 단일 창구.
    /// 실행 중 한 번만 만들고 결과를 캐시한다. 어떤 에셋이 대체물로 나갔는지는 로그 한 줄로 남긴다.
    /// </summary>
    public sealed class ArtLibrary
    {
        public const string ManifestResourcePath = "GameData/art";
        public const int PortraitSize = 256;

        private static ArtLibrary _instance;
        public static ArtLibrary Instance { get { return _instance ?? (_instance = new ArtLibrary()); } }

        public readonly ArtManifest Manifest;

        private readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();
        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private readonly List<string> _missing = new List<string>();
        private Texture2D _parchment;
        private Texture2D _vignette;
        private Texture2D _disc;

        private ArtLibrary()
        {
            var asset = Resources.Load<TextAsset>(ManifestResourcePath);
            ArtManifest manifest = null;
            if (asset != null) manifest = GameDataLoader.Parse<ArtManifest>(asset.text, "art.json");
            else Debug.LogWarning("[ArtLibrary] Resources/" + ManifestResourcePath + ".json 이 없어 전부 생성 질감을 쓴다.");
            Manifest = (manifest ?? new ArtManifest()).Normalized();
        }

        /// <summary>테스트/씬 재시작용.</summary>
        public static void Reset() { _instance = null; }

        /// <summary>대체물로 나간 에셋 경로 목록(중복 없음). 시작 화면 등에서 안내할 때 쓴다.</summary>
        public IList<string> MissingAssets { get { return _missing.AsReadOnly(); } }

        // ----- 그림 --------------------------------------------------------------

        /// <summary>인물 초상화. 파일이 없으면 세피아 실루엣.</summary>
        public Sprite Portrait(NpcDefinition npc)
        {
            if (npc == null) return null;
            NpcArt art = Manifest.NpcOf(npc.id);
            string path = art != null ? art.portrait : null;
            string key = "portrait:" + npc.id;

            Sprite sprite;
            if (_sprites.TryGetValue(key, out sprite)) return sprite;

            sprite = LoadSprite(path);
            if (sprite == null)
            {
                MarkMissing(path);
                sprite = ProceduralTextures.ToSprite(ProceduralTextures.PortraitSilhouette(PortraitSize, npc.id.GetHashCode() & 0xFF), 100f);
            }
            _sprites[key] = sprite;
            return sprite;
        }

        public bool HasPortraitFile(string npcId)
        {
            NpcArt art = Manifest.NpcOf(npcId);
            return art != null && LoadSprite(art.portrait) != null;
        }

        /// <summary>단서 이미지. 없으면 null(글만 보여 준다).</summary>
        public Sprite EvidenceImage(string evidenceId)
        {
            EvidenceArt art = Manifest.EvidenceOf(evidenceId);
            if (art == null || string.IsNullOrEmpty(art.image)) return null;

            Sprite sprite;
            string key = "evidence:" + evidenceId;
            if (_sprites.TryGetValue(key, out sprite)) return sprite;

            sprite = LoadSprite(art.image);
            if (sprite == null) MarkMissing(art.image);
            _sprites[key] = sprite;
            return sprite;
        }

        /// <summary>UI 패널용 종이 질감(런타임 생성, 공유).</summary>
        public Sprite ParchmentSprite()
        {
            if (_parchment == null) _parchment = ProceduralTextures.Parchment(256);
            return Cached("parchment", () => ProceduralTextures.ToSprite(_parchment, 100f));
        }

        public Sprite VignetteSprite()
        {
            if (_vignette == null) _vignette = ProceduralTextures.Vignette(256);
            return Cached("vignette", () => ProceduralTextures.ToSprite(_vignette, 100f));
        }

        public Sprite DiscSprite()
        {
            if (_disc == null) _disc = ProceduralTextures.Disc(128, 9f);
            return Cached("disc", () => ProceduralTextures.ToSprite(_disc, 128f));
        }

        /// <summary>UI 폰트. art.json → OS 한글 폰트 → 내장 폰트.</summary>
        public Font LoadUiFont()
        {
            if (string.IsNullOrEmpty(Manifest.uiFont)) return null;
            Font font = Resources.Load<Font>(Manifest.uiFont);
            if (font == null) MarkMissing(Manifest.uiFont);
            return font;
        }

        // ----- 소리 --------------------------------------------------------------

        public AudioClip Clip(string resourcePath, System.Func<AudioClip> fallback)
        {
            string key = string.IsNullOrEmpty(resourcePath) ? "(none)" : resourcePath;
            AudioClip clip;
            if (_clips.TryGetValue(key, out clip)) return clip;

            clip = string.IsNullOrEmpty(resourcePath) ? null : Resources.Load<AudioClip>(resourcePath);
            if (clip == null)
            {
                MarkMissing(resourcePath);
                if (fallback != null) clip = fallback();
            }
            _clips[key] = clip;
            return clip;
        }

        // ----- 내부 --------------------------------------------------------------

        private Sprite LoadSprite(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite != null) return sprite;

            // Sprite 타입으로 임포트되지 않은 그림도 받아 준다.
            var tex = Resources.Load<Texture2D>(path);
            return tex != null ? ProceduralTextures.ToSprite(tex, 100f) : null;
        }

        private Sprite Cached(string key, System.Func<Sprite> make)
        {
            Sprite sprite;
            if (_sprites.TryGetValue(key, out sprite) && sprite != null) return sprite;
            sprite = make();
            _sprites[key] = sprite;
            return sprite;
        }

        private void MarkMissing(string path)
        {
            if (string.IsNullOrEmpty(path) || _missing.Contains(path)) return;
            _missing.Add(path);
            Debug.Log("[ArtLibrary] Resources/" + path + " 이 없어 생성 대체물을 쓴다.");
        }
    }
}
