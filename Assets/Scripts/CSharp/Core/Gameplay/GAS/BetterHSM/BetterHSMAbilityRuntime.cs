using Framework.Gameplay.Abilities.Configuration;

namespace Framework.Gameplay.Abilities
{
    /// <summary>执行 BetterHSM 能力的单位独占运行时逻辑。</summary>
    public sealed class BetterHSMAbilityRuntime : AbilityRuntime
    {
        // 当前能力的静态配置。
        private readonly BetterHSMAbilitySO _configuration;
        // 当前实体创建并由本能力唯一驱动的状态机运行时包装。
        private BetterHSMAbilityContext _abilityContext;
        // 当前能力向其他能力公开的运行时数据。
        private BetterHSMAbilityRuntimeData _runtimeData;
        // 当前能力运行时数据在拥有者上下文中的注册键。
        private const AbilityRuntimeDataType RuntimeDataType = AbilityRuntimeDataType.BetterHSM;

        /// <summary>创建 BetterHSM 能力运行时并保存配置引用。</summary>
        /// <param name="configuration">BetterHSM 能力配置资产。</param>
        public BetterHSMAbilityRuntime(BetterHSMAbilitySO configuration)
        {
            _configuration = configuration;
        }

        /// <summary>绑定能力拥有者上下文并初始化运行时依赖。</summary>
        /// <param name="ownerContext">当前单位的能力拥有者上下文。</param>
        public override void AbilityInit(AbilityOwnerContext ownerContext)
        {
            // 先绑定当前实体的 GAS 上下文。
            base.AbilityInit(ownerContext);
            if (ownerContext == null) throw new System.ArgumentNullException(nameof(ownerContext));
            if (_configuration == null) throw new System.InvalidOperationException("BetterHSM 能力配置不能为空。");

            // 按 SO 选择的顶层分类创建当前实体独有的状态图。
            _abilityContext = BetterHSMAbilitySchemeCatalog.Create(_configuration.EntityCategory, _configuration.RegistrationId, ownerContext);

            // 将只读查询入口注册给其他能力或其他实体使用。
            _runtimeData = new BetterHSMAbilityRuntimeData(_abilityContext);
            OwnerContext.Register(RuntimeDataType, _runtimeData);
        }

        /// <summary>执行能力普通帧逻辑。</summary>
        /// <param name="deltaTime">当前帧时长，单位：秒。</param>
        public override void AbilityUpdate(float deltaTime)
        {
            // HSM 自行从状态上下文读取所需时间和数据；不接收 GAS 的 deltaTime 参数。
            _abilityContext?.Update();
        }

        /// <summary>释放能力持有的运行时依赖。</summary>
        public override void AbilityDispose()
        {
            // 先撤销对外查询入口，再释放状态机及其所有状态实例。
            OwnerContext?.Unregister(RuntimeDataType, _runtimeData);
            _abilityContext?.Dispose();
            _abilityContext = null;
            _runtimeData = null;
            base.AbilityDispose();
        }
    }
}
