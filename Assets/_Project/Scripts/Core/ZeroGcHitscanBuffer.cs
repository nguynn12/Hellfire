using UnityEngine;

namespace Hellfire.Combat
{
    /// <summary>
    /// Bộ đệm Raycast và Collider tĩnh dùng chung, ngăn chặn cấp phát bộ nhớ Heap (0 Bytes GC Alloc)
    /// cho toàn bộ vòng lặp bắn súng, kiểm tra va chạm cận chiến và quét vụ nổ.
    /// </summary>
    public static class ZeroGcHitscanBuffer
    {
        public const int MAX_HITS = 16;
        public static readonly RaycastHit[] HitBuffer = new RaycastHit[MAX_HITS];
        public static readonly Collider[] OverlapBuffer = new Collider[MAX_HITS];
    }
}

