// Script: PlayerAnimationBridge.cs
// Mục đích: Tương thích ngược (Backward Compatibility) với PlayerAnimationSync.

namespace Hellfire.Player
{
    [System.Obsolete("Sử dụng PlayerAnimationSync thay thế.")]
    public class PlayerAnimationBridge : PlayerAnimationSync
    {
    }
}
