using System.Collections;
using System.Collections.Generic;
using System.Linq;
using InfiniteOrbit.GameMath;
using UnityEngine;
using Grid = InfiniteOrbit.GameMath.Grid;

namespace InfiniteOrbit.Presentation
{
    /// <summary>One symbol slot: art, rank/label text and an optional orb prize.</summary>
    public sealed class SymbolView
    {
        public readonly GameObject Root;
        private readonly SpriteRenderer _icon;
        private readonly SpriteRenderer _glow;
        private readonly TextMesh _label;
        private readonly TextMesh _value;
        private readonly float _size;
        private readonly GameConfig _config;
        public Symbol Symbol { get; private set; }

        public SymbolView(Transform parent, GameConfig config, float size)
        {
            _config = config;
            _size = size;
            Root = Ui.Node("Symbol", parent, Vector2.zero);
            _glow = Ui.Sprite("Glow", Root.transform, Art.Soft, Vector2.zero, size * 1.5f, Ui.Order.Symbols - 1, new Color(1, 1, 1, 0));
            _glow.sharedMaterial = Art.Additive;
            _icon = Ui.Sprite("Icon", Root.transform, Art.SymbolSprite(Symbol.Ten), Vector2.zero, size * 0.92f, Ui.Order.Symbols);
            _label = Ui.Text("Label", Root.transform, "", Vector2.zero, size * 0.42f, Color.white, Ui.Order.Symbols + 1);
            _value = Ui.Text("Value", Root.transform, "", new Vector2(0, -size * 0.02f), size * 0.2f, Color.white, Ui.Order.Symbols + 2);
        }

        public void Set(Symbol s, string orbValue = null)
        {
            Symbol = s;
            _icon.sprite = Art.SymbolSprite(s);
            float k = _size * 0.92f / Mathf.Max(_icon.sprite.bounds.size.x, _icon.sprite.bounds.size.y);
            _icon.transform.localScale = new Vector3(k, k, 1);
            var info = _config.Info(s);
            switch (info.Kind)
            {
                case SymbolKind.Low:
                    _label.text = info.Code;
                    _label.characterSize = _size * 0.42f / (Ui.FontSize * Ui.TextScale);
                    _label.transform.localPosition = Vector3.zero;
                    break;
                case SymbolKind.Wild:
                    _label.text = "WILD";
                    _label.characterSize = _size * 0.2f / (Ui.FontSize * Ui.TextScale);
                    _label.transform.localPosition = new Vector3(0, -_size * 0.33f, 0);
                    break;
                case SymbolKind.Scatter:
                    _label.text = "GATE";
                    _label.characterSize = _size * 0.2f / (Ui.FontSize * Ui.TextScale);
                    _label.transform.localPosition = new Vector3(0, -_size * 0.33f, 0);
                    break;
                default:
                    _label.text = "";
                    break;
            }
            _value.text = s == Symbol.Orb ? (orbValue ?? "") : "";
        }

        public void SetAlpha(float a)
        {
            _icon.color = new Color(1, 1, 1, a);
            _label.color = new Color(1, 1, 1, a);
            _value.color = new Color(1, 1, 1, a);
        }

        public void SetGlow(float a, Color color)
        {
            color.a = a;
            _glow.color = color;
        }

        public void SetScale(float s) => Root.transform.localScale = new Vector3(s, s, 1);
        public void SetVisible(bool v) => Root.SetActive(v);
        public void SetPosition(Vector2 p) => Root.transform.localPosition = new Vector3(p.x, p.y, 0);
    }

    /// <summary>A reel column. Position is a continuous strip index for the top row; it only ever plays back stops it is told.</summary>
    public sealed class ReelColumn
    {
        private const int Slots = 5; // 3 visible + 1 above + 1 below
        private readonly SymbolView[] _views = new SymbolView[Slots];
        private readonly float _cellH;
        private readonly Vector2 _topCenter;
        private ReelStrip _strip;
        private double _pos;
        private Dictionary<int, string> _orbLabels;

        public ReelColumn(Transform parent, GameConfig config, Vector2 topCenter, float cellW, float cellH)
        {
            _cellH = cellH;
            _topCenter = topCenter;
            for (int i = 0; i < Slots; i++) _views[i] = new SymbolView(parent, config, Mathf.Min(cellW, cellH));
        }

        public bool Spinning { get; set; }

