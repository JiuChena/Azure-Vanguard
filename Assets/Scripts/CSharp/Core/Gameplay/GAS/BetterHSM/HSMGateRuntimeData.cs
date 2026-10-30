using System;

namespace Framework.Gameplay.Abilities
{
    /// <summary>BetterHSM 门控标脏入口：注册工厂建图后把各门控状态的 MarkDirty 收成闭包注册，
    /// 感知能力写完决策标志后经它触发门控（闭包绕开 StateBase&lt;TContext&gt; 的泛型擦除，零框架改动）。</summary>
    public sealed class HSMGateRuntimeData : IAbilityRuntimeData
    {
        // 各门控状态的标脏闭包（注册工厂在建图期收集，生命周期随 HSM 能力）。
        private readonly Action[] _markGatedDirty;

        /// <summary>
        /// 创建 HSM 门控标脏入口。
        /// </summary>
        /// <param name="markGatedDirty">各门控状态的标脏闭包；不允许为 null。</param>
        /// <exception cref="ArgumentNullException">闭包数组为空时抛出。</exception>
        public HSMGateRuntimeData(Action[] markGatedDirty)
        {
            _markGatedDirty = markGatedDirty ?? throw new ArgumentNullException(nameof(markGatedDirty));
        }

        /// <summary>标记全部门控状态脏：感知标志变化帧调用一次，HSM 下帧评估本层中断；同帧多次调用天然合并。</summary>
        public void MarkGatedDirty()
        {
            for (int index = 0; index < _markGatedDirty.Length; index++) _markGatedDirty[index]?.Invoke();
        }

        /// <summary>门控入口无可重置状态。</summary>
        public void Reset()
        {
        }
    }
}
