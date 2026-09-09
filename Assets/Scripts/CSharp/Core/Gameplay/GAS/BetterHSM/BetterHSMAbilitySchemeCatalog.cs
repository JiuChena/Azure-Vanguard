using System;
using System.Collections.Generic;
using Core.Gear;
using Framework.Gameplay.Abilities.BetterHSM.Test;

namespace Framework.Gameplay.Abilities
{
    /// <summary>集中保存 BetterHSM 顶层实体分类与状态图创建方案的静态映射。</summary>
    public static class BetterHSMAbilitySchemeCatalog
    {
        // 顶层实体分类到二级状态图方案到工厂的固定映射，不提供运行时注册入口。
        private static readonly Dictionary<BetterHSMEntityCategory, Dictionary<BetterHSMRegistrationId, Func<AbilityOwnerContext, BetterHSMAbilityContext>>> Schemes =
            new Dictionary<BetterHSMEntityCategory, Dictionary<BetterHSMRegistrationId, Func<AbilityOwnerContext, BetterHSMAbilityContext>>>
            {
                // 具体方案确定后，在对应分类的内层字典中显式追加枚举和工厂方法。
                {
                    BetterHSMEntityCategory.Character,
                    new Dictionary<BetterHSMRegistrationId, Func<AbilityOwnerContext, BetterHSMAbilityContext>>()
                    {
                        //此处写每个角色的HSM状态注册方案
                        { BetterHSMRegistrationId.CH0221Test, CH0221TestBetterHSMRegistration.Create }
                    }
                },
                {
                    BetterHSMEntityCategory.Enemy,
                    new Dictionary<BetterHSMRegistrationId, Func<AbilityOwnerContext, BetterHSMAbilityContext>>()
                    {
                        //此处写每个敌人的HSM状态注册方案
                    }
                },
                {
                    BetterHSMEntityCategory.NPC,
                    new Dictionary<BetterHSMRegistrationId, Func<AbilityOwnerContext, BetterHSMAbilityContext>>()
                    {
                        //此处写每个NPC的HSM状态注册方案
                    }
                }
            };

        /// <summary>
        /// 按顶层实体分类创建当前单位独有的 BetterHSM 状态图。
        /// </summary>
        /// <param name="entityCategory">当前 BetterHSM 能力配置选择的顶层实体分类。</param>
        /// <param name="registrationId">当前顶层分类下选择的具体状态图注册方案。</param>
        /// <param name="ownerContext">当前单位的 GAS 拥有者上下文；不允许为 null。</param>
        /// <returns>包含当前单位独有状态上下文和已构建状态机的运行时包装。</returns>
        /// <exception cref="ArgumentNullException">拥有者上下文为空时抛出。</exception>
        /// <exception cref="InvalidOperationException">分类没有显式配置状态图工厂，或工厂返回空结果时抛出。</exception>
        public static BetterHSMAbilityContext Create(BetterHSMEntityCategory entityCategory, BetterHSMRegistrationId registrationId, AbilityOwnerContext ownerContext)
        {
            // 验证当前实体具备创建运行时状态图所需的 GAS 上下文。
            if (ownerContext == null) throw new ArgumentNullException(nameof(ownerContext));
            if (ownerContext.Owner == null) throw new InvalidOperationException("BetterHSM 状态图工厂需要有效的 GAS Owner。");

            // 查找代码中明确声明的分类和方案工厂，不进行动态注册或反射扫描。
            if (!Schemes.TryGetValue(entityCategory, out Dictionary<BetterHSMRegistrationId, Func<AbilityOwnerContext, BetterHSMAbilityContext>> categorySchemes) ||
                !categorySchemes.TryGetValue(registrationId, out Func<AbilityOwnerContext, BetterHSMAbilityContext> factory))
            {
                throw new InvalidOperationException($"BetterHSM 实体分类 {entityCategory} 的注册方案 {registrationId} 尚未配置状态图工厂。请在 {nameof(BetterHSMAbilitySchemeCatalog)} 中显式添加映射。");
            }

            // 立即执行工厂，并阻止不完整的状态图结果进入能力运行时。
            BetterHSMAbilityContext abilityContext = factory(ownerContext);
            if (abilityContext == null) throw new InvalidOperationException($"BetterHSM 实体分类 {entityCategory} 的注册方案 {registrationId} 返回了空上下文。");

            return abilityContext;
        }
    }
}
