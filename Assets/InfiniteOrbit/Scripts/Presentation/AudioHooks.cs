using System.Collections.Generic;
using UnityEngine;

namespace InfiniteOrbit.Presentation
{
    /// <summary>Every sound the game can trigger. Clip file names in Resources/InfiniteOrbit/Audio match these names.</summary>
    public enum SoundHook
    {
        ButtonPress,
        ReelSpinStart,
        ReelStop,
        ScatterLand,
        ScatterAnticipation,
        OrbLand,
        WinSmall,
        WinLine,
        FeatureEntry,
        FreeSpinRetrigger,
        RespinTick,
        JackpotWin,
        BigWin,
        MegaWin,
        EpicWin,
        CoinTick,
        Tilt,
    }

    /// <summary>
    /// Sound hook dispatcher. To replace a placeholder, drop an AudioClip named after the hook (e.g. "ReelStop.wav")
    /// into Assets/InfiniteOrbit/Resources/InfiniteOrbit/Audio/. Missing clips fall back to synthesized tones.
    /// </summary>
    public sealed class AudioHooks : MonoBehaviour
    {
        private const int SampleRate = 44100;
        private readonly Dictionary<SoundHook, AudioClip> _clips = new Dictionary<SoundHook, AudioClip>();
        private AudioSource[] _voices;
        private AudioSource _loop;
        private int _nextVoice;

        public float Volume { get; set; } = 0.6f;

        private void Awake()
        {
            _voices = new AudioSource[8];
            for (int i = 0; i < _voices.Length; i++)
            {
                _voices[i] = gameObject.AddComponent<AudioSource>();
                _voices[i].playOnAwake = false;
            }
            _loop = gameObject.AddComponent<AudioSource>();
            _loop.loop = true;
            _loop.playOnAwake = false;
        }

        public void Play(SoundHook hook, float pitch = 1f, float volume = 1f)
        {
            var src = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;
            src.pitch = pitch;
            src.PlayOneShot(Clip(hook), Volume * volume);
        }

        /// <summary>Looping hook (e.g. anticipation hum). Pass null to stop.</summary>
        public void Loop(SoundHook? hook, float pitch = 1f)
        {
            if (hook == null)
            {
                _loop.Stop();
                return;
            }
            var clip = Clip(hook.Value);
            if (_loop.clip != clip || !_loop.isPlaying)
            {
                _loop.clip = clip;
                _loop.Play();
            }
            _loop.pitch = pitch;
            _loop.volume = Volume * 0.5f;
        }

        public AudioClip Clip(SoundHook hook)
        {
            if (_clips.TryGetValue(hook, out var clip)) return clip;
            clip = Resources.Load<AudioClip>("InfiniteOrbit/Audio/" + hook);
            if (clip == null) clip = Synthesize(hook);
            _clips[hook] = clip;
            return clip;
        }

        // --------------------------------------------------------------- placeholder synthesis

        private static AudioClip Synthesize(SoundHook hook)
        {
            switch (hook)
            {
                case SoundHook.ButtonPress: return Tone(hook, 0.05f, 880, 880, 0.3f, Wave.Square);
                case SoundHook.ReelSpinStart: return Tone(hook, 0.25f, 200, 600, 0.35f, Wave.Saw);
                case SoundHook.ReelStop: return Tone(hook, 0.09f, 140, 70, 0.6f, Wave.Noise);
                case SoundHook.ScatterLand: return Arp(hook, new[] { 660f, 990f, 1320f }, 0.07f, 0.5f);
                case SoundHook.ScatterAnticipation: return Tone(hook, 1.0f, 110, 110, 0.35f, Wave.Tremolo);
                case SoundHook.OrbLand: return Tone(hook, 0.25f, 520, 1040, 0.45f, Wave.Sine);
                case SoundHook.WinSmall: return Arp(hook, new[] { 523f, 659f }, 0.08f, 0.4f);
                case SoundHook.WinLine: return Arp(hook, new[] { 523f, 659f, 784f }, 0.07f, 0.4f);
                case SoundHook.FeatureEntry: return Arp(hook, new[] { 392f, 523f, 659f, 784f, 1046f }, 0.11f, 0.55f);
                case SoundHook.FreeSpinRetrigger: return Arp(hook, new[] { 784f, 988f, 1175f, 1568f }, 0.08f, 0.5f);
                case SoundHook.RespinTick: return Tone(hook, 0.06f, 300, 300, 0.25f, Wave.Square);
                case SoundHook.JackpotWin: return Arp(hook, new[] { 523f, 784f, 1046f, 1318f, 1568f, 2093f }, 0.1f, 0.6f);
                case SoundHook.BigWin: return Arp(hook, new[] { 262f, 330f, 392f, 523f }, 0.15f, 0.6f);
                case SoundHook.MegaWin: return Arp(hook, new[] { 330f, 415f, 494f, 659f, 831f }, 0.13f, 0.7f);
                case SoundHook.EpicWin: return Arp(hook, new[] { 392f, 494f, 587f, 784f, 988f, 1175f, 1568f }, 0.11f, 0.8f);
                case SoundHook.CoinTick: return Tone(hook, 0.04f, 1800, 2200, 0.2f, Wave.Sine);
                case SoundHook.Tilt: return Tone(hook, 0.8f, 440, 220, 0.5f, Wave.Square);
                default: return Tone(hook, 0.1f, 440, 440, 0.3f, Wave.Sine);
            }
        }

        private enum Wave { Sine, Square, Saw, Noise, Tremolo }

        private static AudioClip Tone(SoundHook name, float seconds, float f0, float f1, float amp, Wave wave)
        {
            int n = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[n];
            float phase = 0;
            var rand = new System.Random(name.GetHashCode());
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float f = Mathf.Lerp(f0, f1, t);
                phase += f / SampleRate;
                float s;
                switch (wave)
                {
                    case Wave.Square: s = Mathf.Sign(Mathf.Sin(phase * Mathf.PI * 2)) * 0.5f; break;
                    case Wave.Saw: s = (phase % 1f) * 2f - 1f; break;
                    case Wave.Noise: s = (float)(rand.NextDouble() * 2 - 1) * 0.6f + Mathf.Sin(phase * Mathf.PI * 2) * 0.6f; break;
                    case Wave.Tremolo: s = Mathf.Sin(phase * Mathf.PI * 2) * (0.6f + 0.4f * Mathf.Sin(t * seconds * 40f)); break;
                    default: s = Mathf.Sin(phase * Mathf.PI * 2); break;
                }
                float env = Mathf.Min(1f, i / (0.005f * SampleRate)) * (wave == Wave.Tremolo ? 1f : Mathf.Pow(1f - t, 1.5f));
                data[i] = s * amp * env;
            }
            var clip = AudioClip.Create(name.ToString(), n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip Arp(SoundHook name, float[] notes, float noteSeconds, float amp)
        {
            int per = Mathf.CeilToInt(noteSeconds * SampleRate);
            int tail = per * 3;
            var data = new float[per * notes.Length + tail];
            for (int k = 0; k < notes.Length; k++)
            {
                int len = per + tail;
                for (int i = 0; i < len && k * per + i < data.Length; i++)
                {
                    float t = (float)i / SampleRate;
                    float env = Mathf.Min(1f, i / 200f) * Mathf.Exp(-t * 9f);
                    data[k * per + i] += (Mathf.Sin(2 * Mathf.PI * notes[k] * t) + 0.3f * Mathf.Sin(4 * Mathf.PI * notes[k] * t)) * env * amp * 0.6f;
                }
            }
            var clip = AudioClip.Create(name.ToString(), data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
