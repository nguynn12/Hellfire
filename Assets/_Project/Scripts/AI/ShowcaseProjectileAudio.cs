using UnityEngine;

namespace Hellfire.AI
{
    public sealed class ShowcaseProjectileAudio : MonoBehaviour
    {
        public AudioClip Launch;
        public AudioClip Impact;
        private ArcherProjectile projectile;
        private void Awake() { projectile = GetComponent<ArcherProjectile>(); }
        private void Start() { if (Launch != null) AudioSource.PlayClipAtPoint(Launch, transform.position, .45f); }
        private void OnDestroy()
        {
            if (Application.isPlaying && projectile != null && projectile.HasHit && Impact != null)
                AudioSource.PlayClipAtPoint(Impact, transform.position, .6f);
        }
    }
}
