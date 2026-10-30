using UnityEngine;

namespace Framework.Gameplay.Abilities
{
    /// <summary>
    /// 敌人单位配置：UnitConfig 基类 + 敌人行为资产（敌人无养成/突破，不引用数据表）。
    /// behaviorClips 与角色同契约：clip 资产名即行为键（战斗状态按 Idle/Move/Attack/Death 查询，
    /// 敌人图只用 Move/Attack）。数值走 EnemyTable（③ 直读），SO 只留资产引用。
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyUnitConfig", menuName = "Framework/Gameplay/Enemy Unit Config")]
    public sealed class EnemyUnitConfig : UnitConfig
    {
    }
}
