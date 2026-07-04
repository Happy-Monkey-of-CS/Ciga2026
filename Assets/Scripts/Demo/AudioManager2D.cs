using System.Collections.Generic;
using UnityEngine;

namespace Ciga.Demo
{
    /// <summary>
    /// Persistent singleton that manages one-shot and looping audio playback
    /// through a pool of AudioSources, avoiding per-object AudioSource clutter.
    /// </summary>
    public sealed class AudioManager2D : MonoBehaviour
    {
        [Header("Pool")]
        [SerializeField] private int poolSize = 16;
        [SerializeField] private bool persistAcrossScenes = true;

        private static AudioManager2D instance;
        private List<AudioSource> pool;
        private int nextSourceIndex;
        private Dictionary<string, AudioSource> loopingSounds;

        public static AudioManager2D Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<AudioManager2D>();
                }
                return instance;
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            if (persistAcrossScenes)
            {
                DontDestroyOnLoad(gameObject);
            }

            BuildPool();
            loopingSounds = new Dictionary<string, AudioSource>();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void BuildPool()
        {
            pool = new List<AudioSource>(poolSize);
            for (int i = 0; i < poolSize; i++)
            {
                GameObject child = new GameObject($"AudioSource_{i}");
                child.transform.SetParent(transform, false);
                AudioSource source = child.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f; // 2D by default
                pool.Add(source);
            }
        }

        // ---- one-shot -----------------------------------------------------------------

        /// <summary>Play a one-shot clip. Returns the AudioSource used (or null if none available).</summary>
        public AudioSource PlayOneShot(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return null;

            AudioSource source = GetAvailableSource();
            if (source == null) return null;

            PrepareSource(source, Vector3.zero, 0f);
            source.clip = clip;
            source.volume = volume;
            source.pitch = pitch;
            source.loop = false;
            source.Play();
            return source;
        }

        /// <summary>Play a one-shot clip at a world position (uses 3D/spatial blend).</summary>
        public AudioSource PlayOneShotAt(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f, float spatialBlend = 1f)
        {
            if (clip == null) return null;

            AudioSource source = GetAvailableSource();
            if (source == null) return null;

            PrepareSource(source, position, spatialBlend);
            source.clip = clip;
            source.volume = volume;
            source.pitch = pitch;
            source.loop = false;
            source.Play();
            return source;
        }

        // ---- looping ------------------------------------------------------------------

        /// <summary>Start a looping sound identified by a key. Stops any existing loop with the same key.</summary>
        public AudioSource StartLoop(string key, AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return null;

            // Stop existing loop with same key
            StopLoop(key);

            AudioSource source = GetAvailableSource();
            if (source == null) return null;

            PrepareSource(source, Vector3.zero, 0f);
            source.clip = clip;
            source.volume = volume;
            source.pitch = pitch;
            source.loop = true;
            source.Play();

            loopingSounds[key] = source;
            return source;
        }

        /// <summary>Stop a looping sound identified by key.</summary>
        public void StopLoop(string key)
        {
            if (!loopingSounds.TryGetValue(key, out AudioSource source)) return;

            if (source != null)
            {
                source.Stop();
                source.clip = null;
                PrepareSource(source, Vector3.zero, 0f);
            }

            loopingSounds.Remove(key);
        }

        /// <summary>True if a looping sound with the given key is currently playing.</summary>
        public bool IsLoopPlaying(string key)
        {
            return loopingSounds.TryGetValue(key, out AudioSource source)
                && source != null
                && source.isPlaying;
        }

        // ---- helpers ------------------------------------------------------------------

        private AudioSource GetAvailableSource()
        {
            if (pool == null || pool.Count == 0) return null;

            // First pass: look for a free source
            int start = nextSourceIndex;
            for (int i = 0; i < pool.Count; i++)
            {
                int index = (start + i) % pool.Count;
                AudioSource source = pool[index];
                if (source != null && !source.isPlaying && source.clip == null)
                {
                    nextSourceIndex = (index + 1) % pool.Count;
                    return source;
                }
            }

            // Second pass: steal the oldest looping source if none free
            for (int i = 0; i < pool.Count; i++)
            {
                int index = (start + i) % pool.Count;
                AudioSource source = pool[index];
                if (source != null && !source.isPlaying)
                {
                    nextSourceIndex = (index + 1) % pool.Count;
                    source.Stop();
                    source.clip = null;
                    return source;
                }
            }

            // Last resort: steal a playing source
            AudioSource fallback = pool[nextSourceIndex];
            if (fallback != null)
            {
                fallback.Stop();
                fallback.clip = null;
            }

            int result = nextSourceIndex;
            nextSourceIndex = (nextSourceIndex + 1) % pool.Count;
            return pool[result];
        }

        // ---- debug --------------------------------------------------------------------

        public void StopAll()
        {
            foreach (KeyValuePair<string, AudioSource> kvp in loopingSounds)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.Stop();
                    kvp.Value.clip = null;
                }
            }

            loopingSounds.Clear();

            if (pool != null)
            {
                foreach (AudioSource source in pool)
                {
                    if (source != null)
                    {
                        source.Stop();
                        source.clip = null;
                        PrepareSource(source, Vector3.zero, 0f);
                    }
                }
            }
        }

        private static void PrepareSource(AudioSource source, Vector3 position, float spatialBlend)
        {
            source.transform.position = position;
            source.spatialBlend = spatialBlend;
            source.loop = false;
            source.pitch = 1f;
            source.volume = 1f;
            source.panStereo = 0f;
            source.outputAudioMixerGroup = null;
        }
    }
}
