using Core.Gear;

namespace Framework.Gameplay.Abilities.BetterHSM.Combat
{
    /// <summary>
    /// 分段战斗状态基类：进入播同名 BE 行为，本轮未计完前转移边不放行（Downed 立即倒地的豁免由图边直接读
    /// Downed 表达，不经本基类）。一轮完成判定与 wrap 模式无关：Once 段=停播（IsPlaying=false），
    /// Loop 段=归一化时间回绕或越过 <see cref="LoopFinishProgress"/> 阈值（默认 1=整圈判轮）；
    /// 完成即 MarkDirty 唤醒门控层下帧评估出边。
    /// 表现缺失（BE 未挂载/clip 未配置）时立即放行——逻辑不因表现卡死。
    /// </summary>
    /// <typeparam name="TContext">所属单位的战斗状态上下文类型。</typeparam>
    public abstract class CombatSegmentState<TContext> : StateBase<TContext> where TContext : CombatStateContext
    {
        // 状态对应的 BE 行为键（子类 const 供调试面板反射，经 Key 虚属性进入基类）。
        protected abstract string Key { get; }

        // 是否循环段：Loop 段按回绕或阈值判轮，Once 段按停播判轮。
        private readonly bool _isLoopSegment;
        // 上一帧归一化时间（Loop 段回绕检测）。
        private float _lastNormalized;
        // 本圈是否已过提前放行点（Loop 段阈值 <1 时防同圈重复计轮）。
        private bool _passedFinish;

        /// <summary>本轮是否已播完（转移闭包的完成度 gate；留守续播期由 OnUpdate 复位）。</summary>
        public bool SegmentFinished { get; private set; }

        /// <summary>Loop 段一轮完成的进度阈值：1=整圈回绕判轮（默认）；子类覆写小于 1 提前放行转出，不影响续播节拍。</summary>
        protected virtual float LoopFinishProgress => 1f;

        /// <summary>创建分段状态。</summary>
        /// <param name="isLoopSegment">是否循环段（Loop=true / Once=false）。</param>
        protected CombatSegmentState(bool isLoopSegment)
        {
            _isLoopSegment = isLoopSegment;
        }

        /// <summary>进入分段：复位完成标记与回绕检测，播放同名 BE 行为（空 key = 未接入 clip，跳过播放）。</summary>
        public override void OnEnter()
        {
            SegmentFinished = false;
            _lastNormalized = 0f;
            _passedFinish = false;
            if (!string.IsNullOrEmpty(Key)) Context.PlayBehavior(Key);
        }

        /// <summary>离开分段：停止 BE 行为播放。</summary>
        public override void OnExit() => Context.StopBehavior();

        /// <summary>逐帧推进：检测一轮完成 → 按子类续播意图续播或标记完成。</summary>
        public override void OnUpdate()
        {
            bool roundCompleted;
            bool wrapped = false;
            if (_isLoopSegment)
            {
                // 表现缺失兜底：无时长（Play 失败）立即放行，防 Loop 段永不回绕卡死。
                if (Context.BehaviorDuration <= 0f)
                {
                    Finish();
                    return;
                }
                float current = Context.BehaviorNormalizedTime;
                wrapped = current < _lastNormalized;
                // 提前放行点（子类覆写阈值 <1）：本圈进度过阈值且未放过即计一轮完成——转出不再死等整圈。
                bool crossed = !wrapped && !_passedFinish && current >= LoopFinishProgress;
                if (wrapped) _passedFinish = false;
                if (crossed) _passedFinish = true;
                _lastNormalized = current;
                roundCompleted = wrapped || crossed;
            }
            else
            {
                // Once 段：停播即一轮完成（含 Play 失败/表现缺失）。
                roundCompleted = !Context.IsBehaviorPlaying;
            }

            if (!roundCompleted) return;

            // 一轮计完：留守续播（循环攻击段按欲望）或移交出边。
            if (ContinueOnRoundComplete())
            {
                if (!_isLoopSegment || wrapped)
                {
                    // Once 段每轮重播 / Loop 段整圈回绕重播（幂等）：节拍与音频按完整一轮。
                    SegmentFinished = false;
                    if (!string.IsNullOrEmpty(Key)) Context.PlayBehavior(Key);
                }
                else
                {
                    // 提前放行点留守：完成标记保持置位，欲望消失当帧出边即放行。
                    SegmentFinished = true;
                    MarkDirty();
                }
                return;
            }
            Finish();
        }

        /// <summary>一轮播完后是否续播本轮；默认否（完成即移交出边），循环攻击段覆写为按欲望留守。</summary>
        protected virtual bool ContinueOnRoundComplete() => false;

        // 标记本轮完成并标脏（门控层下帧评估出边；幂等）。
        protected void Finish()
        {
            if (SegmentFinished) return;
            SegmentFinished = true;
            MarkDirty();
        }
    }
}
