using System.Collections.Generic;
using UnityEngine;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 技能 CD 管理器（③ 模拟层，2026-10-27 技能方案）：入场角色技能 CD 统一注册，
    /// Step 节拍统一推进（暂停 = 不调 Step、倍速 = dt×scale 天然生效——资源计时禁放渲染帧链），
    /// 缩减/减免类效果由本管理器统一调配；感知层只读就绪判定。敌人无技能不注册。
    /// E 技能 CD 取 CharacterSkillTable.Cooldown（初始就绪——可用即放）；Q 大招无 CD 靠能量门槛。
    /// </summary>
    public sealed class SkillCooldownManager
    {
        // 单位 → (技能 id → 剩余 CD 秒)；剩余 <= 0 = 就绪
        private readonly Dictionary<BattleUnitState, Dictionary<int, float>> _cooldowns =
            new Dictionary<BattleUnitState, Dictionary<int, float>>();

        /// <summary>入场注册（角色部署/复活时调；初始就绪）。</summary>
        public void Register(BattleUnitState unit, int skillId)
        {
            if (unit == null || skillId <= 0) return;
            if (!_cooldowns.TryGetValue(unit, out Dictionary<int, float> map))
            {
                map = new Dictionary<int, float>();
                _cooldowns[unit] = map;
            }
            map[skillId] = 0f;
        }

        /// <summary>退场注销（单位死亡/进守点回收时调）。</summary>
        public void Unregister(BattleUnitState unit)
        {
            if (unit != null) _cooldowns.Remove(unit);
        }

        /// <summary>Step 节拍统一推进剩余 CD（clamp 0）。</summary>
        public void Tick(float dt)
        {
            foreach (Dictionary<int, float> map in _cooldowns.Values)
            {
                // 技能 id 集合在遍历中只减不减增，快照安全
                foreach (KeyValuePair<int, float> pair in new Dictionary<int, float>(map))
                {
                    if (pair.Value > 0f) map[pair.Key] = Mathf.Max(0f, pair.Value - dt);
                }
            }
        }

        /// <summary>技能是否就绪（剩余 CD <= 0；未注册按就绪处理）。</summary>
        public bool IsReady(BattleUnitState unit, int skillId)
        {
            if (unit == null || skillId <= 0) return true;
            return !_cooldowns.TryGetValue(unit, out Dictionary<int, float> map)
                || !map.TryGetValue(skillId, out float remaining)
                || remaining <= 0f;
        }

        /// <summary>技能施放后重设 CD（CD 值取 CharacterSkillTable.Cooldown，由调用方传入）。</summary>
        public void StartCooldown(BattleUnitState unit, int skillId, float cooldown)
        {
            if (unit == null || skillId <= 0) return;
            if (!_cooldowns.TryGetValue(unit, out Dictionary<int, float> map))
            {
                map = new Dictionary<int, float>();
                _cooldowns[unit] = map;
            }
            map[skillId] = Mathf.Max(0f, cooldown);
        }
    }
}
