using UnityEngine;

namespace Framework.Gameplay.Abilities.Input
{
    /// <summary>保存输入能力采集的连续状态和可消费边沿事件。</summary>
    public sealed class InputRuntimeData : IAbilityRuntimeData
    {
        // 当前帧平面移动输入。
        public Vector2 Move { get; private set; }
        // 当前处于按住状态的按钮位集合。
        private ulong _heldButtons;
        // 尚未被任何能力消费的按钮按下边沿位集合。
        private ulong _pressedButtons;
        // 尚未被任何能力消费的按钮松开边沿位集合。
        private ulong _releasedButtons;

        /// <summary>写入当前帧平面移动输入。</summary>
        /// <param name="move">平面输入，超出单位圆时归一化。</param>
        public void WriteMove(Vector2 move) { Move = move.normalized; }

        /// <summary>写入指定按钮的当前按住状态并记录状态变化边沿。</summary>
        /// <param name="button">需要写入的通用按钮标识；无效标识会被忽略。</param>
        /// <param name="isHeld">当前帧是否按住该按钮。</param>
        public void WriteButtonState(InputButton button, bool isHeld)
        {
            if (!TryGetButtonMask(button, out ulong mask)) return;

            // 比较上一帧持续状态，只在变化时保留可跨物理帧消费的边沿。
            bool wasHeld = (_heldButtons & mask) != 0ul;
            if (isHeld)
            {
                if (!wasHeld) _pressedButtons |= mask;
                _heldButtons |= mask;
                return;
            }

            if (wasHeld) _releasedButtons |= mask;
            _heldButtons &= ~mask;
        }

        /// <summary>判断指定按钮当前是否处于按住状态。</summary>
        /// <param name="button">需要读取的通用按钮标识。</param>
        /// <returns>按钮有效且当前按住时返回 true。</returns>
        public bool IsHeld(InputButton button)
        {
            return TryGetButtonMask(button, out ulong mask) && (_heldButtons & mask) != 0UL;
        }

        /// <summary>判断指定按钮是否存在尚未消费的按下边沿。</summary>
        /// <param name="button">需要读取的通用按钮标识。</param>
        /// <returns>按钮有效且存在未消费按下边沿时返回 true。</returns>
        public bool WasPressed(InputButton button)
        {
            return TryGetButtonMask(button, out ulong mask) && (_pressedButtons & mask) != 0UL;
        }

        /// <summary>判断指定按钮是否存在尚未消费的松开边沿。</summary>
        /// <param name="button">需要读取的通用按钮标识。</param>
        /// <returns>按钮有效且存在未消费松开边沿时返回 true。</returns>
        public bool WasReleased(InputButton button)
        {
            return TryGetButtonMask(button, out ulong mask) && (_releasedButtons & mask) != 0UL;
        }

        /// <summary>消费一次指定按钮尚未处理的按下边沿。</summary>
        /// <param name="button">需要消费的通用按钮标识。</param>
        /// <returns>成功消费未处理按下边沿时返回 true。</returns>
        public bool ConsumePressed(InputButton button)
        {
            if (!TryGetButtonMask(button, out ulong mask)) return false;
            if ((_pressedButtons & mask) == 0UL) return false;
            _pressedButtons &= ~mask;
            return true;
        }

        /// <summary>消费一次指定按钮尚未处理的松开边沿。</summary>
        /// <param name="button">需要消费的通用按钮标识。</param>
        /// <returns>成功消费未处理松开边沿时返回 true。</returns>
        public bool ConsumeReleased(InputButton button)
        {
            if (!TryGetButtonMask(button, out ulong mask)) return false;
            if ((_releasedButtons & mask) == 0UL) return false;
            _releasedButtons &= ~mask;
            return true;
        }

        /// <summary>清空当前帧输入和待消费事件。</summary>
        public void Reset()
        {
            Move = Vector2.zero;
            _heldButtons = 0UL;
            _pressedButtons = 0UL;
            _releasedButtons = 0UL;
        }

        /// <summary>将平面输入转换为指定参考系下的世界方向。</summary>
        /// <param name="reference">移动参考 Transform；为空时使用世界坐标。</param>
        /// <returns>归一化世界空间平面方向。</returns>
        public Vector3 GetWorldMoveDirection(Transform reference)
        {
            if (Move.sqrMagnitude <= 0.0001f) return Vector3.zero;
            if (reference == null) return new Vector3(Move.x, 0f, Move.y).normalized;

            Vector3 forward = Vector3.ProjectOnPlane(reference.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(reference.right, Vector3.up).normalized;
            return (forward * Move.y + right * Move.x).normalized;
        }

        /// <summary>将通用按钮标识转换为内部位掩码。</summary>
        /// <param name="button">需要转换的按钮标识。</param>
        /// <param name="mask">有效时返回唯一按钮位掩码。</param>
        /// <returns>按钮标识可由当前位集合表示时返回 true。</returns>
        internal static bool TryGetButtonMask(InputButton button, out ulong mask)
        {
            int bitIndex = (int)button;
            if (bitIndex <= (int)InputButton.None || bitIndex >= 64)
            {
                mask = 0ul;
                return false;
            }

            mask = 1ul << bitIndex;
            return true;
        }
    }
}
