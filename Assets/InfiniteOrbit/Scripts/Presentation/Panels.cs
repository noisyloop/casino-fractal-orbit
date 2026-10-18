using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using InfiniteOrbit.Core;
using InfiniteOrbit.GameMath;
using UnityEngine;
using Grid = InfiniteOrbit.GameMath.Grid;

namespace InfiniteOrbit.Presentation
{
    /// <summary>Base for full-screen modal panels with a close button.</summary>
    public abstract class Panel
    {
        protected readonly Transform Root;
        protected readonly List<UiButton> Buttons = new List<UiButton>();
        public bool IsOpen => Root.gameObject.activeSelf;

        protected Panel(Transform parent, string title, Vector2 size)
        {
            Root = Ui.Node(title + " Panel", parent, Vector2.zero).transform;
            Ui.Box("Shade", Root, Vector2.zero, new Vector2(60, 40), new Color(0, 0, 0, 0.6f), Ui.Order.Panel - 2, false);
            Ui.Box("Body", Root, Vector2.zero, size, new Color(0.04f, 0.05f, 0.13f, 0.97f), Ui.Order.Panel - 1);
            Ui.Text("Title", Root, title, new Vector2(0, size.y / 2 - 0.45f), 0.42f, new Color(0.7f, 0.9f, 1f), Ui.Order.Panel);
            Buttons.Add(new UiButton(Root, "CLOSE", new Vector2(size.x / 2 - 1.0f, size.y / 2 - 0.45f), new Vector2(1.5f, 0.55f), new Color(0.5f, 0.2f, 0.3f), Ui.Order.Panel + 2, Close, 0.22f));
            Root.gameObject.SetActive(false);
        }

        public virtual void Open()
        {
            Root.gameObject.SetActive(true);
            Refresh();
        }

        public void Close() => Root.gameObject.SetActive(false);
        public void Toggle() { if (IsOpen) Close(); else Open(); }
        public abstract void Refresh();
    }

    /// <summary>Paytable / help, generated entirely from <see cref="GameConfig"/> (the same object the math uses).</summary>
    public sealed class PaytablePanel : Panel
    {
        private readonly GameConfig _config;
        private readonly Func<long> _lineBet;
        private readonly Transform _page;
        private int _pageIndex;
        private const int Pages = 3;

        public PaytablePanel(Transform parent, GameConfig config, Func<long> lineBet) : base(parent, "PAYTABLE & RULES", new Vector2(16.5f, 8.6f))
        {
            _config = config;
            _lineBet = lineBet;
            _page = Ui.Node("Page", Root, Vector2.zero).transform;
            Buttons.Add(new UiButton(Root, "<", new Vector2(-1.2f, -3.85f), new Vector2(0.9f, 0.5f), new Color(0.2f, 0.3f, 0.6f), Ui.Order.Panel + 2, () => Flip(-1)));
            Buttons.Add(new UiButton(Root, ">", new Vector2(1.2f, -3.85f), new Vector2(0.9f, 0.5f), new Color(0.2f, 0.3f, 0.6f), Ui.Order.Panel + 2, () => Flip(1)));
        }

        private void Flip(int d)
        {
            _pageIndex = (_pageIndex + d + Pages) % Pages;
            Refresh();
        }

