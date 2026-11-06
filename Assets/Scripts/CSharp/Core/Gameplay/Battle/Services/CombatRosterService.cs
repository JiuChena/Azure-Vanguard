using System.Collections.Generic;
using UnityEngine;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 在场单位档案子服务：角色/敌人列表与索引、敌人生成、倒地/移除状态迁移、
    /// 目标引用清理、慢速状态推进。单位列表的单一写者。
    /// </summary>
    public sealed class CombatRosterService : ICombatSubservice
    {
        // 停走射程随机偏移幅度（±米；同种敌人停在略有差异的半径、聚堆点错开）。
        private const float StopRangeJitter = 0.3f;

        // 所属战斗服务（事件外发 / 数据查询）。
        private CombatService service;

        // 战斗数据包（敌人状态生成）。
        private BattleDataPack data;

        // 全部在场单位（角色 + 敌人，按出生顺序）。
        private readonly List<BattleUnitState> units = new List<BattleUnitState>();

        // 在场角色。
        private readonly List<BattleUnitState> characters = new List<BattleUnitState>();

        // 在场敌人。
        private readonly List<BattleUnitState> enemies = new List<BattleUnitState>();

        /// <summary>全部在场单位（只读）。</summary>
        public IReadOnlyList<BattleUnitState> Units => units;

        /// <summary>在场角色（只读）。</summary>
        public IReadOnlyList<BattleUnitState> Characters => characters;

        /// <summary>在场敌人（只读）。</summary>
        public IReadOnlyList<BattleUnitState> Enemies => enemies;

        /// <summary>已上场角色数（含倒地；倒地不空名额）。</summary>
        public int DeployedCharacterCount { get; private set; }

        /// <summary>当前存活敌人数（胜负评估用）。</summary>
        public int AliveEnemyCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < enemies.Count; i++)
                {
                    if (enemies[i].IsAlive) count++;
                }
                return count;
            }
        }

        /// <summary>绑定战斗服务与数据包（重复调用先清空）。</summary>
        /// <param name="combatService">所属战斗服务；不允许为 null。</param>
        /// <param name="dataPack">战斗数据包；不允许为 null。</param>
        public void Begin(CombatService combatService, BattleDataPack dataPack)
        {
            Reset();
            service = combatService;
            data = dataPack;
        }

        /// <summary>清空全部单位与索引。</summary>
        public void Reset()
        {
            units.Clear();
            characters.Clear();
            enemies.Clear();
            DeployedCharacterCount = 0;
            service = null;
            data = null;
        }

        /// <summary>按 id 查角色（只查角色列表）。</summary>
        /// <param name="unitId">角色 id。</param>
        /// <returns>命中单位；未找到为 null。</returns>
        public BattleUnitState FindCharacter(string unitId)
        {
            for (int i = 0; i < characters.Count; i++)
            {
                if (characters[i].UnitId == unitId) return characters[i];
            }
            return null;
        }

        /// <summary>任意阵营按 id 查找（击杀归属回能等；未命中返回 null）。</summary>
        /// <param name="unitId">单位 id。</param>
        /// <returns>命中单位；未找到为 null。</returns>
        public BattleUnitState FindUnit(string unitId)
        {
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i].UnitId == unitId) return units[i];
            }
            return null;
        }

        /// <summary>登记角色（部署时调用；不自动外发事件，由指令服务按流水顺序外发）。</summary>
        /// <param name="unit">角色单位。</param>
        public void AddCharacter(BattleUnitState unit)
        {
            if (unit == null) return;
            units.Add(unit);
            characters.Add(unit);
            DeployedCharacterCount++;
        }

        /// <summary>按波次回调生成敌人并外发 UnitSpawned。</summary>
        /// <param name="enemyId">敌人 id。</param>
        /// <param name="level">等级。</param>
        /// <param name="position">出生点。</param>
        /// <param name="route">行进路线；可为 null。</param>
        public void SpawnEnemy(string enemyId, int level, Vector3 position, IReadOnlyList<Vector3> route)
        {
            if (data == null) return;
            string statsKey = BattleDataModule.EnemyCacheKey(enemyId, level);
            if (!data.Stats.TryGetValue(statsKey, out BattleUnitStats stats))
            {
                Debug.LogError($"[CombatRoster] 敌人数值缺失，跳过生成：{statsKey}");
                return;
            }

            var state = new BattleUnitState(enemyId, BattleCamp.Enemy, stats, position, route, statsKey);
            // 停走射程每单位独立抽样：同种敌人停在不同半径、聚堆点错开
            state.StopRangeOffset = UnityEngine.Random.Range(-StopRangeJitter, StopRangeJitter);
            units.Add(state);
            enemies.Add(state);
            service?.RaiseUnitSpawned(state);
        }

        /// <summary>推进减速状态（SlowRemaining 递减 / 移速乘数恢复）。</summary>
        /// <param name="dt">本拍时长（秒）。</param>
        public void TickStatuses(float dt)
        {
            for (int i = 0; i < units.Count; i++)
            {
                BattleUnitState unit = units[i];
                if (unit.SlowRemaining <= 0f) continue;
                unit.SlowRemaining = Mathf.Max(0f, unit.SlowRemaining - dt);
                if (unit.SlowRemaining <= 0f) unit.MoveSlowFactor = 1f;
            }
        }

        /// <summary>把指定下标的角色标记为倒地并外发 UnitDowned。</summary>
        /// <param name="index">units 列表下标。</param>
        public void MarkDownedAt(int index)
        {
            BattleUnitState unit = units[index];
            unit.Vital = UnitVitalState.Downed;
            unit.ClearCombatTarget();
            unit.TargetIsGuardPoint = false;
            service?.RaiseUnitDowned(unit);
        }

        /// <summary>移除指定下标的敌人、清理全场目标引用并外发 EnemyRemoved。</summary>
        /// <param name="index">units 列表下标。</param>
        public void RemoveEnemyAt(int index)
        {
            BattleUnitState unit = units[index];
            units.RemoveAt(index);
            enemies.Remove(unit);
            ClearTargetReferences(unit);
            service?.RaiseEnemyRemoved(unit);
        }

        /// <summary>清掉全场指向已移除单位的目标引用（实例优先，旧 id 兼容）。</summary>
        /// <param name="removed">已移除单位。</param>
        public void ClearTargetReferences(BattleUnitState removed)
        {
            if (removed == null) return;
            for (int i = 0; i < units.Count; i++)
            {
                BattleUnitState other = units[i];
                if (other == null || other == removed) continue;
                if (other.TargetUnit == removed)
                {
                    other.ClearCombatTarget();
                }
                else if (other.TargetUnit == null && other.TargetUnitId == removed.UnitId)
                {
                    other.TargetUnitId = null;
                }
            }
        }
    }
}
