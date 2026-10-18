using System.Collections.Generic;
using InfiniteOrbit.GameMath;
using UnityEngine;

namespace InfiniteOrbit.Presentation
{
    /// <summary>
    /// Procedural placeholder art: every texture is generated at startup so the project needs no imported assets.
    /// Replace by dropping sprites into Resources/InfiniteOrbit/Symbols/&lt;SymbolName&gt; (e.g. "Pulsar").
    /// </summary>
    public static class Art
    {
        public const int SymbolSize = 128;

        private static readonly Dictionary<Symbol, Sprite> SymbolSprites = new Dictionary<Symbol, Sprite>();
        private static Sprite _white, _circle, _rounded, _soft;
        private static Font _font;
        private static Material _additive;

        public static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        public static Material Additive
        {
            get
            {
                if (_additive == null)
                {
                    var shader = Resources.Load<Shader>("InfiniteOrbit/AdditiveGlow");
                    _additive = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
                    _additive.mainTexture = Soft.texture;
                }
                return _additive;
            }
        }

        public static Sprite White => _white != null ? _white : (_white = MakeSprite(Tex(4, (x, y) => Color.white), 4));
        public static Sprite Circle => _circle != null ? _circle : (_circle = MakeSprite(Tex(64, (x, y) => Alpha(Color.white, Mathf.Clamp01((1f - Len(x, y)) * 32f))), 64));
        public static Sprite Soft => _soft != null ? _soft : (_soft = MakeSprite(Tex(64, (x, y) => Alpha(Color.white, Mathf.Pow(Mathf.Clamp01(1f - Len(x, y)), 2f))), 64));

        /// <summary>Rounded rectangle with a 9-slice border, used for panels and buttons (drawMode = Sliced).</summary>
        public static Sprite Rounded
        {
            get
            {
                if (_rounded != null) return _rounded;
                const int n = 64;
                const float r = 16f;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = Mathf.Max(0, Mathf.Max(r - x - 0.5f, x + 0.5f - (n - r)));
                        float dy = Mathf.Max(0, Mathf.Max(r - y - 0.5f, y + 0.5f - (n - r)));
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        px[y * n + x] = Alpha(Color.white, Mathf.Clamp01(r - d));
                    }
                tex.SetPixels(px);
                tex.Apply();
                _rounded = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
                return _rounded;
            }
        }

        public static Color SymbolColor(Symbol s)
        {
            switch (s)
            {
                case Symbol.Ten: return new Color(0.35f, 0.75f, 1f);
                case Symbol.Jack: return new Color(0.45f, 1f, 0.65f);
                case Symbol.Queen: return new Color(1f, 0.55f, 0.9f);
                case Symbol.King: return new Color(1f, 0.8f, 0.35f);
                case Symbol.Ace: return new Color(1f, 0.45f, 0.4f);
                case Symbol.Comet: return new Color(0.6f, 0.9f, 1f);
                case Symbol.Nebula: return new Color(0.85f, 0.45f, 1f);
                case Symbol.Ringworld: return new Color(1f, 0.75f, 0.45f);
                case Symbol.Pulsar: return new Color(1f, 1f, 0.75f);
                case Symbol.Wild: return new Color(1f, 0.55f, 0.15f);
                case Symbol.Scatter: return new Color(0.4f, 1f, 0.9f);
                default: return new Color(0.35f, 0.9f, 1f);
            }
        }

        public static Sprite SymbolSprite(Symbol s)
        {
            if (SymbolSprites.TryGetValue(s, out var sprite)) return sprite;
            sprite = Resources.Load<Sprite>("InfiniteOrbit/Symbols/" + s);
            if (sprite == null) sprite = MakeSprite(DrawSymbol(s), SymbolSize);
            SymbolSprites[s] = sprite;
            return sprite;
        }

        // ------------------------------------------------------------------ procedural drawing

        private delegate Color Shader2D(float x, float y);

        private static float Len(float x, float y) => Mathf.Sqrt(x * x + y * y);
        private static Color Alpha(Color c, float a) { c.a = a; return c; }

