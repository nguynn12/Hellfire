using Hellfire.Player;
using UnityEngine;

namespace Hellfire.Player
{
    /// <summary>
    /// Component quản lý toàn bộ hiệu ứng thị giác chuyển động của người chơi (Slide Dust & Sprint Speed Feel).
    /// Hoàn toàn độc lập, gắn trực tiếp trên Prefab thử nghiệm cá nhân.
    /// Tự động sinh Particle System nếu chưa gán prefab ngoài.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerMovementVFX : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerSlide _playerSlide;
        [SerializeField] private PlayerCrouchSlide _playerCrouchSlide;
        [SerializeField] private PlayerMovement _playerMovement;
        [SerializeField] private ParticleSystem _slideDustParticles;

        [Header("Slide Dust Settings")]
        [SerializeField] private Vector3 _dustOffset = new Vector3(0f, 0.1f, -0.4f);
        [SerializeField] private int _dustEmissionRate = 25;

        private void Reset()
        {
            Initialize();
        }

        private void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (_playerSlide == null) _playerSlide = GetComponent<PlayerSlide>();
            if (_playerCrouchSlide == null) _playerCrouchSlide = GetComponent<PlayerCrouchSlide>();
            if (_playerMovement == null) _playerMovement = GetComponent<PlayerMovement>();

            EnsureSlideDustParticleSystem();
        }

        private void Update()
        {
            UpdateSlideDustEffect();
        }

        private void UpdateSlideDustEffect()
        {
            bool isSliding = (_playerSlide != null && _playerSlide.IsSliding) || 
                            (_playerCrouchSlide != null && _playerCrouchSlide.IsSliding);

            if (_slideDustParticles != null)
            {
                var emission = _slideDustParticles.emission;
                if (isSliding)
                {
                    if (!_slideDustParticles.isPlaying)
                    {
                        _slideDustParticles.Play();
                    }
                    emission.rateOverTime = _dustEmissionRate;
                }
                else
                {
                    emission.rateOverTime = 0;
                    if (_slideDustParticles.isPlaying && _slideDustParticles.particleCount == 0)
                    {
                        _slideDustParticles.Stop();
                    }
                }
            }
        }

        private void EnsureSlideDustParticleSystem()
        {
            if (_slideDustParticles != null) return;

            // Tự động khởi tạo Particle System dưới chân người chơi
            var dustObj = new GameObject("SlideDust_Particles");
            dustObj.transform.SetParent(transform);
            dustObj.transform.localPosition = _dustOffset;
            dustObj.transform.localRotation = Quaternion.Euler(0, 180, 0); // Phun ngược về sau lưng

            _slideDustParticles = dustObj.AddComponent<ParticleSystem>();
            var main = _slideDustParticles.main;
            main.startLifetime = 0.4f;
            main.startSpeed = 2.5f;
            main.startSize = 0.35f;
            main.startColor = new Color(0.75f, 0.7f, 0.65f, 0.6f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;

            var emission = _slideDustParticles.emission;
            emission.rateOverTime = 0;

            var shape = _slideDustParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.2f;

            var renderer = dustObj.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default"));
        }
    }
}
