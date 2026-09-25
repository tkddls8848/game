using UnityEngine;

namespace Detective.Art
{
    /// <summary>
    /// 코드로 만드는 질감. 외부 이미지가 없어도 "사건 파일" 분위기가 나도록 바닥·종이·비네팅·말(토큰)·초상 실루엣을 그린다.
    /// 씬 빌더는 이걸 PNG로 저장해 씬에 끼우고(직렬화 가능), 런타임 UI는 바로 텍스처로 쓴다.
    /// 전부 결정적(seed 고정)이라 다시 만들어도 같은 그림이 나온다.
    /// </summary>
    public static class ProceduralTextures
    {
        public const int FloorSize = 256;

        // ----- 노이즈 ------------------------------------------------------------

        /// <summary>타일링되는 값 노이즈(0~1). period는 한 변의 격자 수.</summary>
        private static float TiledNoise(int x, int y, int size, int period, int seed)
        {
            float fx = (float)x / size * period;
            float fy = (float)y / size * period;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = Smooth(fx - x0), ty = Smooth(fy - y0);

            float a = Hash(x0, y0, period, seed), b = Hash(x0 + 1, y0, period, seed);
            float c = Hash(x0, y0 + 1, period, seed), d = Hash(x0 + 1, y0 + 1, period, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        private static float Fbm(int x, int y, int size, int basePeriod, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, total = 0f;
            int period = basePeriod;
            for (int o = 0; o < octaves; o++)
            {
                sum += TiledNoise(x, y, size, period, seed + o * 17) * amp;
                total += amp;
                amp *= 0.5f;
                period *= 2;
            }
            return sum / total;
        }

        private static float Hash(int x, int y, int period, int seed)
        {
            x = ((x % period) + period) % period;
            y = ((y % period) + period) % period;
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
        }

        private static float Smooth(float t) { return t * t * (3f - 2f * t); }

        // ----- 바닥 --------------------------------------------------------------

        /// <summary>종류별 바닥 질감(회색조, 타일링). 색은 SpriteRenderer.color로 입힌다.</summary>
        public static Texture2D Floor(string kind)
        {
            switch (kind)
            {
                case "marble": return Marble();
                case "stone": return Stone();
                case "carpet": return Carpet();
                case "rug": return Rug();
                default: return Wood();
            }
        }

        /// <summary>긴 판자 마루. 나뭇결 + 판자 이음새.</summary>
        public static Texture2D Wood()
        {
            return Make(FloorSize, (x, y) =>
            {
                int plankH = FloorSize / 8;
                int row = y / plankH;
                int offset = (row % 2) * FloorSize / 2;
                float grain = Fbm(x + offset + row * 37, y * 6, FloorSize, 4, 4, 11);
                float streak = Mathf.Sin((x + offset) * 0.35f + grain * 9f) * 0.5f + 0.5f;
                float v = 0.62f + (grain - 0.5f) * 0.25f + (streak - 0.5f) * 0.10f;

                bool seamY = y % plankH == 0;
                bool seamX = (x + offset) % (FloorSize / 2) == 0;
                if (seamY || seamX) v *= 0.55f;
                else if (y % plankH == 1) v *= 1.06f; // 이음새 옆 하이라이트
                return v;
            });
        }

        /// <summary>체스판 대리석. 흰 칸은 옅은 무늬, 검은 칸은 진한 무늬.</summary>
        public static Texture2D Marble()
        {
            return Make(FloorSize, (x, y) =>
            {
                int cell = FloorSize / 4;
                bool dark = ((x / cell) + (y / cell)) % 2 == 0;
                float vein = Mathf.Abs(Mathf.Sin(Fbm(x, y, FloorSize, 3, 4, 23) * 12f + x * 0.03f));
                float v = dark ? 0.32f + vein * 0.10f : 0.86f - vein * 0.14f;
                if (x % cell == 0 || y % cell == 0) v *= 0.7f;
                return v;
            });
        }

        /// <summary>거친 석재 바닥. 불규칙한 돌 조각.</summary>
        public static Texture2D Stone()
        {
            return Make(FloorSize, (x, y) =>
            {
                int cell = FloorSize / 6;
                int cx = x / cell, cy = y / cell;
                float jitter = Hash(cx, cy, 6, 5) * 0.18f;
                float rough = Fbm(x, y, FloorSize, 8, 3, 41);
                float v = 0.50f + jitter + (rough - 0.5f) * 0.22f;
                int lx = x % cell, ly = y % cell;
                if (lx < 2 || ly < 2) v *= 0.6f;
                return v;
            });
        }

        /// <summary>서재 카펫. 촘촘한 직물 + 큼직한 다마스크 무늬.</summary>
        public static Texture2D Carpet()
        {
            return Make(FloorSize, (x, y) =>
            {
                float weave = ((x + y) % 2 == 0) ? 0.03f : -0.03f;
                float px = x / (float)FloorSize * Mathf.PI * 4f;
                float py = y / (float)FloorSize * Mathf.PI * 4f;
                float damask = Mathf.Abs(Mathf.Sin(px) * Mathf.Sin(py));
                damask = Mathf.Pow(damask, 3f);
                float fuzz = Fbm(x, y, FloorSize, 16, 2, 77);
                return 0.55f + weave + damask * 0.22f + (fuzz - 0.5f) * 0.08f;
            });
        }

        /// <summary>복도 러너 러그. 가운데 긴 띠 + 테두리 줄.</summary>
        public static Texture2D Rug()
        {
            return Make(FloorSize, (x, y) =>
            {
                float weave = ((x + y) % 2 == 0) ? 0.03f : -0.03f;
                float fuzz = Fbm(x, y, FloorSize, 16, 2, 91);
                float v = 0.52f + weave + (fuzz - 0.5f) * 0.08f;
                int stripe = (y * 12 / FloorSize) % 12;
                if (stripe == 0 || stripe == 11) v *= 0.75f;      // 바깥 어두운 줄
                else if (stripe == 1 || stripe == 10) v *= 1.25f; // 밝은 테두리 줄
                return v;
            });
        }

        // ----- UI ----------------------------------------------------------------

        /// <summary>바랜 종이. UI 패널 배경. 밝은 베이지에 얼룩과 섬유.</summary>
        public static Texture2D Parchment(int size)
        {
            return Make(size, (x, y) =>
            {
                float stain = Fbm(x, y, size, 2, 5, 131);
                float fiber = Fbm(x * 3, y, size, 32, 2, 137);
                return 0.90f - (stain - 0.5f) * 0.14f - (fiber - 0.5f) * 0.05f;
            });
        }

        /// <summary>화면 가장자리를 어둡게 하는 비네팅(알파만 있음).</summary>
        public static Texture2D Vignette(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half, dy = (y - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((d - 0.55f) / 0.75f);
                    a = a * a;
                    px[y * size + x] = new Color32(8, 6, 4, (byte)(a * 235f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        /// <summary>안티에일리어싱된 원판. 인물·플레이어 말(토큰). 테두리 링은 어둡다.</summary>
        public static Texture2D Disc(int size, float ringWidth)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            float r = size * 0.5f - 1f;
            float c = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c));
                    float alpha = Mathf.Clamp01(r - d + 0.5f);
                    float ring = Mathf.Clamp01(d - (r - ringWidth) + 0.5f);
                    // 위쪽이 살짝 밝아 입체감이 난다.
                    float shade = 0.82f + (y / (float)size) * 0.18f;
                    byte v = (byte)(255f * shade * (1f - ring * 0.78f));
                    px[y * size + x] = new Color32(v, v, v, (byte)(alpha * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        /// <summary>속이 빈 링(흰색, 알파만). 소나 파문. 스케일이 곧 지름이 되도록 정사각형 가득 그린다.</summary>
        public static Texture2D Ring(int size, float thickness)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            float r = size * 0.5f - 1f;
            float c = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c));
                    float outer = Mathf.Clamp01(r - d + 0.5f);
                    float inner = Mathf.Clamp01(d - (r - thickness) + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Min(outer, inner) * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        /// <summary>흰 정사각형. 실행 중에 면(가림막·방 강조)을 그릴 때 쓴다.</summary>
        public static Texture2D Square(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        /// <summary>초상화 자리 대체: 어두운 배경에 흉상 실루엣. 이름 첫 글자는 UI가 위에 따로 얹는다.</summary>
        public static Texture2D PortraitSilhouette(int size, int seed)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            float cx = size * 0.5f;
            float headR = size * 0.17f;
            float headY = size * 0.60f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float grain = Fbm(x, y, size, 2, 4, 200 + seed);
                    float bg = 0.20f + (grain - 0.5f) * 0.10f;
                    // 위쪽은 살짝 밝게: 오래된 유화의 광원처럼
                    bg += (y / (float)size) * 0.08f;

                    float dx = x - cx, dy = y - headY;
                    bool head = dx * dx + dy * dy < headR * headR;
                    float shoulderY = size * 0.38f;
                    float shoulderW = size * 0.34f * Mathf.Clamp01((shoulderY - y) / (size * 0.25f) + 0.6f);
                    bool torso = y < shoulderY && Mathf.Abs(dx) < shoulderW;
                    bool neck = y >= shoulderY && y < headY && Mathf.Abs(dx) < size * 0.07f;

                    float v = (head || torso || neck) ? 0.07f : bg;
                    byte b = (byte)(Mathf.Clamp01(v) * 255f);
                    // 세피아: R > G > B
                    px[y * size + x] = new Color32((byte)Mathf.Min(255, b + 18), (byte)Mathf.Min(255, b + 8), b, 255);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        // ----- 공통 --------------------------------------------------------------

        private static Texture2D Make(int size, System.Func<int, int, float> shade)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    byte v = (byte)(Mathf.Clamp01(shade(x, y)) * 255f);
                    px[y * size + x] = new Color32(v, v, v, 255);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        public static Sprite ToSprite(Texture2D tex, float pixelsPerUnit)
        {
            return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }
    }
}
