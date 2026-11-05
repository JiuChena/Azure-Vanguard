using System.Collections.Generic;
using Framework.Gameplay.Abilities.Stat;
using Framework.Gameplay.Player;
using Framework.Gameplay.Unit;
using UnityEngine;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// CH0221 天赋效果（2026-11-04 用户拍板）：
    /// 22104 Harmony = 全场光环（全队含自己 +充能效率 / +伤害加成）；
    /// 22111 追奏 = 全队获得隐形 Effect，持有者对敌人造成伤害时概率追加一次伤害。
    /// 表只提供数值参数，行为在这里实现。
    /// </summary>
    public static class Talents_CH0221
    {
        public const string UnitId = "CH0221";

        private const int EncoreNodeId = 22111;
        private const int HarmonyDisplayBuffId = 2210401;
        private const int EncoreDisplayBuffId = 2211101;
        private const string HarmonyEffectId = "talent_CH0221_22104";
        private const string EncoreEffectId = "talent_CH0221_22111";

        /// <summary>角色登场时注册自己的全场效果（容器按 unitId 调用）。</summary>
        public static void Register(TalentEffectContainer container, BattleUnitState unit)
        {
            if (container == null || unit == null || unit.Camp != BattleCamp.Character) return;
            if (!CharacterWarehouseManager.Instance.TryGetEntry(UnitId, out CharacterEntry entry))
            {
                Debug.LogWarning($"[Talent] {UnitId} 未找到养成条目，天赋不注册。");
                return;
            }

            // Harmony：天赋技能等级 → params（energy 固定、damage 随等级成长）
            int level = Mathf.Max(1, entry.skillLevelTalent);
            float energy = 0.05f;
            float damage = 0.10f + 0.01f * (level - 1);
            if (SkillDataLibrary.TryGetSkillRow(UnitId, CharacterSkillRow.SkillTypes.Talent, out CharacterSkillRow row))
            {
                level = Mathf.Clamp(level, 1, Mathf.Max(1, row.MaxLevel));
                if (SkillDataLibrary.TryResolveValues(row, level, out Dictionary<string, float> values))
                {
                    if (values.TryGetValue("energy", out float e)) energy = e;
                    if (values.TryGetValue("damage", out float d)) damage = d;
                }
            }
            container.Register(new HarmonyTalentEffect(unit, energy, damage));

            // 追奏：22111 已解锁才注册
            if (!entry.unlockedNodes.Contains(EncoreNodeId)) return;
            if (!BattleDataModule.TryGetUnlockNode(EncoreNodeId, out UnlockNodeRow node))
            {
                Debug.LogWarning($"[Talent] {UnitId} 追奏节点 {EncoreNodeId} 查表失败，追奏不启用。");
                return;
            }
            if (node.Type != UnlockNodeRow.NodeTypes.Talent) return;

            float chance = node.GetParamValue("chance", 1);
            float ratio = node.GetParamValue("ratio", 1);
            if (chance <= 0f) chance = node.EffectValue; // 旧数据兼容
            if (ratio <= 0f) ratio = 0.05f;
            container.Register(new EncoreTalentEffect(container.Controller, unit, chance, ratio));
        }

        /// <summary>Harmony 光环：全队成员获取后登记充能效率/伤害加成。</summary>
        private sealed class HarmonyTalentEffect : IBattleGlobalEffect, ITalentDisplayStats
        {
            private readonly BattleUnitState source;
            private readonly float energyRecharge;
            private readonly float damageBonus;

            public HarmonyTalentEffect(BattleUnitState source, float energyRecharge, float damageBonus)
            {
                this.source = source;
                this.energyRecharge = energyRecharge;
                this.damageBonus = damageBonus;
            }

            public string Id => HarmonyEffectId;
            public BattleUnitState Source => source;
            public int DisplayBuffId => HarmonyDisplayBuffId;

            public bool CanAcquire(in TalentAcquireContext context)
            {
                return context.Camp == BattleCamp.Character && context.SourceAlive;
            }

            public void OnAcquire(BattleUnitState unit)
            {
                TalentAura.Add(unit.UnitId, Id, energyRecharge, damageBonus);
            }

            public void OnRelease(BattleUnitState unit)
            {
                TalentAura.Remove(unit.UnitId, Id);
            }

            public void OnDamageDealt(in BattleDamageEvent e)
            {
            }

            // 面板展示：把光环加成挂到单位 StatRuntimeData（同来源先撤再加，幂等）
            public void ApplyDisplayStats(StatRuntimeData stats)
            {
                if (stats == null) return;
                stats.RemoveBySource(Id);
                stats.AddModifier(new StatModifier(Id, StatType.EnergyRecharge,
                    StatModifier.ModifierType.Flat, energyRecharge));
                stats.AddModifier(new StatModifier(Id, StatType.DamageBonus,
                    StatModifier.ModifierType.Flat, damageBonus));
            }

            public void RemoveDisplayStats(StatRuntimeData stats)
            {
                stats?.RemoveBySource(Id);
            }
        }

        /// <summary>追奏：持有者对敌人造成伤害时概率追加一次伤害（基础值取 CH0221 面板）。</summary>
        private sealed class EncoreTalentEffect : IBattleGlobalEffect
        {
            private readonly BattleController controller;
            private readonly BattleUnitState source;
            private readonly float chance;
            private readonly float ratio;
            private readonly HashSet<BattleUnitState> holders = new HashSet<BattleUnitState>();

            public EncoreTalentEffect(BattleController controller, BattleUnitState source, float chance, float ratio)
            {
                this.controller = controller;
                this.source = source;
                this.chance = chance;
                this.ratio = ratio;
            }

            public string Id => EncoreEffectId;
            public BattleUnitState Source => source;
            public int DisplayBuffId => EncoreDisplayBuffId;

            public bool CanAcquire(in TalentAcquireContext context)
            {
                return context.Camp == BattleCamp.Character && context.SourceAlive;
            }

            public void OnAcquire(BattleUnitState unit)
            {
                holders.Add(unit);
            }

            public void OnRelease(BattleUnitState unit)
            {
                holders.Remove(unit);
            }

            public void OnDamageDealt(in BattleDamageEvent e)
            {
                // 防递归：追奏伤害不再触发追奏
                if (e.Kind == BattleDamageKind.FollowUp) return;
                if (!holders.Contains(e.Attacker)) return;
                if (source == null || !source.IsAlive) return;
                if (e.Target == null || !e.Target.IsAlive || e.Target.Camp != BattleCamp.Enemy) return;
                if (controller == null || controller.Rng == null) return;
                if (controller.Rng.NextDouble() >= chance) return;

                float energyRecharge = source.Stats.EnergyRecharge + TalentAura.GetEnergyRecharge(source.UnitId);
                float baseValue = source.Stats.MaxHp * energyRecharge * ratio;
                if (baseValue <= 0f) return;

                BattleDamage.Apply(controller, new BattleDamageRequest(
                    e.Attacker, source, e.Target, BattleDamageKind.FollowUp,
                    baseValue: baseValue, useDefense: false));
            }
        }
    }
}