        public void SetStrip(ReelStrip strip, int stop, Dictionary<int, string> orbLabels = null)
        {
            _strip = strip;
            _pos = stop;
            _orbLabels = orbLabels;
            Render();
        }

        public SymbolView ViewAtRow(int row)
        {
            // Same unwrapped index -> slot mapping as Render().
            return _views[Mod((int)System.Math.Round(_pos) + row, Slots)];
        }

        private static int Mod(int a, int n) => ((a % n) + n) % n;

        public void Render()
        {
            int baseIndex = (int)System.Math.Floor(_pos);
            for (int k = -1; k < Slots - 1; k++)
            {
                int j = baseIndex + k;
                var view = _views[Mod(j, Slots)];
                float row = (float)(j - _pos);
                bool visible = row > -1f && row < 3f;
                view.SetVisible(visible);
                if (!visible) continue;
                int stripIndex = Mod(j, _strip.Length);
                string label = null;
                if (!Spinning && _orbLabels != null) _orbLabels.TryGetValue(Mod(stripIndex - Mod((int)System.Math.Round(_pos), _strip.Length), _strip.Length), out label);
                view.Set(_strip[stripIndex], label);
                view.SetPosition(_topCenter + new Vector2(0, -row * _cellH));
                view.SetAlpha(Spinning ? 0.85f : 1f);
                view.SetGlow(0, Color.white);
                view.SetScale(1f);
            }
        }

        /// <summary>Scrolls downward at a constant speed (stops per second).</summary>
        public void Tick(float dt, float speed)
        {
            _pos -= speed * dt;
            if (_pos < -1e6) _pos += _strip.Length * 1000;
            Render();
        }

