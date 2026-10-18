using System;
using System.Collections.Generic;
using UnityEngine;

namespace InfiniteOrbit.Presentation
{
    /// <summary>
    /// Minimal world-space UI kit (sprites + TextMesh + rect hit-testing). Cabinet games typically render
    /// their UI as 2D sprites in world space; this also keeps the project free of package dependencies.
    /// </summary>
    public static class Ui
    {
        public const int FontSize = 64;

        /// <summary>World units of line height per unit of TextMesh characterSize x fontSize.</summary>
        public const float TextScale = 0.1f;

        public static class Order
        {
            public const int Background = -100;
            public const int Cabinet = 0;
            public const int Symbols = 10;
            public const int Frame = 20;
            public const int Highlights = 30;
            public const int Hud = 40;
            public const int Particles = 60;
            public const int Overlay = 80;
            public const int Panel = 100;
            public const int Tilt = 200;
        }

        public static GameObject Node(string name, Transform parent, Vector2 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(pos.x, pos.y, 0);
            return go;
        }

        public static SpriteRenderer Box(string name, Transform parent, Vector2 pos, Vector2 size, Color color, int order, bool rounded = true)
        {
            var sr = Node(name, parent, pos).AddComponent<SpriteRenderer>();
            sr.sprite = rounded ? Art.Rounded : Art.White;
            sr.color = color;
            sr.sortingOrder = order;
            if (rounded)
            {
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.size = size;
            }
            else
            {
                sr.transform.localScale = new Vector3(size.x / sr.sprite.bounds.size.x, size.y / sr.sprite.bounds.size.y, 1);
            }
            return sr;
        }

        public static SpriteRenderer Sprite(string name, Transform parent, Sprite sprite, Vector2 pos, float size, int order, Color? color = null)
        {
            var sr = Node(name, parent, pos).AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            sr.color = color ?? Color.white;
            float s = size / Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
            sr.transform.localScale = new Vector3(s, s, 1);
            return sr;
        }

        public static TextMesh Text(string name, Transform parent, string text, Vector2 pos, float height, Color color, int order, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var go = Node(name, parent, pos);
            var tm = go.AddComponent<TextMesh>();
            tm.font = Art.Font;
            tm.fontSize = FontSize;
            tm.characterSize = height / (FontSize * TextScale);
            tm.anchor = anchor;
            tm.alignment = anchor == TextAnchor.MiddleLeft || anchor == TextAnchor.UpperLeft || anchor == TextAnchor.LowerLeft
                ? TextAlignment.Left
                : anchor == TextAnchor.MiddleRight || anchor == TextAnchor.UpperRight || anchor == TextAnchor.LowerRight ? TextAlignment.Right : TextAlignment.Center;
            tm.color = color;
            tm.text = text;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = Art.Font.material;
            mr.sortingOrder = order;
            return tm;
        }

        public static string Credits(long c) => c.ToString("N0");
    }

    /// <summary>A clickable world-space button. Hit testing is done by <see cref="ButtonHub"/>.</summary>
    public sealed class UiButton
    {
        public readonly GameObject Root;
        public readonly SpriteRenderer Background;
        public readonly TextMesh Label;
        public readonly Vector2 Size;
        public Action OnClick;
        public Color BaseColor;

        private bool _enabled = true;
        private bool _highlighted;

        public UiButton(Transform parent, string label, Vector2 pos, Vector2 size, Color color, int order, Action onClick, float textHeight = 0.28f)
        {
            Size = size;
            BaseColor = color;
            Background = Ui.Box("Button " + label, parent, pos, size, color, order);
            Root = Background.gameObject;
            Label = Ui.Text("Label", Root.transform, label, Vector2.zero, textHeight, Color.white, order + 1);
            OnClick = onClick;
            ButtonHub.Register(this);
        }

        public bool Enabled
        {
            get => _enabled;
            set { _enabled = value; Refresh(); }
        }

        public bool Highlighted
        {
            get => _highlighted;
            set { _highlighted = value; Refresh(); }
        }

        public bool Visible => Root.activeInHierarchy;

        public void SetText(string text) => Label.text = text;

        private void Refresh()
        {
            var c = _highlighted ? Color.Lerp(BaseColor, Color.white, 0.35f) : BaseColor;
            Background.color = _enabled ? c : new Color(c.r * 0.35f, c.g * 0.35f, c.b * 0.35f, 0.7f);
            Label.color = _enabled ? Color.white : new Color(1, 1, 1, 0.4f);
        }

        public bool Contains(Vector2 world)
        {
            var p = (Vector2)Root.transform.position;
            var scale = Root.transform.lossyScale;
            return Mathf.Abs(world.x - p.x) <= Size.x * scale.x * 0.5f && Mathf.Abs(world.y - p.y) <= Size.y * scale.y * 0.5f;
        }
    }

    /// <summary>Routes mouse/touch clicks to the topmost visible, enabled button.</summary>
    public static class ButtonHub
    {
        private static readonly List<UiButton> Buttons = new List<UiButton>();

        public static void Register(UiButton b) => Buttons.Add(b);
        public static void Clear() => Buttons.Clear();

        /// <summary>Returns true if a button consumed the click.</summary>
        public static bool Click(Vector2 world)
        {
            UiButton best = null;
            int bestOrder = int.MinValue;
            foreach (var b in Buttons)
            {
                if (b.Root == null || !b.Visible || !b.Enabled || !b.Contains(world)) continue;
                if (b.Background.sortingOrder > bestOrder)
                {
                    best = b;
                    bestOrder = b.Background.sortingOrder;
                }
            }
            if (best == null) return false;
            best.OnClick?.Invoke();
            return true;
        }
    }
}
