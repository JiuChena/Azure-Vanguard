using Core.Gear;

namespace Framework.Gameplay.Abilities.BetterHSM.Combat
{
    /// <summary>攻击循环状态：一轮攻击动画播完后仍想打就续播留守（wrap 模式无关——Once 重播 / Loop 幂等），
    /// 欲望消失才移交收招出边；Downed 立即倒地豁免由图边表达。
    /// 转出放行按 80% 口径（2026-10-20 拍板）：圈内进度过八成即计一轮完成，收招不再死等整圈
    /// （音频在片段开头触发，等待整圈会拖节奏）；留守续播节拍仍按整圈回绕。</summary>
    /// <typeparam name="TContext">所属单位的战斗状态上下文类型。</typeparam>
    public class CombatAttackingState<TContext> : CombatSegmentState<TContext> where TContext : CombatStateContext
    {
        /// <summary>本状态对应的 BE 行为键；与 UnitConfig 行为列表中的 clip 资产名对齐（Normal 前缀=站姿）。</summary>
        public const string BehaviorKey = "NormalAttacking";

        /// <inheritdoc/>
        protected override string Key => BehaviorKey;

        /// <summary>循环攻击段一轮完成阈值：进度过八成即放行转出（音频每圈开头触发，收招等待期缩短）。</summary>
        protected override float LoopFinishProgress => 0.8f;

        /// <summary>创建循环段（按轮完成检测，兼容 Once/Loop 两种 wrap 配置）。</summary>
        public CombatAttackingState() : base(true) { }

        /// <summary>一轮播完仍想打就续播留守；无欲望交给出边收招。</summary>
        protected override bool ContinueOnRoundComplete() => Context.WantAttack;
    }
}
