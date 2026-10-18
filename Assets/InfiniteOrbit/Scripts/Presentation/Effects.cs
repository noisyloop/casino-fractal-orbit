using InfiniteOrbit.Core;
using UnityEngine;

namespace InfiniteOrbit.Presentation
{
    public enum EffectsQuality
    {
        Low,
        Medium,
        High,
    }

    /// <summary>Persisted visual quality setting for lower-end cabinets.</summary>
    public static class QualitySetting
    {
        private const string Key = "InfiniteOrbit.EffectsQuality";

        public static EffectsQuality Current
        {
            get
            {
                try { return (EffectsQuality)Mathf.Clamp(PlayerPrefs.GetInt(Key, (int)EffectsQuality.High), 0, 2); }
                catch { return EffectsQuality.High; }
            }
            set
            {
                PlayerPrefs.SetInt(Key, (int)value);
                PlayerPrefs.Save();
            }
        }

        public static int MaxParticles(EffectsQuality q) => q == EffectsQuality.Low ? 60 : q == EffectsQuality.Medium ? 250 : 800;
        public static float EmissionScale(EffectsQuality q) => q == EffectsQuality.Low ? 0.2f : q == EffectsQuality.Medium ? 0.5f : 1f;
        public static int FractalIterations(EffectsQuality q) => q == EffectsQuality.Low ? 20 : q == EffectsQuality.Medium ? 40 : 72;
    }

    /// <summary>
    /// Full-screen fractal quad. Intensity eases toward a target picked from the game state: calm in Idle,
    /// faster and brighter during spins, wins and features, red-shifted in tilt.
    /// </summary>
    public sealed class FractalBackground : MonoBehaviour
    {
        private Material _material;
        private float _intensity = 0.1f;
        private float _target = 0.1f;
        private float _time;
        private float _hue = 0.6f;
        private float _hueTarget = 0.6f;
        private Color _tint = Color.white;
        private Color _tintTarget = Color.white;
        private float _pulse;
        private Camera _camera;

        public static FractalBackground Create(Camera camera)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Fractal Background";
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var bg = go.AddComponent<FractalBackground>();
            bg._camera = camera;
            var shader = Resources.Load<Shader>("InfiniteOrbit/FractalBackground");
            var mr = go.GetComponent<MeshRenderer>();
            bg._material = new Material(shader != null ? shader : Shader.Find("Unlit/Color"));
            mr.sharedMaterial = bg._material;
            mr.sortingOrder = Ui.Order.Background;
            bg.ApplyQuality(QualitySetting.Current);
            return bg;
        }

        public void ApplyQuality(EffectsQuality q) => _material.SetFloat("_Iterations", QualitySetting.FractalIterations(q));

        /// <summary>Short brightness kick (reel stops, orb landings).</summary>
        public void Pulse(float amount) => _pulse = Mathf.Max(_pulse, amount);

        public void SetMood(GameState state, FeatureKind feature)
        {
            _tintTarget = Color.white;
            switch (state)
            {
                case GameState.Idle: _target = 0.12f; _hueTarget = 0.6f; break;
                case GameState.Spinning: _target = 0.3f; break;
                case GameState.WinPresentation: _target = 0.5f; break;
                case GameState.BonusIntro: _target = 0.75f; _hueTarget = feature == FeatureKind.HoldAndSpin ? 0.45f : 0.85f; break;
                case GameState.FreeSpins: _target = 0.7f; _hueTarget = 0.85f; break;
                case GameState.HoldAndSpin: _target = 0.65f; _hueTarget = 0.45f; break;
                case GameState.BigWin: _target = 1f; _hueTarget = 0.1f; break;
                case GameState.Error: _target = 0.05f; _tintTarget = new Color(1f, 0.25f, 0.25f); break;
            }
        }

        /// <summary>Lets celebrations push intensity above the state baseline (e.g. Big -> Mega -> Epic).</summary>
        public void SetIntensityOverride(float value) => _target = Mathf.Max(_target, value);

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            _intensity = Mathf.MoveTowards(_intensity, _target, dt * 0.6f);
            _hue = Mathf.Lerp(_hue, _hueTarget, dt * 0.8f);
            _tint = Color.Lerp(_tint, _tintTarget, dt * 3f);
            _pulse = Mathf.MoveTowards(_pulse, 0, dt * 2f);
            float shown = Mathf.Clamp01(_intensity + _pulse * 0.4f);
            _time += dt * (0.25f + 2.2f * shown);

            _material.SetFloat("_Intensity", shown);
            _material.SetFloat("_TimeOffset", _time);
            _material.SetFloat("_Hue", _hue);
            _material.SetColor("_Tint", _tint);

            if (_camera != null)
            {
                float h = _camera.orthographicSize * 2f;
                float w = h * _camera.aspect;
                transform.position = new Vector3(_camera.transform.position.x, _camera.transform.position.y, 10f);
                transform.localScale = new Vector3(w, h, 1);
                _material.SetFloat("_Aspect", _camera.aspect);
            }
        }
    }

    /// <summary>Win particles built from Unity's ParticleSystem. Counts scale with the quality setting.</summary>
    public sealed class WinParticles : MonoBehaviour
    {
        private ParticleSystem _burst;
        private ParticleSystem _fountain;
        private EffectsQuality _quality;

        public static WinParticles Create(Transform parent, Vector2 pos)
        {
            var go = Ui.Node("Win Particles", parent, pos);
            var wp = go.AddComponent<WinParticles>();
            wp._burst = MakeSystem(go.transform, "Burst", false);
            wp._fountain = MakeSystem(go.transform, "Fountain", true);
            wp.ApplyQuality(QualitySetting.Current);
            return wp;
        }

        private static ParticleSystem MakeSystem(Transform parent, string name, bool fountain)
        {
            var go = Ui.Node(name, parent, fountain ? new Vector2(0, -3.5f) : Vector2.zero);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = fountain;
            main.duration = fountain ? 1f : 0.5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(fountain ? 1.6f : 0.8f, fountain ? 2.4f : 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(fountain ? 6f : 2f, fountain ? 9f : 7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.3f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.3f), new Color(0.4f, 0.9f, 1f));
            main.gravityModifier = fountain ? 1.2f : 0.3f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0;

            var shape = ps.shape;
            shape.enabled = true;
            if (fountain)
            {
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 25f;
                shape.radius = 3f;
                shape.rotation = new Vector3(-90f, 0, 0);
            }
            else
            {
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.5f;
            }

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = Art.Additive;
            renderer.sortingOrder = Ui.Order.Particles;
            return ps;
        }

        public void ApplyQuality(EffectsQuality q)
        {
            _quality = q;
            var a = _burst.main;
            a.maxParticles = QualitySetting.MaxParticles(q);
            var b = _fountain.main;
            b.maxParticles = QualitySetting.MaxParticles(q);
        }

        /// <summary>One-shot burst at a world position; strength 0..1.</summary>
        public void Burst(Vector2 world, float strength)
        {
            _burst.transform.position = new Vector3(world.x, world.y, 0);
            int count = Mathf.Max(3, Mathf.RoundToInt(80 * strength * QualitySetting.EmissionScale(_quality)));
            _burst.Emit(count);
        }

        /// <summary>Continuous coin fountain for big wins; level 0 stops it.</summary>
        public void Fountain(float level)
        {
            var emission = _fountain.emission;
            emission.rateOverTime = level * 220f * QualitySetting.EmissionScale(_quality);
            if (level > 0 && !_fountain.isPlaying) _fountain.Play();
            if (level <= 0 && _fountain.isPlaying) _fountain.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
