using System;
using UnityEngine;

namespace Framework.Gameplay.Battle
{
    /// <summary>伤害来源类型（天赋追奏用 FollowUp 做递归保护）。</summary>
    public enum BattleDamageKind
    {
        NormalAttack,
        Skill,
        FollowUp,
        Other,
    }

    /// <summary>一次伤害请求：Attacker = 归属者（击杀/回能），StatSource = 属性来源（可与攻击者不同）。</summary>
    public readonly struct BattleDamageRequest
    {
        public readonly BattleUnitState Attacker;
        public readonly BattleUnitState StatSource;
        public readonly BattleUnitState Target;
        public readonly BattleDamageKind Kind;
        // 攻击型伤害参数（BaseValue > 0 时走数值型，忽略下面三项）
        public readonly float Rate;
        public readonly float AtkAdd;
        // 数值型伤害基础值（追奏）
        public readonly float BaseValue;
        // 是否减目标防御（攻击型 true；追奏 false）
        public readonly bool UseDefense;

        public BattleDamageRequest(BattleUnitState attacker, BattleUnitState statSource, BattleUnitState target,
            BattleDamageKind kind, float rate = 0f, float atkAdd = 0f, float baseValue = 0f, bool useDefense = true)
        {
            Attacker = attacker;
            StatSource = statSource ?? attacker;
            Target = target;
            Kind = kind;
            Rate = rate;
            AtkAdd = atkAdd;
            BaseValue = baseValue;
            UseDefense = useDefense;
        }
    }

    /// <summary>伤害结算事件（天赋/表现/HUD 监听）。</summary>
    public readonly struct BattleDamageEvent
    {
        public readonly BattleUnitState Attacker;
        public readonly BattleUnitState StatSource;
        public readonly BattleUnitState Target;
        public readonly int Amount;
        public readonly bool IsCrit;
        public readonly BattleDamageKind Kind;

        public BattleDamageEvent(BattleUnitState attacker, BattleUnitState statSource, BattleUnitState target,
            int amount, bool isCrit, BattleDamageKind kind)
        {
            Attacker = attacker;
            StatSource = statSource;
            Target = target;
            Amount = amount;
            IsCrit = isCrit;
            Kind = kind;
        }
    }

    /// <summary>
    /// 统一伤害入口（2026-11-04）：唯一写目标 Hp 的伤害结算点。
    /// 攻击型 = atk×rate+atkAdd，减防御；数值型 = baseValue，不减防御；两者都吃伤害加成与暴击。
    /// 结算后写 LastHitBy（归属者）并广播 DamageDealt（天赋追奏监听）。
    /// </summary>
    public static class BattleDamage
    {
        /// <summary>结算一次伤害并返回实际伤害；目标无效返回 0。</summary>
        public static int Apply(BattleController controller, in BattleDamageRequest request)
        {
            BattleUnitState target = request.Target;
            BattleUnitState source = request.StatSource ?? request.Attacker;
            BattleUnitState attacker = request.Attacker ?? source;
            if (target == null || !target.IsAlive || target.Hp <= 0 || source == null || controller == null) return 0;

            float damageBonus = source.Stats.DamageBonus + TalentAura.GetDamageBonus(source.UnitId);
            int def = request.UseDefense ? target.Stats.Def : 0;
            int amount;
            bool crit;
            if (request.BaseValue > 0f)
            {
                amount = BattleMath.CalcNumericDamage(request.BaseValue, damageBonus,
                    source.Stats.CritRate, source.Stats.CritDmg, controller.Rng, out crit);
            }
            else
            {
                amount = BattleMath.CalcDamage(source.Stats.Atk, request.Rate, request.AtkAdd, def,
                    source.Stats.CritRate, source.Stats.CritDmg, controller.Rng, damageBonus, out crit);
            }

            target.Hp = Math.Max(0, target.Hp - amount);
            target.LastHitBy = attacker.UnitId;
            controller.RaiseDamageDealt(new BattleDamageEvent(attacker, source, target, amount, crit, request.Kind));
            return amount;
        }
    }
}
