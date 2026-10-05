using System;
using BehaviorEditor;
using Framework.Gameplay.Abilities.Configuration;

namespace Framework.Gameplay.Abilities
{
    /// <summary>执行 BE 行为能力的单位独占运行时：持有 BehaviorExecutor 并每帧推进播放头。</summary>
    public sealed class BehaviorAbilityRuntime : AbilityRuntime
    {
        // 当前能力的静态配置。
        private readonly BehaviorAbilitySO _configuration;
        // 当前能力创建并持有的纯 C# 行为播放执行器。
        private BehaviorExecutor _executor;
        // 当前能力向其他能力公开的运行时数据。
        private BehaviorAbilityRuntimeData _runtimeData;
        // 当前能力运行时数据在拥有者上下文中的注册键。
        private const AbilityRuntimeDataType RuntimeDataType = AbilityRuntimeDataType.Behavior;

        /// <summary>创建 BE 行为能力运行时并保存配置引用。</summary>
        /// <param name="configuration">BE 行为能力配置资产。</param>
        /// <exception cref="ArgumentNullException">配置资产为空时抛出。</exception>
        public BehaviorAbilityRuntime(BehaviorAbilitySO configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>绑定能力拥有者上下文并创建播放执行器与公开运行时数据。</summary>
        /// <param name="ownerContext">当前单位的能力拥有者上下文。</param>
        public override void AbilityInit(AbilityOwnerContext ownerContext)
        {
            // 先绑定当前实体的 GAS 上下文。
            base.AbilityInit(ownerContext);
            if (ownerContext == null) throw new ArgumentNullException(nameof(ownerContext));

            // 创建绑定当前单位的播放执行器；UnitConfig 未配置时按缺失处理，播放请求报错跳过。
            _executor = new BehaviorExecutor(OwnerContext.Owner, _configuration.LogBehaviorFlow);

            // 将按键播放与停止入口注册给状态图等其他能力使用。
            _runtimeData = new BehaviorAbilityRuntimeData(_executor, OwnerContext.UnitConfig, OwnerContext.Owner);
            OwnerContext.Register(RuntimeDataType, _runtimeData);
        }

        /// <summary>执行能力普通帧逻辑：常开推进播放头，空闲时执行器内部直接返回。</summary>
        /// <param name="deltaTime">当前帧时长，单位：秒。</param>
        public override void AbilityUpdate(float deltaTime)
        {
            _executor?.Tick(deltaTime);
        }

        /// <summary>释放能力持有的运行时依赖。</summary>
        public override void AbilityDispose()
        {
            // 先撤销对外查询入口，再停止播放并丢弃执行器。
            OwnerContext?.Unregister(RuntimeDataType, _runtimeData);
            _executor?.Stop();
            _executor = null;
            _runtimeData = null;
            base.AbilityDispose();
        }
    }
}