        /// <summary>Decelerates onto <paramref name="stop"/> after at least <paramref name="minTravel"/> more stops, with a small bounce.</summary>
        public IEnumerator LandOn(int stop, float minTravel, float duration)
        {
            double start = _pos;
            double target = start - minTravel;
            double delta = ((target - stop) % _strip.Length + _strip.Length) % _strip.Length;
            target -= delta;
            float t = 0;
            while (t < duration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / duration);
                // Ease-out-back: overshoots slightly then settles, like a mechanical reel stop.
                const float s = 1.2f;
                float e = 1f + (s + 1f) * Mathf.Pow(u - 1f, 3) + s * Mathf.Pow(u - 1f, 2);
                _pos = start + (target - start) * e;
                Render();
                yield return null;
            }
            _pos = stop;
            Spinning = false;
            Render();
        }
    }

    /// <summary>The 5x3 reel window, frame and payline highlights.</summary>
    public sealed class ReelsView
    {
        public readonly GameConfig Config;
        public readonly Transform Root;
        public readonly ReelColumn[] Columns = new ReelColumn[GameConfig.Reels];
        public readonly float CellW;
        public readonly float CellH;
        public readonly Vector2 Center;
        private readonly LineRenderer _line;
        private readonly SpriteRenderer[] _reelGlow = new SpriteRenderer[GameConfig.Reels];
        private ReelSet _reels;

        public ReelsView(Transform parent, GameConfig config, Vector2 center, float cellW, float cellH)
        {
            Config = config;
            Center = center;
            CellW = cellW;
            CellH = cellH;
            Root = Ui.Node("Reels", parent, center).transform;

            float w = cellW * GameConfig.Reels, h = cellH * GameConfig.Rows;
            Ui.Box("Window", Root, Vector2.zero, new Vector2(w + 0.2f, h + 0.2f), new Color(0.02f, 0.02f, 0.06f, 0.82f), Ui.Order.Cabinet);
            for (int r = 0; r < GameConfig.Reels; r++)
            {
                float x = (r - 2) * cellW;
                if (r > 0) Ui.Box("Divider", Root, new Vector2(x - cellW / 2, 0), new Vector2(0.02f, h), new Color(0.4f, 0.6f, 1f, 0.25f), Ui.Order.Cabinet + 1, false);
                _reelGlow[r] = Ui.Box("Anticipation", Root, new Vector2(x, 0), new Vector2(cellW, h), new Color(0.3f, 1f, 0.9f, 0f), Ui.Order.Highlights - 1);
                _reelGlow[r].sharedMaterial = Art.Additive;
                Columns[r] = new ReelColumn(Root, config, new Vector2(x, cellH), cellW, cellH);
            }

            // Opaque frame bars hide symbols scrolling outside the window (the classic cabinet "mask").
            var frameColor = new Color(0.05f, 0.06f, 0.14f, 1f);
            Ui.Box("FrameTop", Root, new Vector2(0, h / 2 + cellH * 0.6f + 0.1f), new Vector2(w + 1.2f, cellH * 1.2f), frameColor, Ui.Order.Frame, false);
            Ui.Box("FrameBottom", Root, new Vector2(0, -h / 2 - cellH * 0.6f - 0.1f), new Vector2(w + 1.2f, cellH * 1.2f), frameColor, Ui.Order.Frame, false);
            var trim = new Color(0.45f, 0.75f, 1f, 0.9f);
            Ui.Box("TrimTop", Root, new Vector2(0, h / 2 + 0.1f), new Vector2(w + 0.4f, 0.05f), trim, Ui.Order.Frame + 1, false);
            Ui.Box("TrimBottom", Root, new Vector2(0, -h / 2 - 0.1f), new Vector2(w + 0.4f, 0.05f), trim, Ui.Order.Frame + 1, false);

            var lineGo = Ui.Node("Payline", Root, Vector2.zero);
            _line = lineGo.AddComponent<LineRenderer>();
            _line.useWorldSpace = false;
            _line.positionCount = 0;
            _line.widthMultiplier = 0.07f;
            _line.sharedMaterial = Art.Additive;
            _line.sortingOrder = Ui.Order.Highlights;
            _line.numCapVertices = 4;
        }

        public Vector2 CellCenter(int reel, int row) => new Vector2((reel - 2) * CellW, (1 - row) * CellH);

        public void Show(ReelSet reels, int[] stops, IList<OrbCell> orbs = null)
        {
            _reels = reels;
            for (int r = 0; r < GameConfig.Reels; r++)
            {
                Columns[r].Spinning = false;
                Columns[r].SetStrip(reels[r], stops[r], OrbLabels(orbs, r));
            }
        }

        public static Dictionary<int, string> OrbLabels(IList<OrbCell> orbs, int reel)
        {
            if (orbs == null) return null;
            var map = new Dictionary<int, string>();
            foreach (var o in orbs)
                if (Grid.ReelOf(o.Cell) == reel) map[Grid.RowOf(o.Cell)] = OrbText(o);
            return map;
        }

        public static string OrbText(OrbCell o) => o.Prize.IsJackpot ? o.Prize.Kind.ToString().ToUpperInvariant() : Ui.Credits(o.Credits);

        public SymbolView View(int cell) => Columns[Grid.ReelOf(cell)].ViewAtRow(Grid.RowOf(cell));

        public void SetAnticipation(int reel, float a) => _reelGlow[reel].color = new Color(0.3f, 1f, 0.9f, a);

        public void SetVisible(bool visible)
        {
            for (int r = 0; r < GameConfig.Reels; r++)
                for (int row = 0; row < GameConfig.Rows; row++)
                    Columns[r].ViewAtRow(row).SetVisible(visible);
        }

        public void ClearHighlights()
        {
            _line.positionCount = 0;
            for (int c = 0; c < GameConfig.Cells; c++)
            {
                var v = View(c);
                v.SetGlow(0, Color.white);
                v.SetScale(1);
                v.SetAlpha(1);
            }
        }

        /// <summary>Draws a payline and pulses its winning cells; non-winning cells dim.</summary>
        public void HighlightLine(LineWin win, float pulse)
        {
            var line = Config.Paylines[win.LineIndex];
            _line.positionCount = GameConfig.Reels;
            for (int r = 0; r < GameConfig.Reels; r++)
            {
                var p = CellCenter(r, line.Rows[r]);
                _line.SetPosition(r, new Vector3(p.x, p.y, 0));
            }
            var color = Color.HSVToRGB((win.LineIndex * 0.13f) % 1f, 0.6f, 1f);
            _line.startColor = color;
            _line.endColor = color;
            HighlightCells(win.Cells, pulse, color);
        }

        public void HighlightCells(IReadOnlyList<int> cells, float pulse, Color color)
        {
            for (int c = 0; c < GameConfig.Cells; c++)
            {
                bool on = cells.Contains(c);
                var v = View(c);
                v.SetAlpha(on ? 1f : 0.45f);
                v.SetGlow(on ? 0.35f + 0.35f * pulse : 0, color);
                v.SetScale(on ? 1f + 0.06f * pulse : 1f);
            }
        }

        public void HideLine() => _line.positionCount = 0;
    }
}