        private static Texture2D Tex(int n, Shader2D f)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    px[y * n + x] = f((x + 0.5f) / n * 2f - 1f, (y + 0.5f) / n * 2f - 1f);
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        private static Sprite MakeSprite(Texture2D tex, float ppu) =>
            Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu);

        private static Color Over(Color under, Color over)
        {
            float a = over.a + under.a * (1 - over.a);
            if (a <= 0) return Color.clear;
            Color c = (over * over.a + under * under.a * (1 - over.a)) / a;
            c.a = a;
            return c;
        }

        private static Texture2D DrawSymbol(Symbol s)
        {
            Color tint = SymbolColor(s);
            switch (s)
            {
                case Symbol.Comet:
                    return Tex(SymbolSize, (x, y) =>
                    {
                        Color c = Disc(x, y, 0.92f, new Color(0.05f, 0.08f, 0.2f));
                        // Tail along the diagonal toward the bottom-left.
                        float t = (x + y) * 0.7071f;
                        float side = Mathf.Abs(x - y) * 0.7071f;
                        float tail = Mathf.Clamp01((0.35f - t) * 1.2f) * Mathf.Clamp01(1f - side / (0.08f + 0.25f * Mathf.Clamp01(0.35f - t)));
                        c = Over(c, Alpha(tint, tail * 0.85f * Mask(x, y)));
                        float head = Len(x - 0.3f, y - 0.3f);
                        c = Over(c, Alpha(Color.white, Mathf.Clamp01((0.22f - head) * 12f)));
                        return c;
                    });
                case Symbol.Nebula:
                    return Tex(SymbolSize, (x, y) =>
                    {
                        Color c = Disc(x, y, 0.92f, new Color(0.07f, 0.03f, 0.15f));
                        float r = Len(x, y);
                        float ang = Mathf.Atan2(y, x);
                        float arms = 0.5f + 0.5f * Mathf.Sin(ang * 2f + r * 9f);
                        float density = arms * Mathf.Clamp01(1f - r / 0.85f);
                        c = Over(c, Alpha(Color.Lerp(tint, new Color(0.3f, 0.6f, 1f), r), density * Mask(x, y)));
                        c = Over(c, Alpha(Color.white, Mathf.Clamp01((0.12f - r) * 10f)));
                        return c;
                    });
                case Symbol.Ringworld:
                    return Tex(SymbolSize, (x, y) =>
                    {
                        Color c = Disc(x, y, 0.92f, new Color(0.04f, 0.05f, 0.14f));
                        float rx = x, ry = (y - 0.02f) / 0.32f;
                        float ring = Mathf.Abs(Len(rx, ry) - 0.78f);
                        bool behind = y > 0.02f;
                        Color ringCol = Alpha(new Color(1f, 0.9f, 0.7f), Mathf.Clamp01((0.12f - ring) * 10f) * 0.9f);
                        if (behind) c = Over(c, ringCol);
                        float pr = Len(x, y);
                        if (pr < 0.5f)
                        {
                            float shade = Mathf.Clamp01(0.4f + 0.8f * (-x * 0.5f + y * 0.6f + Mathf.Sqrt(Mathf.Max(0, 0.25f - pr * pr))));
                            float band = 0.85f + 0.15f * Mathf.Sin(y * 30f);
                            c = Over(c, Alpha(tint * shade * band, Mathf.Clamp01((0.5f - pr) * 40f)));
                        }
                        if (!behind) c = Over(c, ringCol);
                        return c;
                    });
                case Symbol.Pulsar:
                    return Tex(SymbolSize, (x, y) =>
                    {
                        Color c = Disc(x, y, 0.92f, new Color(0.03f, 0.03f, 0.1f));
                        float r = Len(x, y);
                        float bx = x * 0.8f + y * 0.6f, by = -x * 0.6f + y * 0.8f;
                        float beam = Mathf.Clamp01(1f - Mathf.Abs(bx) / (0.03f + 0.1f * Mathf.Abs(by))) * Mathf.Clamp01(1f - Mathf.Abs(by));
                        float rings = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(r * 30f), 6f) * Mathf.Clamp01(1f - r);
                        c = Over(c, Alpha(new Color(0.5f, 0.8f, 1f), rings * 0.5f * Mask(x, y)));
                        c = Over(c, Alpha(tint, beam * Mask(x, y)));
                        c = Over(c, Alpha(Color.white, Mathf.Clamp01((0.16f - r) * 10f)));
                        return c;
                    });
                case Symbol.Wild:
                    return Tex(SymbolSize, (x, y) =>
                    {
                        float r = Len(x, y);
                        Color c = Disc(x, y, 0.95f, new Color(0.02f, 0.01f, 0.03f));
                        float disk = Mathf.Clamp01(1f - Mathf.Abs(r - 0.6f) / 0.28f);
                        float ang = Mathf.Atan2(y, x);
                        float swirl = 0.6f + 0.4f * Mathf.Sin(ang * 3f + r * 14f);
                        c = Over(c, Alpha(Color.Lerp(tint, Color.white, disk * 0.5f), disk * swirl * Mask(x, y)));
                        c = Over(c, Alpha(Color.black, Mathf.Clamp01((0.36f - r) * 20f)));
                        c = Over(c, Alpha(Color.white, Mathf.Clamp01(1f - Mathf.Abs(r - 0.37f) * 40f) * 0.8f));
                        return c;
                    });
                case Symbol.Scatter:
                    return Tex(SymbolSize, (x, y) =>
                    {
                        Color c = Disc(x, y, 0.95f, new Color(0.02f, 0.05f, 0.08f));
                        // A tiny Julia set: the same family as the background shader.
                        float zx = x * 1.5f, zy = y * 1.5f;
                        int i = 0;
                        for (; i < 40; i++)
                        {
                            float nx = zx * zx - zy * zy - 0.745f;
                            zy = 2f * zx * zy + 0.186f;
                            zx = nx;
                            if (zx * zx + zy * zy > 16f) break;
                        }
                        float f = i / 40f;
                        Color fc = Color.Lerp(new Color(0.1f, 0.2f, 0.6f), tint, f);
                        c = Over(c, Alpha(fc, Mathf.Clamp01(f * 2.5f) * Mask(x, y)));
                        float ringR = Len(x, y);
                        c = Over(c, Alpha(tint, Mathf.Clamp01(1f - Mathf.Abs(ringR - 0.88f) * 25f)));
                        return c;
                    });
                case Symbol.Orb:
                    return Tex(SymbolSize, (x, y) =>
                    {
                        float r = Len(x, y);
                        Color glow = Alpha(tint, Mathf.Pow(Mathf.Clamp01(1f - r), 1.5f) * 0.7f);
                        Color c = glow;
                        if (r < 0.72f)
                        {
                            float zz = Mathf.Sqrt(0.72f * 0.72f - r * r) / 0.72f;
                            float light = Mathf.Clamp01(0.25f + 0.75f * (-x * 0.4f + y * 0.5f + zz * 0.7f));
                            Color body = Color.Lerp(new Color(0.02f, 0.2f, 0.4f), tint, light);
                            body = Color.Lerp(body, Color.white, Mathf.Pow(light, 8f));
                            c = Over(c, Alpha(body, Mathf.Clamp01((0.72f - r) * 40f)));
                        }
                        return c;
                    });
                default:
                    // Card ranks: faceted crystal plaques; the rank letter is drawn as text on top.
                    return Tex(SymbolSize, (x, y) =>
                    {
                        float d = Mathf.Max(Mathf.Abs(x) * 0.95f + Mathf.Abs(y) * 0.35f, Mathf.Abs(y) * 0.98f);
                        if (d > 0.9f) return Color.clear;
                        float facet = 0.55f + 0.45f * Mathf.Sign(x * y) * 0.3f + 0.2f * (y + 0.2f);
                        Color body = Color.Lerp(tint * 0.25f, tint * 0.75f, Mathf.Clamp01(facet));
                        float edge = Mathf.Clamp01(1f - (0.9f - d) * 20f);
                        body = Color.Lerp(body, tint, edge);
                        return Alpha(body, Mathf.Clamp01((0.9f - d) * 60f) * 0.95f);
                    });
            }
        }

        private static Color Disc(float x, float y, float radius, Color c) => Alpha(c, Mathf.Clamp01((radius - Len(x, y)) * 40f));
        private static float Mask(float x, float y) => Mathf.Clamp01((0.92f - Len(x, y)) * 40f);
    }
}
