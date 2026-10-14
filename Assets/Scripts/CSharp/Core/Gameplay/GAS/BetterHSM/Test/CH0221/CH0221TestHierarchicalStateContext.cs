using System;
using Framework.Gameplay.Abilities.Input;

namespace Framework.Gameplay.Abilities.BetterHSM.Test
{
    /// <summary>保存 CH0221 分层 BetterHSM 演示状态共同读取的数据；包装基础测试上下文并追加战斗请求数据。</summary>
    public sealed class CH0221TestHierarchicalStateContext
    {
        // 被包装的基础测试上下文，提供输入读取与 BE 行为播放能力。
        private readonly CH0221TestStateContext _baseContext;
        // 战斗请求标志：业务数据变更处置位，locomotion 层攻击边的中断条件读取。
        private bool _hasCombatRequest;

        /// <summary>
        /// 创建分层演示状态上下文。
        /// </summary>
        /// <param name="baseContext">被包装的基础测试上下文；不允许为 null。</param>
        /// <exception cref="ArgumentNullException">基础上下文为空时抛出。</exception>
        public CH0221TestHierarchicalStateContext(CH0221TestStateContext baseContext)
        {
            // 保存由注册方案传入的基础上下文。
            if (baseContext == null) throw new ArgumentNullException(nameof(baseContext));

            _baseContext = baseContext;
        }

        /// <summary>
        /// 获取当前 CH0221 实体所属的 GAS 上下文。
        /// </summary>
        public AbilityOwnerContext OwnerContext => _baseContext.OwnerContext;

        /// <summary>
        /// 判断当前帧是否存在有效的平面移动输入。
        /// </summary>
        /// <returns>输入能力已提供非零移动输入时返回 true。</returns>
        public bool HasMoveInput()
        {
            return _baseContext.HasMoveInput();
        }

        /// <summary>
        /// 按叶子状态键播放当前实体的 BE 行为；BE 行为能力未挂载时忽略本次请求。
        /// </summary>
        /// <param name="behaviorKey">叶子状态类中声明的 BehaviorKey 常量。</param>
        public void PlayBehavior(string behaviorKey)
        {
            _baseContext.PlayBehavior(behaviorKey);
        }

        /// <summary>
        /// 停止当前实体的 BE 行为播放；BE 行为能力未挂载时忽略。
        /// </summary>
        public void StopBehavior()
        {
            _baseContext.StopBehavior();
        }

        /// <summary>
        /// 判断当前是否存在战斗请求（locomotion 层攻击边的中断条件）。
        /// </summary>
        /// <returns>业务已写入战斗请求时返回 true。</returns>
        public bool HasCombatRequest()
        {
            return _hasCombatRequest;
        }

        /// <summary>
        /// 业务侧写入战斗请求；按事件式门控约定，数据变更处应同时对本层状态 MarkDirty。
        /// </summary>
        public void RequestCombat()
        {
            _hasCombatRequest = true;
        }

        /// <summary>
        /// 清除战斗请求（攻击状态进入时消费）。
        /// </summary>
        public void ClearCombatRequest()
        {
            _hasCombatRequest = false;
        }
    }
}
