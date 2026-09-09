using Framework.Gameplay.Abilities;
using UnityEngine;

namespace Framework.Gameplay.Abilities.Configuration
{
    /// <summary>保存 BetterHSM 能力的静态配置。</summary>
    [CreateAssetMenu(fileName = "BetterHSMAbility", menuName = "Framework/Gameplay/Abilities/BetterHSM")]
    public sealed class BetterHSMAbilitySO : AbilityDefinitionSO
    {
        // 当前能力选择的顶层实体状态图分类。
        [Tooltip("当前单位使用的 BetterHSM 顶层状态图分类；具体角色、敌人和 NPC 方案后续在分类下继续细分")]
        [SerializeField] private BetterHSMEntityCategory _entityCategory;
        // 当前顶层分类下选择的具体状态图注册方案。
        [Tooltip("当前分类使用的 BetterHSM 状态图注册方案；具体方案确定后会在此枚举中继续增加")]
        [SerializeField] private BetterHSMRegistrationId _registrationId;

        /// <summary>获取当前配置选择的 BetterHSM 顶层实体分类。</summary>
        public BetterHSMEntityCategory EntityCategory => _entityCategory;

        /// <summary>获取当前配置选择的 BetterHSM 状态图注册方案。</summary>
        public BetterHSMRegistrationId RegistrationId => _registrationId;

        /// <summary>创建 BetterHSM 能力运行时。</summary>
        /// <returns>使用当前配置的 BetterHSM 能力运行时实例。</returns>
        public override AbilityRuntime CreateRuntime()
        {
            return new BetterHSMAbilityRuntime(this);
        }
    }
}
