using System.Collections;
using InfiniteOrbit.GameMath;
using UnityEngine;

namespace InfiniteOrbit.Presentation
{
    /// <summary>
    /// The cabinet's top box: jackpot meters and win celebrations. It lives in its own world region with its own
    /// camera, which renders to a second physical display when one is connected, otherwise to the top strip of
    /// the main display.
    /// </summary>
    public sealed class TopScreen
    {
        public static readonly Vector2 Origin = new Vector2(1000f, 0f);
        public const float Height = 3f;
        public const float Width = 16f;

        public readonly Camera Camera;
        public readonly bool OnSecondDisplay;
        private readonly Transform _root;
        private readonly TextMesh[] _values = new TextMesh[4];
        private readonly SpriteRenderer[] _plates = new SpriteRenderer[4];
        private readonly TextMesh _banner;
        private readonly TextMesh _sub;
        private readonly GameConfig _config;
        private static readonly OrbPrizeKind[] Tiers = { OrbPrizeKind.Grand, OrbPrizeKind.Major, OrbPrizeKind.Minor, OrbPrizeKind.Mini };
        private static readonly Color[] TierColors = { new Color(1f, 0.3f, 0.35f), new Color(1f, 0.65f, 0.2f), new Color(0.6f, 0.4f, 1f), new Color(0.3f, 0.85f, 1f) };

        public TopScreen(GameConfig config, float mainViewportSplit)
        {
            _config = config;
            _root = new GameObject("Top Screen").transform;
            _root.position = new Vector3(Origin.x, Origin.y, 0);

            var camGo = new GameObject("Top Screen Camera");
            Camera = camGo.AddComponent<Camera>();
            Camera.orthographic = true;
            Camera.orthographicSize = Height / 2f;
            Camera.transform.position = new Vector3(Origin.x, Origin.y, -10f);
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = new Color(0.01f, 0.01f, 0.04f);
            Camera.depth = 1;

            OnSecondDisplay = Display.displays.Length > 1;
            if (OnSecondDisplay)
            {
                Display.displays[1].Activate();
                Camera.targetDisplay = 1;
                Camera.rect = new Rect(0, 0, 1, 1);
            }
            else
            {
                Camera.rect = new Rect(0, mainViewportSplit, 1, 1 - mainViewportSplit);
            }

            Ui.Box("Top Frame", _root, Vector2.zero, new Vector2(Width + 4f, Height), new Color(0.03f, 0.03f, 0.1f, 1f), Ui.Order.Cabinet);
            Ui.Text("Logo", _root, "INFINITE  ORBIT", new Vector2(0, 1.05f), 0.5f, new Color(0.75f, 0.9f, 1f), Ui.Order.Hud);

            float plateW = 3.6f;
            for (int i = 0; i < 4; i++)
            {
                float x = (i - 1.5f) * (plateW + 0.3f);
                _plates[i] = Ui.Box(Tiers[i] + " Plate", _root, new Vector2(x, -0.05f), new Vector2(plateW, 1.1f), TierColors[i] * 0.45f, Ui.Order.Cabinet + 1);
                Ui.Text("Name", _plates[i].transform, Tiers[i].ToString().ToUpperInvariant(), new Vector2(0, 0.25f), 0.26f, TierColors[i], Ui.Order.Hud);
                _values[i] = Ui.Text("Value", _plates[i].transform, "", new Vector2(0, -0.18f), 0.36f, Color.white, Ui.Order.Hud);
            }

            _banner = Ui.Text("Banner", _root, "", new Vector2(0, -0.95f), 0.42f, new Color(1f, 0.9f, 0.4f), Ui.Order.Overlay);
            _sub = Ui.Text("Sub", _root, "FAKE CREDITS ONLY  -  DEMO", new Vector2(0, -1.3f), 0.18f, new Color(1, 1, 1, 0.5f), Ui.Order.Hud);
        }

        public static long JackpotValue(GameConfig config, OrbPrizeKind kind, long totalBet) => config.HoldAndSpin.JackpotMultiple(kind) * totalBet;

        public void SetBet(long totalBet)
        {
            for (int i = 0; i < 4; i++) _values[i].text = Ui.Credits(JackpotValue(_config, Tiers[i], totalBet));
        }

        public void SetBanner(string text, Color? color = null)
        {
            _banner.text = text ?? "";
            _banner.color = color ?? new Color(1f, 0.9f, 0.4f);
        }

        public IEnumerator FlashJackpot(OrbPrizeKind kind, float seconds)
        {
            int i = System.Array.IndexOf(Tiers, kind);
            if (i < 0) yield break;
            SetBanner(kind.ToString().ToUpperInvariant() + " JACKPOT!", TierColors[i]);
            float t = 0;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float p = 0.5f + 0.5f * Mathf.Sin(t * 18f);
                _plates[i].color = Color.Lerp(TierColors[i] * 0.45f, Color.white, p * 0.7f);
                _plates[i].transform.localScale = Vector3.one * (1f + 0.05f * p);
                yield return null;
            }
            _plates[i].color = TierColors[i] * 0.45f;
            _plates[i].transform.localScale = Vector3.one;
        }

        public void Celebrate(float level)
        {
            for (int i = 0; i < 4; i++)
            {
                float p = level <= 0 ? 0 : 0.5f + 0.5f * Mathf.Sin(Time.time * (6f + 10f * level) + i);
                _plates[i].color = Color.Lerp(TierColors[i] * 0.45f, TierColors[i], p * level);
            }
        }
    }
}
