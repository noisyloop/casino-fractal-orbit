using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using InfiniteOrbit.Core;
using InfiniteOrbit.GameMath;
using UnityEngine;
using Grid = InfiniteOrbit.GameMath.Grid;

namespace InfiniteOrbit.Presentation
{
    /// <summary>
    /// Presentation root. Builds the cabinet, listens to <see cref="GameSession"/> state changes and plays each
    /// phase of the pre-decided <see cref="RoundResult"/>. When a phase finishes it calls
    /// <see cref="GameSession.Advance"/>. Nothing here can change an outcome: it only reads results.
    ///
    /// Keys: Space spin/skip, Up/Down bet, M max bet, A auto-play, I paytable, H history, Q quality,
    /// T simulate tilt (door open), C attendant clear, F1 add demo credits, F12 screenshot to docs/screenshots.
    /// </summary>
    public sealed class GameDirector : MonoBehaviour
    {
        public const long StartingCredits = 10_000;
        private const float MainViewportSplit = 0.78f;

        private GameSession _session;
        private GameConfig _config;
        private Camera _camera;
        private ReelsView _reels;
        private TopScreen _top;
        private AudioHooks _audio;
        private FractalBackground _background;
        private WinParticles _particles;
        private PaytablePanel _paytable;
        private HistoryPanel _history;
        private AutoPlayPanel _autoPanel;

        private TextMesh _creditMeter, _betMeter, _winMeter, _message, _featureInfo, _celebration, _celebrationAmount, _tiltText;
        private GameObject _tiltOverlay;
        private UiButton _spinButton, _betDown, _betUp, _maxBet, _autoButton, _infoButton, _historyButton, _qualityButton;
        private readonly SymbolView[] _lockCells = new SymbolView[GameConfig.Cells];
        private readonly SpriteRenderer[] _lockSlots = new SpriteRenderer[GameConfig.Cells];

        private Coroutine _phase;
        private bool _skipRequested;
        private long _displayedWin;
        private float _autoDelay;

        // ------------------------------------------------------------------ bootstrap

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (FindAnyObjectByType<GameDirector>() != null) return;
            new GameObject("Infinite Orbit").AddComponent<GameDirector>();
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            ButtonHub.Clear();
            _config = InfiniteOrbitMath.CreateConfig();
            ulong seed = (ulong)DateTime.UtcNow.Ticks;
            _session = new GameSession(_config, new Xoshiro256StarStar(seed), StartingCredits);

            BuildCamera();
            _audio = gameObject.AddComponent<AudioHooks>();
            _background = FractalBackground.Create(_camera);
            _top = new TopScreen(_config, MainViewportSplit);
            if (_top.OnSecondDisplay) _camera.rect = new Rect(0, 0, 1, 1);

            var root = Ui.Node("Cabinet", transform, Vector2.zero).transform;
            _reels = new ReelsView(root, _config, new Vector2(0, 0.55f), 1.75f, 1.5f);
            BuildLockGrid(root);
            BuildHud(root);
            _particles = WinParticles.Create(root, Vector2.zero);
            _paytable = new PaytablePanel(root, _config, () => _session.LineBet);
            _history = new HistoryPanel(root, _session);
            _autoPanel = new AutoPlayPanel(root, _session, () => _audio.Play(SoundHook.ButtonPress));
            BuildTiltOverlay(root);

            _reels.Show(_config.BaseReels, new int[GameConfig.Reels]);

            _session.StateChanged += OnStateChanged;
            _session.MetersChanged += RefreshMeters;
            _session.AutoPlayStopped += r => SetMessage(r == AutoPlayStopReason.Completed ? "AUTO PLAY COMPLETE" : "AUTO PLAY STOPPED: " + Pretty(r));
            RefreshMeters();
            SetMessage("PRESS SPIN TO PLAY  -  DEMO CREDITS ONLY");
            OnStateChanged(GameState.Idle, GameState.Idle);
        }