        public override void Refresh()
        {
            for (int i = _page.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(_page.GetChild(i).gameObject);
            Ui.Text("PageNo", _page, $"{_pageIndex + 1} / {Pages}", new Vector2(0, -3.85f), 0.22f, Color.gray, Ui.Order.Panel);
            switch (_pageIndex)
            {
                case 0: PaysPage(); break;
                case 1: FeaturesPage(); break;
                default: LinesPage(); break;
            }
        }

        private void PaysPage()
        {
            long lb = _lineBet();
            Ui.Text("Note", _page, $"Pays shown in credits at the current line bet ({lb}). Line wins pay left to right on adjacent reels.", new Vector2(0, 3.35f), 0.2f, Color.white, Ui.Order.Panel);
            var paying = _config.Symbols.Where(s => s.PaysOnLines).OrderByDescending(s => s.Pays[5]).ToList();
            for (int i = 0; i < paying.Count; i++)
            {
                var s = paying[i];
                int col = i < 4 ? 0 : 1;
                int row = col == 0 ? i : i - 4;
                var pos = new Vector2(col == 0 ? -6.2f : 1.4f, 2.4f - row * 1.25f);
                var sym = new SymbolView(_page, _config, 1.05f);
                sym.Root.transform.localPosition = pos;
                sym.Set(s.Id);
                SetOrders(sym.Root, Ui.Order.Panel);
                var sb = new StringBuilder();
                sb.Append(s.Name.ToUpperInvariant()).Append('\n');
                for (int n = 5; n >= 3; n--) sb.Append(n).Append("x  ").Append(Ui.Credits(s.Pays[n] * lb)).Append("   ");
                Ui.Text("Pays", _page, sb.ToString(), pos + new Vector2(0.8f, 0), 0.26f, Color.white, Ui.Order.Panel, TextAnchor.MiddleLeft);
            }
        }

        private void FeaturesPage()
        {
            var fs = _config.FreeSpins;
            var hs = _config.HoldAndSpin;
            long tb = _lineBet() * _config.LineCount;
            var specials = new[] { Symbol.Wild, Symbol.Scatter, Symbol.Orb };
            string[] text =
            {
                $"{_config.Info(Symbol.Wild).Name.ToUpperInvariant()} (WILD) - reels 2, 3 and 4 only. Substitutes for all symbols\nexcept {_config.Info(Symbol.Scatter).Name} and {_config.Info(Symbol.Orb).Name}.",
                $"{_config.Info(Symbol.Scatter).Name.ToUpperInvariant()} (SCATTER) - pays anywhere: 3 = {_config.ScatterPays[3] * tb}, 4 = {_config.ScatterPays[4] * tb}, 5 = {_config.ScatterPays[5] * tb}.\n3 / 4 / 5 award {fs.Awards[3]} / {fs.Awards[4]} / {fs.Awards[5]} FREE SPINS, retriggerable. Multiplier starts at {fs.StartMultiplier}x\nand rises by {fs.MultiplierStep} after every winning free spin (max {fs.MaxMultiplier}x).",
                $"{_config.Info(Symbol.Orb).Name.ToUpperInvariant()} - {hs.TriggerCount}+ in the base game start ORBIT LOCK: {hs.Respins} respins, orbs hold,\neach new orb resets respins to {hs.Respins}. Orbs carry credits or MINI {hs.MiniMultiple * tb} / MINOR {hs.MinorMultiple * tb} / MAJOR {hs.MajorMultiple * tb}.\nFill all 15 positions for the GRAND {hs.GrandMultiple * tb}.",
            };
            for (int i = 0; i < specials.Length; i++)
            {
                var pos = new Vector2(-7f, 2.4f - i * 2.2f);
                var sym = new SymbolView(_page, _config, 1.3f);
                sym.Root.transform.localPosition = pos;
                sym.Set(specials[i], specials[i] == Symbol.Orb ? "MINI" : null);
                SetOrders(sym.Root, Ui.Order.Panel);
                Ui.Text("Rule", _page, text[i], pos + new Vector2(1.0f, 0), 0.24f, Color.white, Ui.Order.Panel, TextAnchor.MiddleLeft);
            }
            Ui.Text("Footer", _page, "All outcomes are decided by the RNG before the reels move. Malfunction voids all pays and plays.\nDemo only - fake credits, no real money.", new Vector2(0, -3.1f), 0.2f, new Color(1, 1, 1, 0.6f), Ui.Order.Panel);
        }

        private void LinesPage()
        {
            Ui.Text("Note", _page, $"{_config.LineCount} fixed paylines", new Vector2(0, 3.35f), 0.24f, Color.white, Ui.Order.Panel);
            const float cell = 0.17f;
            for (int i = 0; i < _config.Paylines.Length; i++)
            {
                var line = _config.Paylines[i];
                int col = i % 5, row = i / 5;
                var origin = new Vector2(-6f + col * 3f, 2.4f - row * 1.25f);
                Ui.Text("No", _page, (i + 1).ToString(), origin + new Vector2(-0.75f, 0), 0.22f, Color.gray, Ui.Order.Panel);
                for (int r = 0; r < GameConfig.Reels; r++)
                    for (int y = 0; y < GameConfig.Rows; y++)
                    {
                        bool on = line.Rows[r] == y;
                        Ui.Box("c", _page, origin + new Vector2((r - 2) * cell * 1.15f, (1 - y) * cell * 1.15f), new Vector2(cell, cell),
                            on ? Color.HSVToRGB((i * 0.13f) % 1f, 0.6f, 1f) : new Color(1, 1, 1, 0.12f), Ui.Order.Panel, false);
                    }
            }
        }

        private static void SetOrders(GameObject go, int order)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.sortingOrder = order + (r.sortingOrder - Ui.Order.Symbols + 1);
        }
    }

