using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Gear;
using Framework.Gameplay.Player;
using Framework.Gameplay.Unit;
using UnityEngine;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 战斗装配子服务：编队名单解析 → 数据预热 → 波次/数据包构建 → 守点/点数/评估器构造。
    /// 无状态工厂：返回 CombatSetupResult，不直接修改 CombatService。
    /// </summary>
    public sealed class CombatAssemblyService
    {
        /// <summary>
        /// 构建一场战斗所需的全部装配结果。
        /// </summary>
        /// <param name="config">场景配置；不允许为 null。</param>
        /// <returns>装配结果；校验/预热失败返回 null（错误已落日志）。</returns>
        public async Task<CombatSetupResult> BuildAsync(CombatBattleConfig config)
        {
            if (config == null)
            {
                Debug.LogError("[CombatAssembly] 战斗配置为空。");
                return null;
            }
            if (config.LevelConfig == null)
            {
                Debug.LogError("[CombatAssembly] levelConfig 未绑定（SO 拖拽）。");
                return null;
            }

            string stageId = config.LevelConfig.StageId;
            if (string.IsNullOrEmpty(stageId))
            {
                Debug.LogError("[CombatAssembly] LevelConfig.stageId 未配置。", config.LevelConfig);
                return null;
            }
            if (config.GuardPoint == null)
            {
                Debug.LogError("[CombatAssembly] 守点 guardPoint 未绑定（Transform 拖拽）。");
                return null;
            }

            string[] deployIds = ResolveDeployUnitIds();
            if (deployIds.Length == 0) return null;

            if (!await BattleDataModule.PreloadAsync(deployIds, stageId))
            {
                Debug.LogError("[CombatAssembly] 数据预热失败（缺失项见上方 BattleData 报错）。");
                return null;
            }

            // 波次序列：StageId 查 StageTable（GetWaveGroups 内部做关卡行/波次行引用报错）
            IReadOnlyList<WaveGroup> waveGroups = BattleDataModule.GetWaveGroups(stageId);
            BattleDataPack pack = BuildPack(deployIds, waveGroups);

            // 胜负策略：SO 关卡级注入；未配置时兜底守点默认实现
            BattleOutcomeEvaluator evaluator = config.LevelConfig.EvaluatorAsset != null
                ? config.LevelConfig.EvaluatorAsset.CreateEvaluator()
                : new GuardPointEvaluator();

            return new CombatSetupResult
            {
                DeployRoster = deployIds,
                WaveGroups = waveGroups,
                DataPack = pack,
                GuardPoint = new GuardPointState(config.LevelConfig.GuardPointMaxHp),
                GuardPosition = config.GuardPoint.position,
                Ledger = new PointLedger(config.LevelConfig.InitialPoints,
                    config.LevelConfig.PointSupplyInterval,
                    config.LevelConfig.PointSupplyAmount,
                    config.LevelConfig.PointCap),
                Evaluator = evaluator,
            };
        }

        // 部署名单唯一来源 = 编队管理器当前编队（战备面板保存）。
        private static string[] ResolveDeployUnitIds()
        {
            FormationTeamManager formation = FormationTeamManager.Instance;
            List<string> teamUnitIds = formation.GetTeamUnitIds(formation.CurrentTeamIndex);
            if (teamUnitIds.Count == 0)
            {
                Debug.LogError("[CombatAssembly] 编队为空（战备面板未编入角色）。");
                return Array.Empty<string>();
            }
            return teamUnitIds.ToArray();
        }

        // ⑥ 打包：预热结果 → 数据包。角色来自部署名单（CharacterTable.Cost）；
        // 敌人按 (id, 等级) 去重补入 Stats/NormalAttacks（键 = id@等级）。
        private static BattleDataPack BuildPack(string[] unitIds, IReadOnlyList<WaveGroup> waveGroups)
        {
            var pack = new BattleDataPack();
            var characters = DataTableManager.Instance
                .LoadTableAsync<CharacterRow, string>("CharacterTable").GetAwaiter().GetResult();

            for (int i = 0; i < unitIds.Length; i++)
            {
                string unitId = unitIds[i];
                if (BattleDataModule.TryGetUnitStats(unitId, out BattleUnitStats stats))
                {
                    pack.Stats[unitId] = stats;
                }
                if (BattleDataModule.TryGetNormalAttack(unitId, out BattleDataModule.NormalAttackData normalAttack))
                {
                    pack.NormalAttacks[unitId] = normalAttack;
                }
                if (characters != null && characters.TryGet(unitId, out CharacterRow row))
                {
                    pack.DeployCosts[unitId] = row.Cost;
                }
            }

            var packed = new HashSet<string>(unitIds);
            for (int g = 0; g < waveGroups.Count; g++)
            {
                IReadOnlyList<WaveRow> rows = waveGroups[g].Rows;
                for (int r = 0; r < rows.Count; r++)
                {
                    IReadOnlyList<WaveSpawnEntry> points = rows[r].TimePoints;
                    for (int p = 0; p < points.Count; p++)
                    {
                        for (int u = 0; u < points[p].Units.Count; u++)
                        {
                            string enemyId = points[p].Units[u].EnemyId;
                            string statsKey = string.IsNullOrEmpty(enemyId)
                                ? null
                                : BattleDataModule.EnemyCacheKey(enemyId, points[p].Units[u].Level);
                            if (statsKey == null || !packed.Add(statsKey)) continue;
                            if (BattleDataModule.TryGetUnitStats(statsKey, out BattleUnitStats stats))
                            {
                                pack.Stats[statsKey] = stats;
                            }
                            if (BattleDataModule.TryGetNormalAttack(statsKey, out BattleDataModule.NormalAttackData normalAttack))
                            {
                                pack.NormalAttacks[statsKey] = normalAttack;
                            }
                        }
                    }
                }
            }
            return pack;
        }
    }

    /// <summary>装配结果（CombatService 据此初始化各子服务）。</summary>
    public sealed class CombatSetupResult
    {
        // 部署名单（编队顺序）。
        public IReadOnlyList<string> DeployRoster;

        // 波次分组。
        public IReadOnlyList<WaveGroup> WaveGroups;

        // 战斗数据包。
        public BattleDataPack DataPack;

        // 守点状态。
        public GuardPointState GuardPoint;

        // 守点世界坐标。
        public Vector3 GuardPosition;

        // 点数台账。
        public PointLedger Ledger;

        // 胜负评估器。
        public BattleOutcomeEvaluator Evaluator;
    }
}
