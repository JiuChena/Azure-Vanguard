using System.Collections.Generic;
using UnityEngine;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 场内单位注册表（静态）：单位出生/部署时登记 id → 单位状态 + head 节点，死亡出表注销。
    /// Core 侧静态表：②（CombatViewService）登记、②（子弹/转向）读取，Core 不反向引用 Bissness。
    /// 编辑器会话跨 run_script 存活（与 DataTableManager._tables 同坑），战斗拆除时 Clear、
    /// 验证脚本开头清空；反查时按 IsAlive + Unity null 双兜底。
    /// </summary>
    public static class FieldUnitRegistry
    {
        // 守点登记键（战斗唯一守点；子弹/转向打守点走同一入口，无 head 概念时守点 Transform 即"头部"）
        public const string GuardPointKey = "__guard_point__";

        // 注册表：单位 id → 场内信息（角色/敌人同形，主键 id）。
        private static readonly Dictionary<string, FieldUnitInfo> infos = new Dictionary<string, FieldUnitInfo>();

        // 实例注册表（2026-11-04）：敌人同 UnitId 多实例，锁头/取 GAS 必须按实例查，不能只认 id。
        private static readonly Dictionary<BattleUnitState, FieldUnitInfo> byUnit =
            new Dictionary<BattleUnitState, FieldUnitInfo>();

        /// <summary>登记场内单位（出生/部署时调用）。</summary>
        /// <param name="unitId">单位 id（身份/索敌键）。</param>
        /// <param name="unit">单位模拟状态；不允许为 null。</param>
        /// <param name="headNode">解析好的头部骨骼；未配/解析失败可为 null。</param>
        public static void Register(string unitId, BattleUnitState unit, Transform headNode)
        {
            var info = new FieldUnitInfo(unit, headNode);
            infos[unitId] = info;
            if (unit != null) byUnit[unit] = info;
        }

        /// <summary>登记守点（战斗启动时调用）；以守点自身 Transform 作"头部"。</summary>
        /// <param name="guardNode">守点 Transform。</param>
        public static void RegisterGuardPoint(Transform guardNode)
        {
            infos[GuardPointKey] = new FieldUnitInfo(null, guardNode);
        }

        /// <summary>注销单位（死亡出表时调用）。</summary>
        /// <param name="unitId">单位 id。</param>
        public static void Unregister(string unitId)
        {
            if (unitId == null) return;
            if (infos.TryGetValue(unitId, out FieldUnitInfo info))
            {
                infos.Remove(unitId);
                if (info.Unit != null) byUnit.Remove(info.Unit);
            }
        }

        /// <summary>按实例注销（敌人同 UnitId 多实例时不会误删其他实例）。</summary>
        public static void Unregister(BattleUnitState unit)
        {
            if (unit == null) return;
            byUnit.Remove(unit);
            if (infos.TryGetValue(unit.UnitId, out FieldUnitInfo info) && ReferenceEquals(info.Unit, unit))
            {
                infos.Remove(unit.UnitId);
            }
        }

        /// <summary>战斗拆除全清（守点一并清）。</summary>
        public static void Clear()
        {
            infos.Clear();
            byUnit.Clear();
        }

        /// <summary>取目标头部 Transform：已登记、单位存活（守点恒活）、head 未销毁才返回 true。</summary>
        /// <param name="unitId">目标单位 id 或守点保留键。</param>
        /// <param name="head">命中的头部 Transform；未命中为 null。</param>
        /// <returns>命中可锁头目标返回 true。</returns>
        public static bool TryGetHead(string unitId, out Transform head)
        {
            head = null;
            if (unitId == null || !infos.TryGetValue(unitId, out FieldUnitInfo info)) return false;
            if (info.HeadNode == null) return false;
            BattleUnitState unit = info.Unit;
            if (unit != null && (!unit.IsAlive || unit.Hp <= 0)) return false;
            head = info.HeadNode;
            return true;
        }

        /// <summary>按实例取目标头部（敌人同 UnitId 多实例时必须用本重载）。</summary>
        public static bool TryGetHead(BattleUnitState unit, out Transform head)
        {
            head = null;
            if (unit == null || !unit.IsAlive || unit.Hp <= 0) return false;
            if (!byUnit.TryGetValue(unit, out FieldUnitInfo info) || info.HeadNode == null) return false;
            head = info.HeadNode;
            return true;
        }
    }
}