    /// <summary>Last 100 rounds from <see cref="GameHistory"/>, ten per page, with the selected round's screen.</summary>
    public sealed class HistoryPanel : Panel
    {
        private const int PerPage = 10;
        private readonly GameSession _session;
        private readonly TextMesh _table;
        private readonly TextMesh _detail;
        private readonly TextMesh _pageLabel;
        private int _page;

        public HistoryPanel(Transform parent, GameSession session) : base(parent, "GAME HISTORY (LAST 100)", new Vector2(16.5f, 8.6f))
        {
            _session = session;
            _table = Ui.Text("Table", Root, "", new Vector2(-7.8f, 3.3f), 0.24f, Color.white, Ui.Order.Panel, TextAnchor.UpperLeft);
            _detail = Ui.Text("Detail", Root, "", new Vector2(3.0f, 3.3f), 0.22f, new Color(0.8f, 0.9f, 1f), Ui.Order.Panel, TextAnchor.UpperLeft);
            _pageLabel = Ui.Text("PageNo", Root, "", new Vector2(0, -3.85f), 0.22f, Color.gray, Ui.Order.Panel);
            Buttons.Add(new UiButton(Root, "NEWER", new Vector2(-1.8f, -3.85f), new Vector2(1.4f, 0.5f), new Color(0.2f, 0.3f, 0.6f), Ui.Order.Panel + 2, () => Flip(-1), 0.2f));
            Buttons.Add(new UiButton(Root, "OLDER", new Vector2(1.8f, -3.85f), new Vector2(1.4f, 0.5f), new Color(0.2f, 0.3f, 0.6f), Ui.Order.Panel + 2, () => Flip(1), 0.2f));
            session.History.Added += _ => { if (IsOpen) Refresh(); };
        }

        private int PageCount => Math.Max(1, (_session.History.Count + PerPage - 1) / PerPage);

        private void Flip(int d)
        {
            _page = Mathf.Clamp(_page + d, 0, PageCount - 1);
            Refresh();
        }

        public override void Refresh()
        {
            var h = _session.History;
            var sb = new StringBuilder();
            sb.AppendLine("  #      TIME      BET   STOPS            WIN   FEATURES");
            int start = _page * PerPage;
            for (int i = start; i < Math.Min(h.Count, start + PerPage); i++)
            {
                var e = h[i];
                sb.AppendLine($"{e.RoundNumber,5}  {e.Time:HH:mm:ss}  {e.TotalBet,4}  {string.Join(",", e.BaseStops),-14}  {e.TotalWin,6}  {e.FeatureSummary}{(e.AutoPlay ? " [A]" : "")}");
            }
            if (h.Count == 0) sb.AppendLine("\n  No rounds played yet.");
            _table.text = sb.ToString();
            _pageLabel.text = $"{_page + 1} / {PageCount}";
            _detail.text = h.Count > 0 ? Detail(h[start < h.Count ? start : 0]) : "";
        }

