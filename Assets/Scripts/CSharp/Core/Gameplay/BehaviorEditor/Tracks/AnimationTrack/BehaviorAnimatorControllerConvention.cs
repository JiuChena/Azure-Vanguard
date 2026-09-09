using System;
using System.Collections.Generic;
using UnityEngine;

namespace BehaviorEditor
{
    /// <summary>
    /// AnimationTrack 使用的 AnimatorController 命名约定。
    /// </summary>
    public static class BehaviorAnimatorControllerConvention
    {
        public const string DefaultSharedControllerFolder = "Assets/BehaviorEditor/Animator";
        public const string DefaultSharedControllerName = "BehaviorBaseController";
        public const int DefaultLayerCount = 2;
        public const int DefaultSlotsPerLayer = 8;

        public static string GetStateName(int layer, int slotIndex)
        {
            return $"L{layer}_Segment_{slotIndex}";
        }

        public static string GetPlaceholderClipName(int layer, int slotIndex)
        {
            return $"L{layer}_Placeholder_{slotIndex}";
        }
    }
}
