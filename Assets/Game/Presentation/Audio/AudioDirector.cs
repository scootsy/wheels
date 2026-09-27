using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tabletop.Presentation
{
    /// <summary>
    /// Music, ambience and sound effects for both scenes (D-033). One instance survives scene loads so music can
    /// cross-fade from the village to the table. Presentation only: nothing here touches rules state.
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        public const float MusicVolume = 0.42f;
        public const float AmbienceVolume = 0.55f;
        public const float SfxVolume = 0.9f;
        private const float FadeSeconds = 2.2f;

        public static AudioDirector Instance { get; private set; }

        /// <summary>Global mute switch (pause menu). Remembered for the session.</summary>
        public static bool Muted
        {
            get => _muted;
            set { _muted = value; AudioListener.volume = value ? 0f : 1f; }
        }

        private static bool _muted;

        private SoundBank _bank;
        private readonly AudioSource[] _music = new AudioSource[2];
        private readonly AudioSource[] _amb = new AudioSource[2];
        private readonly List<AudioSource> _sfx = new List<AudioSource>();
        private int _musicSlot, _ambSlot;
        private string _musicKey, _ambKey;
        private Coroutine _musicFade, _ambFade;

        public string CurrentMusic => _musicKey;
        public string CurrentAmbience => _ambKey;
        public SoundBank Bank => _bank;

        /// <summary>Creates the director on first use (or adopts a newer bank). Safe to call from every scene.</summary>
        public static AudioDirector Ensure(SoundBank bank)
        {
            if (Instance == null)
            {
                var go = new GameObject("AudioDirector");
                DontDestroyOnLoad(go);
                Instance = go.AddComponent<AudioDirector>();
                Instance.Init();
            }
            if (bank != null) Instance._bank = bank;
            Ui.ClickSound = () => Instance?.Sfx("sfx/click", 0.55f);
            return Instance;
        }

        private void Init()
        {
            for (int i = 0; i < 2; i++)
            {
                _music[i] = NewSource("Music" + i, true);
                _amb[i] = NewSource("Ambience" + i, true);
            }
            for (int i = 0; i < 10; i++) _sfx.Add(NewSource("Sfx" + i, false));
        }

        private AudioSource NewSource(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            s.volume = 0f;
            return s;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------ music and ambience

        public void PlayMusic(string key) => CrossFade(key, ref _musicKey, _music, ref _musicSlot, ref _musicFade, MusicVolume);
        public void PlayAmbience(string key) => CrossFade(key, ref _ambKey, _amb, ref _ambSlot, ref _ambFade, AmbienceVolume);

        private void CrossFade(string key, ref string current, AudioSource[] pair, ref int slot, ref Coroutine fade, float volume)
        {
            if (key == current) return;
            current = key;
            var entry = _bank != null && key != null ? _bank.Pick(key) : null;
            if (fade != null) StopCoroutine(fade);
            var from = pair[slot];
            slot = 1 - slot;
            var to = pair[slot];
            if (entry != null)
            {
                to.clip = entry.clip;
                to.volume = 0f;
                to.time = 0f;
                to.Play();
            }
            else to.Stop();
            fade = StartCoroutine(Fade(from, to, entry != null ? volume * entry.volume : 0f));
        }

        private static IEnumerator Fade(AudioSource from, AudioSource to, float target)
        {
            float startFrom = from.volume;
            for (float t = 0; t < FadeSeconds; t += Time.unscaledDeltaTime)
            {
                float k = t / FadeSeconds;
                from.volume = startFrom * (1f - k);
                to.volume = target * k;
                yield return null;
            }
            from.volume = 0f;
            from.Stop();
            to.volume = target;
        }

        // ------------------------------------------------------------------ one-shots

        /// <summary>Plays a sound effect by key (random pitch variation keeps repeats from sounding mechanical).</summary>
        public void Sfx(string key, float volume = 1f, float pitchJitter = 0.06f)
        {
            if (_bank == null) return;
            var e = _bank.Pick(key);
            if (e == null) return;
            var s = FreeSource();
            s.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            s.PlayOneShot(e.clip, SfxVolume * volume * e.volume);
        }

        private AudioSource FreeSource()
        {
            foreach (var s in _sfx) if (!s.isPlaying) return s;
            return _sfx[Random.Range(0, _sfx.Count)];
        }

        public static void Play(string key, float volume = 1f) => Instance?.Sfx(key, volume);
    }
}