        private string Detail(HistoryEntry e)
        {
            var cfg = _session.Config;
            var sb = new StringBuilder();
            sb.AppendLine($"ROUND {e.RoundNumber}  {e.Time:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Credits {e.CreditsBefore} -> {e.CreditsAfter}");
            sb.AppendLine($"Bet {e.TotalBet}   Win {e.TotalWin}   {e.BigWinTier}");
            sb.AppendLine($"Lines {e.LineWin}   Scatter {e.ScatterWin}");
            if (e.FreeSpinsPlayed > 0) sb.AppendLine($"Free spins {e.FreeSpinsPlayed}, x{e.FreeSpinFinalMultiplier}, won {e.FreeSpinsWin}");
            if (e.HoldAndSpinOrbs > 0) sb.AppendLine($"Orbit Lock {e.HoldAndSpinOrbs} orbs, won {e.HoldAndSpinWin}");
            sb.AppendLine();
            sb.AppendLine("Base screen:");
            for (int row = 0; row < GameConfig.Rows; row++)
            {
                for (int r = 0; r < GameConfig.Reels; r++) sb.Append(cfg.Info(e.BaseGrid[Grid.CellIndex(r, row)]).Code.PadRight(5));
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }

    /// <summary>Auto-play setup: round count and stop conditions.</summary>
    public sealed class AutoPlayPanel : Panel
    {
        private readonly GameSession _session;
        private readonly Action _started;
        private readonly UiButton _feature, _single, _loss, _gain;
        private bool _stopOnFeature = true, _stopOnSingle = true, _stopOnLoss = true, _stopOnGain;

        public AutoPlayPanel(Transform parent, GameSession session, Action started) : base(parent, "AUTO PLAY", new Vector2(10f, 6.8f))
        {
            _session = session;
            _started = started;
            int[] counts = { 10, 25, 50, 100 };
            for (int i = 0; i < counts.Length; i++)
            {
                int n = counts[i];
                Buttons.Add(new UiButton(Root, n + " SPINS", new Vector2(-3.3f + i * 2.2f, 1.9f), new Vector2(2.0f, 0.8f), new Color(0.15f, 0.45f, 0.35f), Ui.Order.Panel + 2, () => Start(n), 0.26f));
            }
            Ui.Text("Stops", Root, "STOP CONDITIONS", new Vector2(0, 0.95f), 0.24f, Color.gray, Ui.Order.Panel);
            _feature = Toggle(new Vector2(0, 0.3f), () => _stopOnFeature = !_stopOnFeature);
            _single = Toggle(new Vector2(0, -0.5f), () => _stopOnSingle = !_stopOnSingle);
            _loss = Toggle(new Vector2(0, -1.3f), () => _stopOnLoss = !_stopOnLoss);
            _gain = Toggle(new Vector2(0, -2.1f), () => _stopOnGain = !_stopOnGain);
        }

        private UiButton Toggle(Vector2 pos, Action flip)
        {
            UiButton b = null;
            b = new UiButton(Root, "", pos, new Vector2(8.6f, 0.62f), new Color(0.15f, 0.2f, 0.4f), Ui.Order.Panel + 2, () => { flip(); Refresh(); }, 0.22f);
            Buttons.Add(b);
            return b;
        }

        private static string Mark(bool on) => on ? "[X] " : "[  ] ";

        public override void Refresh()
        {
            long bet = _session.TotalBet;
            _feature.SetText(Mark(_stopOnFeature) + "Stop when a feature triggers");
            _single.SetText(Mark(_stopOnSingle) + $"Stop on a single win of {Ui.Credits(bet * 50)}+ (50x bet)");
            _loss.SetText(Mark(_stopOnLoss) + $"Stop if credits fall by {Ui.Credits(bet * 40)} (40x bet)");
            _gain.SetText(Mark(_stopOnGain) + $"Stop if credits rise by {Ui.Credits(bet * 100)} (100x bet)");
        }

        private void Start(int rounds)
        {
            long bet = _session.TotalBet;
            var settings = new AutoPlaySettings
            {
                Rounds = rounds,
                StopOnFeature = _stopOnFeature,
                StopOnSingleWinAtLeast = _stopOnSingle ? bet * 50 : 0,
                LossLimit = _stopOnLoss ? bet * 40 : 0,
                StopOnBalanceIncreaseOf = _stopOnGain ? bet * 100 : 0,
            };
            if (_session.StartAutoPlay(settings))
            {
                Close();
                _started?.Invoke();
            }
        }
    }
}
