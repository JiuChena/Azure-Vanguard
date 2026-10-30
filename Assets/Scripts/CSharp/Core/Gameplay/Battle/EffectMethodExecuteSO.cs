using BehaviorEditor;
using Framework.Gameplay.Abilities;
using UnityEngine;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 极简效果执行 SO（2026-10-29 用户拍板：效果逻辑 = 每角色静态类函数，SO 只存 effectKey 查三层字典调函数）：
    /// skill clip 事件挂本 SO，只配 effectKey（string）——Execute 时经施放者单位（种类 + 单位 id）三层查表
    /// EffectMethodRegistry 调对应静态方法（函数内给目标 GAS 注册 buff，GAS Effect 能力管生命周期）。
    /// </summary>
    [CreateAssetMenu(menuName = "AzureVanguard/Battle/Effect/Method", fileName = "EffectMethod_New")]
    public class EffectMethodExecuteSO : BehaviorEventExecuteSO
    {
        [Header("效果函数（三层字典 effectKey：单位种类→单位ID→函数名）")]
        [Tooltip("效果函数注册名（Effects_CH0221 等方法，如 NormalAttack / HealingChord）")]
        [SerializeField] private string effectKey;

        /// <inheritdoc/>
        public override void Execute(BehaviorEventContext context)
        {
            if (context.OwnerContext == null || string.IsNullOrEmpty(effectKey)) return;
            if (!context.OwnerContext.TryGet(AbilityRuntimeDataType.BattleUnit, out BattleUnitRuntimeData casterData)) return;

            BattleUnitState caster = casterData.Unit;
            if (caster == null) return;

            BetterHSMEntityCategory category = caster.Camp == BattleCamp.Enemy
                ? BetterHSMEntityCategory.Enemy
                : BetterHSMEntityCategory.Character;

            BattleController controller = ResolveController(context);
            var ctx = new SkillEffectContext
            {
                Caster = caster,
                Controller = controller,
                OwnerContext = context.OwnerContext,
            };

            if (!EffectMethodRegistry.TryGet(category, caster.UnitId, effectKey, out EffectMethodRegistry.EffectMethod method))
            {
                Debug.LogWarning($"[EffectMethod] 未注册效果函数 {category}/{caster.UnitId}/{effectKey}，跳过。");
                return;
            }
            method(ctx);
        }

        private BattleController ResolveController(BehaviorEventContext context)
        {
            if (context.OwnerContext != null
                && context.OwnerContext.TryGet(AbilityRuntimeDataType.BattleWorld, out BattleWorldRuntimeData worldData))
            {
                return worldData.Controller;
            }
            return null;
        }
    }
}