        private void BuildCamera()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                _camera = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            _camera.orthographic = true;
            _camera.orthographicSize = 4.6f;
            _camera.transform.position = new Vector3(0, 0, -10);
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.black;
            _camera.rect = new Rect(0, 0, 1, MainViewportSplit);
        }

        private void BuildHud(Transform root)
        {
            float y = -3.55f;
            var panelColor = new Color(0.03f, 0.04f, 0.1f, 0.92f);
            Ui.Box("HUD", root, new Vector2(0, -3.75f), new Vector2(19f, 1.75f), panelColor, Ui.Order.Hud - 2, false);

            Meter(root, "CREDIT", new Vector2(-7.4f, y), out _creditMeter);
            Meter(root, "BET", new Vector2(-4.1f, y), out _betMeter);
            Meter(root, "WIN", new Vector2(4.3f, y), out _winMeter);

            _betDown = new UiButton(root, "-", new Vector2(-2.7f, y), new Vector2(0.6f, 0.6f), new Color(0.2f, 0.3f, 0.6f), Ui.Order.Hud, () => { _session.BetDown(); _audio.Play(SoundHook.ButtonPress); }, 0.4f);
            _betUp = new UiButton(root, "+", new Vector2(-1.95f, y), new Vector2(0.6f, 0.6f), new Color(0.2f, 0.3f, 0.6f), Ui.Order.Hud, () => { _session.BetUp(); _audio.Play(SoundHook.ButtonPress); }, 0.4f);
            _maxBet = new UiButton(root, "MAX BET", new Vector2(-0.6f, y), new Vector2(1.6f, 0.75f), new Color(0.6f, 0.35f, 0.1f), Ui.Order.Hud, MaxBet, 0.24f);
            _spinButton = new UiButton(root, "SPIN", new Vector2(1.6f, y), new Vector2(2.2f, 1.1f), new Color(0.1f, 0.55f, 0.35f), Ui.Order.Hud, SpinPressed, 0.42f);
            _autoButton = new UiButton(root, "AUTO", new Vector2(6.6f, y), new Vector2(1.3f, 0.6f), new Color(0.25f, 0.25f, 0.5f), Ui.Order.Hud, AutoPressed, 0.22f);
            _infoButton = new UiButton(root, "PAYTABLE", new Vector2(8.15f, y + 0.37f), new Vector2(1.6f, 0.5f), new Color(0.2f, 0.3f, 0.45f), Ui.Order.Hud, () => { _paytable.Toggle(); _audio.Play(SoundHook.ButtonPress); }, 0.19f);
            _historyButton = new UiButton(root, "HISTORY", new Vector2(8.15f, y - 0.25f), new Vector2(1.6f, 0.5f), new Color(0.2f, 0.3f, 0.45f), Ui.Order.Hud, () => { _history.Toggle(); _audio.Play(SoundHook.ButtonPress); }, 0.19f);
            _qualityButton = new UiButton(root, "", new Vector2(8.15f, -2.45f), new Vector2(1.6f, 0.4f), new Color(0.15f, 0.15f, 0.25f), Ui.Order.Hud, CycleQuality, 0.15f);
            _qualityButton.SetText("FX: " + QualitySetting.Current.ToString().ToUpperInvariant());

            _message = Ui.Text("Message", root, "", new Vector2(0, -2.6f), 0.26f, new Color(0.85f, 0.9f, 1f), Ui.Order.Hud);
            _featureInfo = Ui.Text("Feature Info", root, "", new Vector2(0, 3.55f), 0.34f, new Color(1f, 0.9f, 0.5f), Ui.Order.Hud);
            _celebration = Ui.Text("Celebration", root, "", new Vector2(0, 1.2f), 1.1f, Color.white, Ui.Order.Overlay);
            _celebrationAmount = Ui.Text("Celebration Amount", root, "", new Vector2(0, -0.2f), 0.8f, new Color(1f, 0.9f, 0.4f), Ui.Order.Overlay);
        }

        private static void Meter(Transform root, string label, Vector2 pos, out TextMesh value)
        {
            Ui.Box(label + " Meter", root, pos, new Vector2(2.9f, 0.95f), new Color(0, 0, 0, 0.6f), Ui.Order.Hud - 1);
            Ui.Text(label, root, label, pos + new Vector2(0, 0.25f), 0.2f, new Color(0.6f, 0.75f, 1f), Ui.Order.Hud);
            value = Ui.Text(label + " Value", root, "0", pos + new Vector2(0, -0.12f), 0.38f, Color.white, Ui.Order.Hud);
        }

        private void BuildLockGrid(Transform root)
        {
            for (int c = 0; c < GameConfig.Cells; c++)
            {
                var p = _reels.Center + _reels.CellCenter(Grid.ReelOf(c), Grid.RowOf(c));
                _lockSlots[c] = Ui.Box("Lock Slot", root, p, new Vector2(_reels.CellW - 0.12f, _reels.CellH - 0.12f), new Color(0.1f, 0.25f, 0.4f, 0.5f), Ui.Order.Symbols - 2);
                _lockCells[c] = new SymbolView(root, _config, Mathf.Min(_reels.CellW, _reels.CellH));
                _lockCells[c].SetPosition(p);
                _lockSlots[c].gameObject.SetActive(false);
                _lockCells[c].SetVisible(false);
            }
        }

        private void BuildTiltOverlay(Transform root)
        {
            _tiltOverlay = Ui.Node("Tilt Overlay", root, Vector2.zero);
            Ui.Box("Shade", _tiltOverlay.transform, Vector2.zero, new Vector2(60, 40), new Color(0.25f, 0, 0, 0.82f), Ui.Order.Tilt, false);
            Ui.Box("Plate", _tiltOverlay.transform, Vector2.zero, new Vector2(12f, 4.2f), new Color(0.5f, 0.02f, 0.05f, 1f), Ui.Order.Tilt + 1);
            Ui.Text("Title", _tiltOverlay.transform, "MACHINE TILT", new Vector2(0, 1.2f), 0.75f, Color.white, Ui.Order.Tilt + 2);
            _tiltText = Ui.Text("Reason", _tiltOverlay.transform, "", new Vector2(0, 0.1f), 0.38f, new Color(1f, 0.85f, 0.85f), Ui.Order.Tilt + 2);
            Ui.Text("Help", _tiltOverlay.transform, "PLAY SUSPENDED - CALL ATTENDANT\n(press C to clear)", new Vector2(0, -1.1f), 0.26f, new Color(1, 1, 1, 0.8f), Ui.Order.Tilt + 2);
            _tiltOverlay.SetActive(false);
        }

        // ------------------------------------------------------------------ input

        private void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                Vector2 world = _camera.ScreenToWorldPoint(Input.mousePosition);
                if (!ButtonHub.Click(world) && _session.State != GameState.Idle) _skipRequested = true;
            }

            if (Input.GetKeyDown(KeyCode.T) && _session.State != GameState.Error) _session.Tilt("DOOR OPEN");
            if (Input.GetKeyDown(KeyCode.C)) _session.ClearTilt();
            if (_session.State == GameState.Error) return;

            if (Input.GetKeyDown(KeyCode.Space)) SpinPressed();
            if (Input.GetKeyDown(KeyCode.UpArrow)) _session.BetUp();
            if (Input.GetKeyDown(KeyCode.DownArrow)) _session.BetDown();
            if (Input.GetKeyDown(KeyCode.M)) MaxBet();
            if (Input.GetKeyDown(KeyCode.A)) AutoPressed();
            if (Input.GetKeyDown(KeyCode.I)) _paytable.Toggle();
            if (Input.GetKeyDown(KeyCode.H)) _history.Toggle();
            if (Input.GetKeyDown(KeyCode.Q)) CycleQuality();
            if (Input.GetKeyDown(KeyCode.F1)) _session.AddCredits(StartingCredits);
            if (Input.GetKeyDown(KeyCode.F12)) CaptureScreenshot();

            if (_session.AutoPlayWantsSpin && !AnyPanelOpen)
            {
                _autoDelay -= Time.deltaTime;
                if (_autoDelay <= 0) Spin();
            }

            _top.Celebrate(_session.State == GameState.BigWin ? 1f : _session.State == GameState.WinPresentation ? 0.3f : 0f);
            RefreshButtons();
        }

        private static void CaptureScreenshot()
        {
            string dir = System.IO.Path.Combine(Application.dataPath, "..", "docs", "screenshots");
            System.IO.Directory.CreateDirectory(dir);
            string path = System.IO.Path.Combine(dir, $"infinite-orbit-{DateTime.Now:yyyyMMdd-HHmmss}.png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("Screenshot saved to " + path);
        }

        private bool AnyPanelOpen => _paytable.IsOpen || _history.IsOpen || _autoPanel.IsOpen;

        private void SpinPressed()
        {
            if (_session.State == GameState.Idle)
            {
                if (_session.AutoPlay.Active) _session.StopAutoPlay();
                else Spin();
            }
            else
            {
                _skipRequested = true;
            }
        }

        private void Spin()
        {
            if (AnyPanelOpen) return;
            var rejection = _session.RequestSpin();
            if (rejection == SpinRejection.InsufficientCredits) SetMessage("INSUFFICIENT CREDITS  -  LOWER BET OR PRESS F1 FOR DEMO CREDITS");
            _autoDelay = 0.6f;
        }

        private void MaxBet()
        {
            if (_session.State != GameState.Idle || AnyPanelOpen || _session.AutoPlay.Active) return;
            _audio.Play(SoundHook.ButtonPress);
            _session.SetBetLevel(_session.MaxBetLevel);
            Spin();
        }

        private void AutoPressed()
        {
            _audio.Play(SoundHook.ButtonPress);
            if (_session.AutoPlay.Active) _session.StopAutoPlay();
            else if (_session.State == GameState.Idle) _autoPanel.Toggle();
        }

        private void CycleQuality()
        {
            var q = (EffectsQuality)(((int)QualitySetting.Current + 1) % 3);
            QualitySetting.Current = q;
            _background.ApplyQuality(q);
            _particles.ApplyQuality(q);
            _qualityButton.SetText("FX: " + q.ToString().ToUpperInvariant());
        }

        private void RefreshButtons()
        {
            bool idle = _session.State == GameState.Idle;
            bool auto = _session.AutoPlay.Active;
            _spinButton.SetText(idle ? (auto ? "STOP" : "SPIN") : "SKIP");
            _spinButton.Enabled = !idle || auto || _session.CanSpin;
            _betDown.Enabled = _session.CanChangeBet && _session.BetLevel > 0;
            _betUp.Enabled = _session.CanChangeBet && _session.BetLevel < _session.MaxBetLevel;
            _maxBet.Enabled = idle && !auto && _session.Credits >= _config.TotalBetForLevel(_session.MaxBetLevel);
            _autoButton.SetText(auto ? $"STOP {_session.AutoPlay.RoundsRemaining}" : "AUTO");
            _autoButton.Highlighted = auto;
            _historyButton.Enabled = true;
            _infoButton.Enabled = true;
        }

        private void RefreshMeters()
        {
            _creditMeter.text = Ui.Credits(_session.Credits);
            _betMeter.text = Ui.Credits(_session.TotalBet);
            if (_session.State == GameState.Idle) SetWinMeter(_session.WinMeter);
            _top.SetBet(_session.TotalBet);
        }

        private void SetWinMeter(long value)
        {
            _displayedWin = value;
            _winMeter.text = Ui.Credits(value);
        }

        private void SetMessage(string text) => _message.text = text ?? "";

        private static string Pretty(AutoPlayStopReason r)
        {
            switch (r)
            {
                case AutoPlayStopReason.FeatureTriggered: return "FEATURE TRIGGERED";
                case AutoPlayStopReason.SingleWinLimit: return "SINGLE WIN LIMIT";
                case AutoPlayStopReason.LossLimit: return "LOSS LIMIT";
                case AutoPlayStopReason.BalanceIncreaseLimit: return "BALANCE TARGET";
                case AutoPlayStopReason.InsufficientCredits: return "INSUFFICIENT CREDITS";
                case AutoPlayStopReason.Tilt: return "TILT";
                default: return "PLAYER";
            }
        }

        // ------------------------------------------------------------------ state dispatch

        private void OnStateChanged(GameState from, GameState to)
        {
            _background.SetMood(to, _session.ActiveFeature);

            if (to == GameState.Error)
            {
                _tiltText.text = _session.TiltMessage;
                _tiltOverlay.SetActive(true);
                _audio.Loop(null);
                _audio.Play(SoundHook.Tilt);
                return;
            }
            if (from == GameState.Error)
            {
                // Resuming after an attendant clear: the frozen phase coroutine simply continues.
                _tiltOverlay.SetActive(false);
                return;
            }

            if (_phase != null) StopCoroutine(_phase);
            _skipRequested = false;
            switch (to)
            {
                case GameState.Idle: _phase = StartCoroutine(IdlePhase()); break;
                case GameState.Spinning: _phase = StartCoroutine(SpinPhase()); break;
                case GameState.BonusIntro: _phase = StartCoroutine(BonusIntroPhase()); break;
                case GameState.FreeSpins: _phase = StartCoroutine(FreeSpinsPhase()); break;
                case GameState.HoldAndSpin: _phase = StartCoroutine(HoldAndSpinPhase()); break;
                case GameState.BigWin: _phase = StartCoroutine(BigWinPhase()); break;
                case GameState.WinPresentation: _phase = StartCoroutine(WinPresentationPhase()); break;
            }
        }

        /// <summary>Waits <paramref name="seconds"/> of un-tilted time; optionally cut short by a skip request.</summary>
        private IEnumerator Wait(float seconds, bool skippable = true)
        {
            float t = 0;
            while (t < seconds)
            {
                if (_session.State != GameState.Error) t += Time.deltaTime;
                if (skippable && _skipRequested) yield break;
                yield return null;
            }
        }

        private IEnumerator WhileTilted()
        {
            while (_session.State == GameState.Error) yield return null;
        }

        private void Advance()
        {
            _phase = null;
            _session.Advance();
        }

        private IEnumerator IdlePhase()
        {
            _reels.ClearHighlights();
            _featureInfo.text = "";
            _celebration.text = "";
            _celebrationAmount.text = "";
            _particles.Fountain(0);
            _top.SetBanner("");
            RefreshMeters();
            yield break;
        }

        // ------------------------------------------------------------------ spinning

        private IEnumerator SpinPhase()
        {
            var round = _session.CurrentRound;
            SetMessage(_session.AutoPlay.Active ? $"AUTO PLAY  -  {_session.AutoPlay.RoundsRemaining} LEFT" : "GOOD LUCK!");
            SetWinMeter(0);
            yield return SpinReelsTo(_config.BaseReels, round.BaseSpin);
            Advance();
        }

        /// <summary>Spins all reels and lands them on the stops in <paramref name="spin"/>, with scatter anticipation.</summary>
        private IEnumerator SpinReelsTo(ReelSet reels, SpinResult spin)
        {
            _reels.ClearHighlights();
            _audio.Play(SoundHook.ReelSpinStart);
            for (int r = 0; r < GameConfig.Reels; r++)
            {
                _reels.Columns[r].SetStrip(reels[r], 0);
                _reels.Columns[r].Spinning = true;
            }

            float spinTime = 0;
            float baseTime = 0.55f;
            float speed = 22f;
            int scattersSoFar = 0;

            for (int stopping = 0; stopping < GameConfig.Reels; stopping++)
            {
                bool anticipate = scattersSoFar >= 2 && stopping >= 2;
                float hold = (stopping == 0 ? baseTime : 0.18f) + (anticipate ? 1.1f : 0f);
                if (anticipate)
                {
                    _reels.SetAnticipation(stopping, 0.25f);
                    _audio.Loop(SoundHook.ScatterAnticipation, 1f + 0.1f * stopping);
                }

                float t = 0;
                while (t < hold && !_skipRequested)
                {
                    yield return WhileTilted();
                    float dt = Time.deltaTime;
                    t += dt;
                    spinTime += dt;
                    for (int r = stopping; r < GameConfig.Reels; r++) _reels.Columns[r].Tick(dt, anticipate && r == stopping ? speed * 1.4f : speed);
                    if (anticipate) _reels.SetAnticipation(stopping, 0.2f + 0.15f * Mathf.Sin(spinTime * 20f));
                    yield return null;
                }

                // Remaining reels keep spinning while this one lands.
                var land = StartCoroutine(_reels.Columns[stopping].LandOn(spin.Stops[stopping], _skipRequested ? 1f : 3f, _skipRequested ? 0.12f : 0.32f));
                float landT = 0;
                float landDur = _skipRequested ? 0.12f : 0.32f;
                while (landT < landDur)
                {
                    yield return WhileTilted();
                    landT += Time.deltaTime;
                    for (int r = stopping + 1; r < GameConfig.Reels; r++) _reels.Columns[r].Tick(Time.deltaTime, speed);
                    yield return null;
                }
                yield return land;
                _reels.Columns[stopping].SetStrip(reels[stopping], spin.Stops[stopping], ReelsView.OrbLabels(spin.Orbs, stopping));
                _reels.SetAnticipation(stopping, 0);
                _audio.Loop(null);
                _audio.Play(SoundHook.ReelStop, 0.95f + 0.04f * stopping);
                _background.Pulse(0.2f);

                for (int row = 0; row < GameConfig.Rows; row++)
                {
                    var s = spin.Grid[Grid.CellIndex(stopping, row)];
                    if (s == Symbol.Scatter)
                    {
                        scattersSoFar++;
                        _audio.Play(SoundHook.ScatterLand, 1f + 0.12f * scattersSoFar);
                        _particles.Burst(_reels.Center + _reels.CellCenter(stopping, row), 0.3f);
                    }
                    else if (s == Symbol.Orb)
                    {
                        _audio.Play(SoundHook.OrbLand, 1f + 0.05f * stopping, 0.6f);
                    }
                }
            }
            _skipRequested = false;
        }

        // ------------------------------------------------------------------ wins

        private IEnumerator WinPresentationPhase()
        {
            var round = _session.CurrentRound;
            long total = round.TotalWin;
            SetMessage(round.BigWinTier == BigWinTier.None ? $"WIN {Ui.Credits(total)}" : $"TOTAL WIN {Ui.Credits(total)}");
            _audio.Play(total >= round.TotalBet * 5 ? SoundHook.WinLine : SoundHook.WinSmall);
            _particles.Burst(_reels.Center, Mathf.Clamp01((float)round.WinMultiple / 10f) * 0.8f + 0.2f);

            // Count the win meter up while cycling the base-game line wins.
            float countTime = Mathf.Clamp(0.6f + (float)round.WinMultiple * 0.15f, 0.6f, 2.5f);
            if (round.BigWinTier != BigWinTier.None) countTime = 0.2f;
            var lines = round.BaseSpin.LineWins;
            float t = 0;
            int shown = -1;
            float lineTime = 0.9f;
            float duration = Mathf.Max(countTime, Mathf.Min(lines.Count, 6) * lineTime) + 0.4f;
            while (t < duration && !_skipRequested)
            {
                yield return WhileTilted();
                t += Time.deltaTime;
                long value = (long)Mathf.Lerp(0, total, Mathf.Clamp01(t / countTime));
                if (value != _displayedWin && Time.frameCount % 3 == 0) _audio.Play(SoundHook.CoinTick, 1f, 0.3f);
                SetWinMeter(value);

                if (lines.Count > 0)
                {
                    int idx = (int)(t / lineTime) % lines.Count;
                    var w = lines[idx];
                    if (idx != shown)
                    {
                        shown = idx;
                        SetMessage($"LINE {w.LineIndex + 1}:  {w.Count} x {_config.Info(w.Symbol).Name.ToUpperInvariant()}  PAYS {Ui.Credits(w.Credits)}");
                    }
                    _reels.HighlightLine(w, 0.5f + 0.5f * Mathf.Sin(t * 10f));
                }
                else if (round.BaseSpin.ScatterCredits > 0)
                {
                    _reels.HighlightCells(round.BaseSpin.ScatterCells, 0.5f + 0.5f * Mathf.Sin(t * 10f), Art.SymbolColor(Symbol.Scatter));
                }
                yield return null;
            }
            SetWinMeter(total);
            SetMessage($"WIN {Ui.Credits(total)}");
            _reels.ClearHighlights();
            Advance();
        }

        private IEnumerator BigWinPhase()
        {
            var round = _session.CurrentRound;
            long total = round.TotalWin;
            var big = _config.BigWin;
            var tiers = new[] { BigWinTier.Big, BigWinTier.Mega, BigWinTier.Epic };
            double[] thresholds = { big.Big, big.Mega, big.Epic };
            var colors = new[] { new Color(0.4f, 0.9f, 1f), new Color(1f, 0.55f, 1f), new Color(1f, 0.75f, 0.2f) };
            var sounds = new[] { SoundHook.BigWin, SoundHook.MegaWin, SoundHook.EpicWin };

            int finalTier = Array.IndexOf(tiers, round.BigWinTier);
            float duration = 3.5f + 2.5f * finalTier;
            int currentTier = -1;
            float t = 0;
            while (t < duration)
            {
                yield return WhileTilted();
                if (_skipRequested) t = duration;
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / duration);
                float eased = 1f - Mathf.Pow(1f - u, 2.2f);
                long value = (long)(total * (double)eased);
                SetWinMeter(value);
                _celebrationAmount.text = Ui.Credits(value);

                int tier = 0;
                for (int i = 0; i <= finalTier; i++) if (value >= thresholds[i] * round.TotalBet) tier = i;
                if (tier != currentTier)
                {
                    currentTier = tier;
                    string name = tiers[tier].ToString().ToUpperInvariant() + " WIN";
                    _celebration.text = name;
                    _top.SetBanner(name + "!", colors[tier]);
                    _audio.Play(sounds[tier]);
                    _particles.Burst(_reels.Center, 1f);
                    _background.SetIntensityOverride(0.8f + 0.1f * tier);
                }
                float pulse = 1f + (0.05f + 0.04f * currentTier) * Mathf.Sin(t * (8f + 4f * currentTier));
                _celebration.transform.localScale = Vector3.one * pulse;
                _celebration.color = Color.Lerp(colors[currentTier], Color.white, 0.3f + 0.3f * Mathf.Sin(t * 12f));
                _particles.Fountain(0.4f + 0.3f * currentTier);
                if (Time.frameCount % 4 == 0) _audio.Play(SoundHook.CoinTick, 1f + 0.1f * currentTier, 0.25f);
                yield return null;
            }
            SetWinMeter(total);
            _celebrationAmount.text = Ui.Credits(total);
            _skipRequested = false;
            yield return Wait(1.2f);
            _particles.Fountain(0);
            _celebration.text = "";
            _celebrationAmount.text = "";
            _celebration.transform.localScale = Vector3.one;
            Advance();
        }

        // ------------------------------------------------------------------ features

        private IEnumerator BonusIntroPhase()
        {
            var round = _session.CurrentRound;
            bool hs = _session.ActiveFeature == FeatureKind.HoldAndSpin;
            _audio.Play(SoundHook.FeatureEntry);
            _particles.Burst(_reels.Center, 0.8f);

            var cells = hs ? round.BaseSpin.Orbs.Select(o => o.Cell).ToList() : round.BaseSpin.ScatterCells;
            string title = hs ? "ORBIT LOCK" : "FREE SPINS";
            string sub = hs
                ? $"{round.BaseSpin.Orbs.Count} ORBS LOCKED  -  {_config.HoldAndSpin.Respins} RESPINS"
                : $"{round.FreeSpins.InitialSpins} FREE SPINS  -  MULTIPLIER GROWS ON EVERY WIN";
            _top.SetBanner(title + "!");
            float t = 0;
            while (t < 2.6f && !_skipRequested)
            {
                yield return WhileTilted();
                t += Time.deltaTime;
                _reels.HighlightCells(cells, 0.5f + 0.5f * Mathf.Sin(t * 12f), hs ? Art.SymbolColor(Symbol.Orb) : Art.SymbolColor(Symbol.Scatter));
                _celebration.text = t > 0.5f ? title : "";
                _celebrationAmount.text = t > 0.9f ? sub : "";
                _celebrationAmount.characterSize = 0.32f / (Ui.FontSize * Ui.TextScale);
                yield return null;
            }
            _celebration.text = "";
            _celebrationAmount.text = "";
            _celebrationAmount.characterSize = 0.8f / (Ui.FontSize * Ui.TextScale);
            _reels.ClearHighlights();
            Advance();
        }

        private IEnumerator FreeSpinsPhase()
        {
            var round = _session.CurrentRound;
            var fs = round.FreeSpins;
            long baseWin = round.BaseSpin.TotalCredits + (round.HoldAndSpin?.TotalCredits ?? 0);
            int total = fs.InitialSpins;

            foreach (var step in fs.Spins)
            {
                _featureInfo.text = $"FREE SPIN {step.Number} OF {total}     MULTIPLIER x{step.Multiplier}     FEATURE WIN {Ui.Credits(step.CumulativeCredits - step.Spin.TotalCredits)}";
                yield return SpinReelsTo(_config.FreeSpinReels, step.Spin);

                if (step.Spin.TotalCredits > 0)
                {
                    _audio.Play(SoundHook.WinLine, 1f + 0.03f * step.Multiplier);
                    _particles.Burst(_reels.Center, 0.4f + 0.05f * step.Multiplier);
                    SetMessage($"WIN {Ui.Credits(step.Spin.TotalCredits)}  (x{step.Multiplier})");
                    float t = 0;
                    var lines = step.Spin.LineWins;
                    while (t < 1.1f && !_skipRequested)
                    {
                        yield return WhileTilted();
                        t += Time.deltaTime;
                        if (lines.Count > 0) _reels.HighlightLine(lines[(int)(t / 0.55f) % lines.Count], 0.5f + 0.5f * Mathf.Sin(t * 10f));
                        SetWinMeter(baseWin + (long)Mathf.Lerp(step.CumulativeCredits - step.Spin.TotalCredits, step.CumulativeCredits, Mathf.Clamp01(t / 0.8f)));
                        yield return null;
                    }
                    _reels.ClearHighlights();
                    if (step.Multiplier < _config.FreeSpins.MaxMultiplier)
                        _featureInfo.text = $"MULTIPLIER UP!  x{step.Multiplier + 1}";
                }
                SetWinMeter(baseWin + step.CumulativeCredits);

                if (step.SpinsAdded > 0)
                {
                    total += step.SpinsAdded;
                    _audio.Play(SoundHook.FreeSpinRetrigger);
                    _top.SetBanner($"+{step.SpinsAdded} FREE SPINS!");
                    _celebration.text = $"+{step.SpinsAdded} SPINS";
                    yield return Wait(1.4f);
                    _celebration.text = "";
                }
                _skipRequested = false;
                yield return Wait(0.35f);
            }

            _featureInfo.text = $"FREE SPINS COMPLETE  -  {fs.Spins.Count} SPINS  -  WON {Ui.Credits(fs.TotalCredits)}";
            _top.SetBanner($"FREE SPINS WIN {Ui.Credits(fs.TotalCredits)}");
            yield return Wait(1.8f);
            _reels.Show(_config.BaseReels, round.BaseSpin.Stops, round.BaseSpin.Orbs);
            Advance();
        }

        private IEnumerator HoldAndSpinPhase()
        {
            var round = _session.CurrentRound;
            var hs = round.HoldAndSpin;
            long before = round.BaseSpin.TotalCredits;

            _reels.SetVisible(false);
            for (int c = 0; c < GameConfig.Cells; c++)
            {
                _lockSlots[c].gameObject.SetActive(true);
                _lockCells[c].SetVisible(false);
            }
            foreach (var orb in hs.InitialOrbs) ShowLockedOrb(orb);

            int respins = _config.HoldAndSpin.Respins;
            _featureInfo.text = $"ORBIT LOCK     RESPINS {respins}";
            yield return Wait(1.0f, false);

            foreach (var step in hs.Respins)
            {
                // Flicker the empty cells as their single-position reels spin.
                float t = 0;
                while (t < 0.7f)
                {
                    yield return WhileTilted();
                    t += Time.deltaTime;
                    for (int c = 0; c < GameConfig.Cells; c++)
                        if (!_lockCells[c].Root.activeSelf) _lockSlots[c].color = new Color(0.1f, 0.25f, 0.4f, 0.35f + 0.3f * Mathf.PerlinNoise(c * 3.1f, t * 9f));
                    yield return null;
                }
                _audio.Play(SoundHook.RespinTick);
                foreach (var orb in step.NewOrbs)
                {
                    ShowLockedOrb(orb);
                    _audio.Play(SoundHook.OrbLand, 1f + 0.05f * Grid.ReelOf(orb.Cell));
                    _particles.Burst(_reels.Center + _reels.CellCenter(Grid.ReelOf(orb.Cell), Grid.RowOf(orb.Cell)), 0.35f);
                    _background.Pulse(0.5f);
                    yield return Wait(0.15f, false);
                }
                _featureInfo.text = step.NewOrbs.Count > 0
                    ? $"ORBIT LOCK     RESPINS RESET TO {step.RespinsRemaining}"
                    : $"ORBIT LOCK     RESPINS {step.RespinsRemaining}";
                yield return Wait(step.NewOrbs.Count > 0 ? 0.6f : 0.35f, false);
            }

            // Collect: tally each orb into the win meter, flashing jackpots on the top screen.
            _featureInfo.text = hs.FullScreen ? "FULL SCREEN!  GRAND JACKPOT!" : "COLLECTING";
            long running = before;
            for (int c = 0; c < GameConfig.Cells; c++)
            {
                var orb = hs.Board[c];
                if (orb == null) continue;
                running += orb.Credits;
                _lockCells[c].SetGlow(0.8f, Art.SymbolColor(Symbol.Orb));
                SetWinMeter(running);
                _audio.Play(orb.Prize.IsJackpot ? SoundHook.JackpotWin : SoundHook.CoinTick);
                if (orb.Prize.IsJackpot) yield return _top.FlashJackpot(orb.Prize.Kind, 1.5f);
                else yield return Wait(0.12f, false);
            }
            if (hs.FullScreen)
            {
                running += _config.HoldAndSpin.GrandMultiple * round.TotalBet;
                SetWinMeter(running);
                _audio.Play(SoundHook.JackpotWin, 0.8f);
                _particles.Fountain(1f);
                yield return _top.FlashJackpot(OrbPrizeKind.Grand, 3f);
                _particles.Fountain(0);
            }
            _top.SetBanner($"ORBIT LOCK WIN {Ui.Credits(hs.TotalCredits)}");
            yield return Wait(1.5f);

            for (int c = 0; c < GameConfig.Cells; c++)
            {
                _lockSlots[c].gameObject.SetActive(false);
                _lockCells[c].SetVisible(false);
            }
            _reels.Show(_config.BaseReels, round.BaseSpin.Stops, round.BaseSpin.Orbs);
            _featureInfo.text = "";
            Advance();
        }

        private void ShowLockedOrb(OrbCell orb)
        {
            var view = _lockCells[orb.Cell];
            view.SetVisible(true);
            view.Set(Symbol.Orb, ReelsView.OrbText(orb));
            view.SetGlow(orb.Prize.IsJackpot ? 0.6f : 0.25f, Art.SymbolColor(Symbol.Orb));
            _lockSlots[orb.Cell].color = new Color(0.15f, 0.4f, 0.6f, 0.7f);
        }
    }
}
