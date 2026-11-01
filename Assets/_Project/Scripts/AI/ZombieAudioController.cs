using System.Collections;
using Hellfire.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace Hellfire.AI
{
    /// <summary>
    /// Component quản lý toàn bộ hệ thống âm thanh 3D của Zombie (Aggro, Bước chân, Bị thương, Chết).
    /// Hỗ trợ nạp AudioClip ngoài hoặc tự động sinh âm thanh procedurally nếu chưa có file asset.
    /// Hoàn toàn độc lập, gắn trực tiếp trên Prefab Zombie cá nhân.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    [RequireComponent(typeof(Health))]
    public class ZombieAudioController : MonoBehaviour
    {
        [Header("Audio Clips (Tùy chọn gán file WAV/MP3)")]
        [SerializeField] private AudioClip _aggroSound;
        [SerializeField] private AudioClip _hurtSound;
        [SerializeField] private AudioClip _deathSound;
        [SerializeField] private AudioClip _footstepSound;

        [Header("3D Audio Balancing")]
        [SerializeField] private float _minDistance = 2.0f;
        [SerializeField] private float _maxDistance = 25.0f;
        [SerializeField] private float _footstepInterval = 0.55f;

        private AudioSource _audioSource;
        private Health _health;
        private NavMeshAgent _navAgent;
        private float _lastFootstepTime;
        private bool _hasAggroPlayed = false;

        private void Reset()
        {
            InitializeAudioSource();
        }

        private void Awake()
        {
            InitializeAudioSource();
        }

        public void InitializeAudioSource()
        {
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
            if (_health == null) _health = GetComponent<Health>();
            if (_navAgent == null) _navAgent = GetComponent<NavMeshAgent>();

            if (_audioSource != null)
            {
                _audioSource.spatialBlend = 1.0f; // 100% 3D Sound
                _audioSource.minDistance = _minDistance;
                _audioSource.maxDistance = _maxDistance;
                _audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
                _audioSource.playOnAwake = false;
            }

            EnsureFallbackAudioClips();
        }

        private void OnEnable()
        {
            if (_health != null)
            {
                _health.OnDamageTaken += HandleHurt;
                _health.OnDied += HandleDeath;
            }
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.OnDamageTaken -= HandleHurt;
                _health.OnDied -= HandleDeath;
            }
        }

        private void Update()
        {
            if (_health != null && _health.IsDead.Value) return;

            // 1. m thanh bước chân khi di chuyển
            if (_navAgent != null && _navAgent.isOnNavMesh && _navAgent.velocity.sqrMagnitude > 0.2f)
            {
                if (Time.time - _lastFootstepTime >= _footstepInterval)
                {
                    _lastFootstepTime = Time.time;
                    PlaySound(_footstepSound, 0.4f, Random.Range(0.85f, 1.15f));
                }
            }

            // 2. Phát tiếng gầm Aggro khi phát hiện người chơi lần đầu
            if (!_hasAggroPlayed)
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null && Vector3.Distance(transform.position, player.transform.position) <= 15f)
                {
                    TriggerAggroSound();
                }
            }
        }

        public void TriggerAggroSound()
        {
            if (_hasAggroPlayed) return;
            _hasAggroPlayed = true;
            PlaySound(_aggroSound, 0.9f, Random.Range(0.8f, 1.0f));
        }

        private void HandleHurt(float damage, HitboxType hitbox, ulong attackerId)
        {
            if (_health != null && _health.IsDead.Value) return;
            PlaySound(_hurtSound, 0.75f, Random.Range(0.9f, 1.2f));
        }

        private void HandleDeath(ulong killerId)
        {
            PlaySound(_deathSound, 1.0f, Random.Range(0.75f, 0.95f));
        }

        private void PlaySound(AudioClip clip, float volume, float pitch)
        {
            if (clip == null) return;
            _audioSource.pitch = pitch;
            _audioSource.PlayOneShot(clip, volume);
        }

        private void EnsureFallbackAudioClips()
        {
            if (_aggroSound == null) _aggroSound = CreateToneClip("Zombie_Aggro_Tone", 140f, 0.6f);
            if (_hurtSound == null) _hurtSound = CreateToneClip("Zombie_Hurt_Tone", 220f, 0.25f);
            if (_deathSound == null) _deathSound = CreateToneClip("Zombie_Death_Tone", 95f, 0.9f);
            if (_footstepSound == null) _footstepSound = CreateToneClip("Zombie_Step_Tone", 80f, 0.08f);
        }

        private static AudioClip CreateToneClip(string name, float freq, float duration)
        {
            int sampleRate = 22050;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = 1f - (t / duration); // decay envelope
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope * 0.4f;
            }
            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
