using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace BehaviorEditor
{
    /// <summary>
    /// AnimationTrack 使用的 AnimatorOverrideController 片段播放器。
    /// </summary>
    [MovedFrom("BehaviorCore")]
    public class AnimatorSegmentPlayer : MonoBehaviour, IBehaviorAnimationPlayer
    {
        private readonly struct SlotRuntimeInfo
        {
            public readonly string StateName;
            public readonly string PlaceholderName;
            public readonly int StateHash;
            public readonly bool IsUsable;

            public SlotRuntimeInfo(string stateName, string placeholderName, int stateHash, bool isUsable)
            {
                StateName = stateName;
                PlaceholderName = placeholderName;
                StateHash = stateHash;
                IsUsable = isUsable;
            }
        }

        [Header("Bindings")]
        [SerializeField, Tooltip("自定义槽位绑定。留空时会使用默认命名约定：状态 L{layer}_Segment_{slot}，占位动画 L{layer}_Placeholder_{slot}")]
        private AnimatorSegmentSlotBinding[] slotBindings = Array.Empty<AnimatorSegmentSlotBinding>();

        private readonly Dictionary<(int layer, int slotIndex), AnimatorSegmentSlotBinding> bindingMap =
            new Dictionary<(int layer, int slotIndex), AnimatorSegmentSlotBinding>();

        private readonly HashSet<string> placeholderClipNames = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> missingSlots = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<KeyValuePair<AnimationClip, AnimationClip>> overridesBuffer =
            new List<KeyValuePair<AnimationClip, AnimationClip>>();
        private readonly Dictionary<int, int> activeStateHashByLayer = new Dictionary<int, int>();
        private readonly Dictionary<(int layer, int slotIndex), SlotRuntimeInfo> slotRuntimeInfoCache =
            new Dictionary<(int layer, int slotIndex), SlotRuntimeInfo>();
        private readonly Dictionary<string, int> stateHashCache = new Dictionary<string, int>(StringComparer.Ordinal);

        private Animator animator;
        private AnimatorOverrideController overrideController;
        private bool initialized;

        public bool Initialize(Animator targetAnimator)
        {
            animator = targetAnimator;
            initialized = false;
            bindingMap.Clear();
            placeholderClipNames.Clear();
            missingSlots.Clear();
            activeStateHashByLayer.Clear();
            slotRuntimeInfoCache.Clear();
            stateHashCache.Clear();

            if (animator == null)
            {
                Debug.LogWarning("AnimatorSegmentPlayer 初始化失败：Animator 为空。", this);
                return false;
            }

            if (animator.runtimeAnimatorController == null)
            {
                Debug.LogWarning("AnimatorSegmentPlayer 初始化失败：Animator 缺少 RuntimeAnimatorController。", animator);
                return false;
            }

            overrideController = animator.runtimeAnimatorController as AnimatorOverrideController;
            if (overrideController == null)
                overrideController = new AnimatorOverrideController(animator.runtimeAnimatorController);

            animator.runtimeAnimatorController = overrideController;

            overridesBuffer.Clear();
            overrideController.GetOverrides(overridesBuffer);
            for (int i = 0; i < overridesBuffer.Count; i++)
            {
                AnimationClip originalClip = overridesBuffer[i].Key;
                if (originalClip != null)
                    placeholderClipNames.Add(originalClip.name);
            }

            if (slotBindings != null)
            {
                for (int i = 0; i < slotBindings.Length; i++)
                {
                    AnimatorSegmentSlotBinding binding = slotBindings[i];
                    if (binding == null)
                        continue;

                    bindingMap[(binding.layer, binding.slotIndex)] = binding;
                }
            }

            initialized = true;
            return true;
        }

        public bool TryPlaySegment(AnimationSegment segment, int slotIndex, float crossFadeDurationOverride, out string stateName)
        {
            stateName = null;
            if (!initialized || overrideController == null || animator == null || segment == null || segment.clip == null)
                return false;

            if (!TryResolvePlaybackSlot(segment.layer, slotIndex, out int playbackSlotIndex, out SlotRuntimeInfo slotRuntimeInfo))
            {
                WarnMissingSlot(segment.layer, slotIndex, "没有找到可用的 Animator 状态槽");
                return false;
            }

            stateName = slotRuntimeInfo.StateName;

            if (string.IsNullOrWhiteSpace(stateName))
            {
                WarnMissingSlot(segment.layer, playbackSlotIndex, "状态名为空");
                return false;
            }

            if (string.IsNullOrWhiteSpace(slotRuntimeInfo.PlaceholderName))
            {
                WarnMissingSlot(segment.layer, playbackSlotIndex, "占位动画名为空");
                return false;
            }

            if (!slotRuntimeInfo.IsUsable)
            {
                WarnMissingSlot(segment.layer, playbackSlotIndex,
                    $"未在 AnimatorOverrideController 中找到占位动画：{slotRuntimeInfo.PlaceholderName}");
                return false;
            }

            overrideController[slotRuntimeInfo.PlaceholderName] = segment.clip;
            activeStateHashByLayer[segment.layer] = slotRuntimeInfo.StateHash;
            float normalizedCrossFade = crossFadeDurationOverride >= 0f
                ? Mathf.Clamp01(crossFadeDurationOverride)
                : Mathf.Clamp01(segment.crossFadeDuration);
            if (normalizedCrossFade <= 0f)
                animator.Play(slotRuntimeInfo.StateHash, segment.layer, 0f);
            else
                animator.CrossFade(slotRuntimeInfo.StateHash, normalizedCrossFade, segment.layer, 0f);
            return true;
        }

        public void ResetAnimatorState()
        {
            if (!initialized || animator == null)
                return;

            activeStateHashByLayer.Clear();
            animator.Rebind();
            animator.Update(0f);
        }

        private bool TryResolvePlaybackSlot(int layer, int requestedSlotIndex, out int resolvedSlotIndex,
            out SlotRuntimeInfo slotRuntimeInfo)
        {
            resolvedSlotIndex = requestedSlotIndex;
            slotRuntimeInfo = ResolveSlotRuntimeInfo(layer, requestedSlotIndex);
            if (!slotRuntimeInfo.IsUsable)
                return TryResolveAlternateSlot(layer, requestedSlotIndex, out resolvedSlotIndex, out slotRuntimeInfo);

            if (activeStateHashByLayer.TryGetValue(layer, out int currentStateHash) &&
                currentStateHash == slotRuntimeInfo.StateHash &&
                TryResolveAlternateSlot(layer, requestedSlotIndex, out int alternateSlotIndex, out SlotRuntimeInfo alternateSlotRuntimeInfo))
            {
                resolvedSlotIndex = alternateSlotIndex;
                slotRuntimeInfo = alternateSlotRuntimeInfo;
            }

            return slotRuntimeInfo.IsUsable;
        }

        private bool TryResolveAlternateSlot(int layer, int requestedSlotIndex, out int resolvedSlotIndex,
            out SlotRuntimeInfo slotRuntimeInfo)
        {
            int searchSlotCount = ResolveSearchSlotCount(layer);
            if (searchSlotCount <= 1)
            {
                resolvedSlotIndex = requestedSlotIndex;
                slotRuntimeInfo = default;
                return false;
            }

            for (int offset = 1; offset < searchSlotCount; offset++)
            {
                int candidateSlotIndex = (requestedSlotIndex + offset) % searchSlotCount;
                slotRuntimeInfo = ResolveSlotRuntimeInfo(layer, candidateSlotIndex);
                if (!slotRuntimeInfo.IsUsable)
                    continue;

                resolvedSlotIndex = candidateSlotIndex;
                return true;
            }

            resolvedSlotIndex = requestedSlotIndex;
            slotRuntimeInfo = default;
            return false;
        }

        private int ResolveSearchSlotCount(int layer)
        {
            int maxSlotCount = BehaviorAnimatorControllerConvention.DefaultSlotsPerLayer;
            if (slotBindings == null)
                return maxSlotCount;

            for (int i = 0; i < slotBindings.Length; i++)
            {
                AnimatorSegmentSlotBinding binding = slotBindings[i];
                if (binding == null || binding.layer != layer)
                    continue;

                maxSlotCount = Mathf.Max(maxSlotCount, binding.slotIndex + 1);
            }

            return Mathf.Clamp(maxSlotCount, 1, 32);
        }

        private SlotRuntimeInfo ResolveSlotRuntimeInfo(int layer, int slotIndex)
        {
            if (slotRuntimeInfoCache.TryGetValue((layer, slotIndex), out SlotRuntimeInfo cachedInfo))
                return cachedInfo;

            ResolveSlot(layer, slotIndex, out string stateName, out string placeholderName);
            int stateHash = ResolveStateHash(stateName);
            bool isUsable =
                animator != null &&
                !string.IsNullOrWhiteSpace(stateName) &&
                !string.IsNullOrWhiteSpace(placeholderName) &&
                placeholderClipNames.Contains(placeholderName) &&
                animator.HasState(layer, stateHash);

            SlotRuntimeInfo runtimeInfo = new SlotRuntimeInfo(stateName, placeholderName, stateHash, isUsable);
            slotRuntimeInfoCache[(layer, slotIndex)] = runtimeInfo;
            return runtimeInfo;
        }

        private int ResolveStateHash(string stateName)
        {
            if (string.IsNullOrWhiteSpace(stateName))
                return 0;

            if (stateHashCache.TryGetValue(stateName, out int cachedStateHash))
                return cachedStateHash;

            int stateHash = Animator.StringToHash(stateName);
            stateHashCache[stateName] = stateHash;
            return stateHash;
        }

        private void ResolveSlot(int layer, int slotIndex, out string stateName, out string placeholderName)
        {
            if (bindingMap.TryGetValue((layer, slotIndex), out AnimatorSegmentSlotBinding binding))
            {
                stateName = binding.stateName;
                placeholderName = binding.placeholderClipName;
                return;
            }

            stateName = BehaviorAnimatorControllerConvention.GetStateName(layer, slotIndex);
            placeholderName = BehaviorAnimatorControllerConvention.GetPlaceholderClipName(layer, slotIndex);
        }

        private void WarnMissingSlot(int layer, int slotIndex, string reason)
        {
            string key = $"{layer}:{slotIndex}:{reason}";
            if (missingSlots.Add(key))
            {
                Debug.LogWarning(
                    $"AnimatorSegmentPlayer 槽位不可用。Layer={layer}, Slot={slotIndex}。原因：{reason}。" +
                    " 默认约定为状态名 L{layer}_Segment_{slot}，占位动画名 L{layer}_Placeholder_{slot}。",
                    animator != null ? animator.gameObject : gameObject);
            }
        }
    }
}
