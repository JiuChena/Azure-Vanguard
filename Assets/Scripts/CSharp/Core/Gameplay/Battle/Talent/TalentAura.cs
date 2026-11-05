using System.Collections.Generic;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 战斗级天赋光环加成（2026-11-04）：按单位汇总各来源的充能效率/伤害加成。
    /// 能量结算与伤害结算读这里；容器 Dispose 时 Clear，避免跨战斗残留。
    /// </summary>
    public static class TalentAura
    {
        private sealed class Entry
        {
            public string SourceId;
            public float EnergyRecharge;
            public float DamageBonus;
        }

        private static readonly Dictionary<string, List<Entry>> Bonuses = new Dictionary<string, List<Entry>>();

        /// <summary>给单位登记一条来源加成（同来源覆盖，不叠加）。</summary>
        public static void Add(string unitId, string sourceId, float energyRecharge, float damageBonus)
        {
            if (string.IsNullOrEmpty(unitId) || string.IsNullOrEmpty(sourceId)) return;
            if (!Bonuses.TryGetValue(unitId, out List<Entry> list))
            {
                list = new List<Entry>();
                Bonuses[unitId] = list;
            }
            list.RemoveAll(entry => entry.SourceId == sourceId);
            list.Add(new Entry
            {
                SourceId = sourceId,
                EnergyRecharge = energyRecharge,
                DamageBonus = damageBonus,
            });
        }

        /// <summary>撤掉单位某来源的加成。</summary>
        public static void Remove(string unitId, string sourceId)
        {
            if (string.IsNullOrEmpty(unitId) || !Bonuses.TryGetValue(unitId, out List<Entry> list)) return;
            list.RemoveAll(entry => entry.SourceId == sourceId);
            if (list.Count == 0) Bonuses.Remove(unitId);
        }

        /// <summary>单位当前充能效率加成合计（0 = 无）。</summary>
        public static float GetEnergyRecharge(string unitId)
        {
            return Sum(unitId, true);
        }

        /// <summary>单位当前伤害加成合计（0 = 无）。</summary>
        public static float GetDamageBonus(string unitId)
        {
            return Sum(unitId, false);
        }

        /// <summary>撤掉某来源在所有单位上的加成（效果注销时用）。</summary>
        public static void RemoveAllForSource(string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId)) return;
            var emptyUnits = new List<string>();
            foreach (KeyValuePair<string, List<Entry>> pair in Bonuses)
            {
                pair.Value.RemoveAll(entry => entry.SourceId == sourceId);
                if (pair.Value.Count == 0) emptyUnits.Add(pair.Key);
            }
            for (int i = 0; i < emptyUnits.Count; i++) Bonuses.Remove(emptyUnits[i]);
        }

        /// <summary>清空全部（战斗结束/容器销毁）。</summary>
        public static void Clear()
        {
            Bonuses.Clear();
        }

        private static float Sum(string unitId, bool energyRecharge)
        {
            if (string.IsNullOrEmpty(unitId) || !Bonuses.TryGetValue(unitId, out List<Entry> list)) return 0f;
            float sum = 0f;
            for (int i = 0; i < list.Count; i++)
            {
                sum += energyRecharge ? list[i].EnergyRecharge : list[i].DamageBonus;
            }
            return sum;
        }
    }
}
