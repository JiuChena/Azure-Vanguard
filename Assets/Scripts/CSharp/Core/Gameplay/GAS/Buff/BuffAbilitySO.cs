using UnityEngine;

namespace Framework.Gameplay.Abilities.Buff
{
    /// <summary>
    /// Buff 能力配置：零可配置参数（对齐 StatAbilitySO 首切片口径）——Buff 逻辑由各 Buff 类
    /// 自洽实现（生效/失效/逐帧/到期判定都在实例内），SO 只是能力装配的占位。
    /// Buff 实例由应用方 new 并经 BuffRuntimeData.ApplyBuff 注入，不入资产。
    /// </summary>
    [CreateAssetMenu(fileName = "BuffAbility", menuName = "Framework/Gameplay/Abilities/Buff")]
    public sealed class BuffAbilitySO : AbilityDefinitionSO
    {
        /// <summary>创建 buff 能力运行时。</summary>
        /// <returns>使用当前配置的 buff 能力运行时实例。</returns>
        public override AbilityRuntime CreateRuntime()
        {
            return new BuffAbilityRuntime(this);
        }
    }
}
