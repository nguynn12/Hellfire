using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Hellfire.Vfx
{
    [RequireComponent(typeof(ParticleSystemRenderer))]
    public sealed class EnvironmentParticleDepth : MonoBehaviour
    {
        [SerializeField] private Material _soft;
        [SerializeField] private Material _fallback;

        private void OnEnable()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            GetComponent<ParticleSystemRenderer>().sharedMaterial = pipeline != null && pipeline.supportsCameraDepthTexture
                ? _soft : _fallback;
        }
    }
}
