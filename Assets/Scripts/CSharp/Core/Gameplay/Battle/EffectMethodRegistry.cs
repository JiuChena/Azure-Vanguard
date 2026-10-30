using System.Collections.Generic;
using Framework.Gameplay.Abilities;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 效果函数注册表（2026-10-29 用户拍板：三层嵌套字典，函数配在角色静态类）：
    /// 第一层单位种类（BetterHSMEntityCategory 现成枚举）→ 第二层单位 ID（CH0221）→ 第三层效果函数名（string）→ 委托。
    /// 效果函数 = 每角色静态类的静态方法（如 Effects_CH0221），函数内注册 buff 到目标 GAS（Effect 能力管生命周期）。
    /// 查询 O(1)，比反射快且类型安全；新增效果 = 静态类加方法 + Register 一行。
    /// </summary>
    public static class EffectMethodRegistry
    {
        /// <summary>效果函数签名（模板固定：必须传 SkillEffectContext——施放者 GAS/战场/施放者；其余管理器单例函数内自取）。</summary>
        public delegate void EffectMethod(SkillEffectContext ctx);

        // 三层：单位种类 → 单位 ID → 效果函数名 → 委托
        private static readonly Dictionary<BetterHSMEntityCategory,
            Dictionary<string, Dictionary<string, EffectMethod>>> Catalog =
                new Dictionary<BetterHSMEntityCategory, Dictionary<string, Dictionary<string, EffectMethod>>>();

        static EffectMethodRegistry()
        {
            Effects_CH0221.Register();
        }

        /// <summary>注册效果函数（按单位种类 + 单位 id + 函数名）。</summary>
        public static void Register(BetterHSMEntityCategory category, string unitId, string effectKey,
            EffectMethod method)
        {
            if (string.IsNullOrEmpty(effectKey) || method == null) return;
            if (!Catalog.TryGetValue(category, out Dictionary<string, Dictionary<string, EffectMethod>> byUnit))
            {
                byUnit = new Dictionary<string, Dictionary<string, EffectMethod>>();
                Catalog[category] = byUnit;
            }
            if (!byUnit.TryGetValue(unitId, out Dictionary<string, EffectMethod> byKey))
            {
                byKey = new Dictionary<string, EffectMethod>();
                byUnit[unitId] = byKey;
            }
            byKey[effectKey] = method;
        }

        /// <summary>按单位种类 + 单位 id + 函数名取效果函数（三层查表 O(1)）。</summary>
        public static bool TryGet(BetterHSMEntityCategory category, string unitId, string effectKey,
            out EffectMethod method)
        {
            method = null;
            return Catalog.TryGetValue(category, out Dictionary<string, Dictionary<string, EffectMethod>> byUnit)
                && byUnit.TryGetValue(unitId, out Dictionary<string, EffectMethod> byKey)
                && byKey.TryGetValue(effectKey, out method);
        }
    }
}
