using UnityEngine;

namespace BehaviorEditor
{
    /// <summary>
    /// AnimationTrack 使用的动画片段播放抽象层。
    /// </summary>
    public interface IBehaviorAnimationPlayer
    {
        bool Initialize(Animator animator);
        bool TryPlaySegment(AnimationSegment segment, int slotIndex, float crossFadeDurationOverride, out string stateName);
    }
}
