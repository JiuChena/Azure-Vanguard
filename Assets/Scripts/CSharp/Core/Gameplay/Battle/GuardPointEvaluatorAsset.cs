using UnityEngine;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 胜负策略资产壳（继承式可重写判定的关卡引用载体，2026-10-14 拍板）：
    /// ③模拟核只消费 BattleOutcomeEvaluator 实例（纯 C#），本 SO 只负责关卡资产引用与实例化。
    /// 资产放 Assets/Configurations/Battle/，入组 Configuration-Battle（批 4）。
    /// </summary>
    [CreateAssetMenu(menuName = "AzureVanguard/Battle/GuardPointEvaluator", fileName = "GuardPointEvaluator_New")]
    public sealed class GuardPointEvaluatorAsset : ScriptableObject
    {
        // v1 无参数（守点默认判定）；挑战关变体后续在此扩配置字段
        public BattleOutcomeEvaluator CreateEvaluator() => new GuardPointEvaluator();
    }
}
