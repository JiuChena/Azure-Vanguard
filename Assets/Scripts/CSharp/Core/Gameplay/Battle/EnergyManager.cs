using UnityEngine;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 能量管理器（③ 模拟层，静态入口）：回能事件统一入账（命中/击杀/技能施放，受 EnergyRecharge 乘区，
    /// clamp 到单位各自上限——上限装配自角色表 maxEnergy 列）。
    /// 回能值**表驱动**（2026-10-28 用户拍板）：CharacterSkillTable.energyGain（普攻行=命中回能 / E·EX 行=施放回能）；
    /// 充能效率 = 面板基础 + TalentAura 光环实时加成（2026-11-04 天赋系统）
    /// + EnemyTable.energyOnKill（每敌人击杀回能，boss 可配多）；配 0 不回能，旧硬编码常量已移除。
    /// 能量不随时间走（事件驱动累加），故无节拍推进——入账即结算；感知层读能量判定 Q 技能意图。
    /// 入账源：命中经子弹回调（CombatBullet 读普攻行）、技能施放经 BHSM 施放结算（读技能行）、
    /// 击杀经 BattleController.Step 死亡扫描（读敌人行）。本类无状态，静态方法直取单位上限/乘区。
    /// </summary>
    public static class EnergyManager
    {
        /// <summary>能量入账：受 EnergyRecharge 乘区（RoundToInt），clamp 到单位上限；非活体/无上限/值非正忽略。</summary>
        public static void Add(BattleUnitState unit, int amount)
        {
            if (unit == null || !unit.IsAlive || unit.MaxEnergy <= 0 || amount <= 0) return;
            // 充能效率 = 面板基础 + 天赋/光环实时加成（TalentAura）
            float energyRecharge = unit.Stats.EnergyRecharge + TalentAura.GetEnergyRecharge(unit.UnitId);
            int gained = Mathf.RoundToInt(amount * energyRecharge);
            if (gained <= 0) return;
            unit.Energy = Mathf.Min(unit.MaxEnergy, unit.Energy + gained);
        }

        /// <summary>消耗能量（Q 技能施放扣，clamp 0）；返回实际扣除量。</summary>
        public static int Spend(BattleUnitState unit, int amount)
        {
            if (unit == null || unit.MaxEnergy <= 0 || amount <= 0) return 0;
            int actual = Mathf.Min(unit.Energy, amount);
            unit.Energy -= actual;
            return actual;
        }
    }
}
