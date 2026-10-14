using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Gear;
using Framework.Gameplay.Player;
using Framework.Gameplay.Progression;
using Framework.Gameplay.Unit;
using UnityEngine;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 战斗数据模块（①数据层→③模拟层的唯一出接口，static 门面，同 SkillDataLibrary 惯例）。
    /// 四域：加载（EnsureLoadedAsync，幂等并发共享）、预热（PreloadAsync，进战斗一次性装配）、
    /// 查询（战斗中纯同步 O(1) 字典直查，零 async 零表 IO）、失效（Clear，缓存生命周期 = 一场战斗）。
    /// 缓存以 unitId/enemyId 为键统一存放 BattleUnitStats（角色/敌人两源装配到同一形状）。
    /// 全部表经 DataTableManager 加载（进程级缓存，与养成域/技能域共享，无二次 IO）。
    /// </summary>
    public static class BattleDataModule
    {
        /// <summary>
        /// 普攻解析包：倍率与固定加成已按等级折算完毕，BattleMath 直接消费；
        /// Skill/Effect 行引用保留给切片 2（多效果/描述展示）。
        /// </summary>
        public readonly struct NormalAttackData
        {
            // 普攻技能行（SkillTable）
            public readonly SkillRow Skill;

            // 普攻效果行（SkillEffectTable；首切片普攻取首效果）
            public readonly SkillEffectRow Effect;

            // 结算用的普攻等级（角色 = 养成档案 skillLevelNormal，敌人恒 1）
            public readonly int Level;

            // 攻击力倍率 = Effect.GetValueAtLevel(Level)
            public readonly float Rate;

            // 固定伤害加成 = SkillRow.params 的 atkadd（缺键按 0）
            public readonly float AtkAdd;

            public NormalAttackData(SkillRow skill, SkillEffectRow effect, int level, float rate, float atkAdd)
            {
                Skill = skill;
                Effect = effect;
                Level = level;
                Rate = rate;
                AtkAdd = atkAdd;
            }
        }

        // 角色表缓存（装配角色战斗字段用；与养成域共享 DataTableManager 缓存）
        private static DataTable<CharacterRow, string> characterTable;

        // 敌人表缓存
        private static DataTable<EnemyRow, string> enemyTable;

        // 波次表缓存
        private static DataTable<WaveRow, string> waveTable;

        // 技能表缓存（角色普攻走 SkillDataLibrary 编码定位；敌人普攻按主键直查）
        private static DataTable<SkillRow, int> skillTable;

        // 技能效果表缓存（SkillDataLibrary 只管 SkillTable，效果表战斗侧自管）
        private static DataTable<SkillEffectRow, int> skillEffectTable;

        // 加载任务（并发调用共享同一次加载）
        private static Task<bool> loadTask;

        // 单位数值缓存：unitId（CHxxxx）与 enemyId（ENxxxx）统一键
        private static readonly Dictionary<string, BattleUnitStats> statsCache = new Dictionary<string, BattleUnitStats>();

        // 普攻解析包缓存：同上统一键
        private static readonly Dictionary<string, NormalAttackData> normalAttackCache = new Dictionary<string, NormalAttackData>();

        // 关卡波次序列缓存：LevelConfig 资产名 → 按 waveIds 顺序的行列表
        private static readonly Dictionary<string, List<WaveRow>> waveCache = new Dictionary<string, List<WaveRow>>();

        /// <summary>
        /// 加载域：确保战斗相关表全部就位（幂等，并发调用共享同一次加载）；
        /// 任一表失败时保持未加载状态，下次调用重试。命中 DataTableManager 缓存时近似零开销。
        /// </summary>
        public static async Task<bool> EnsureLoadedAsync()
        {
            if (characterTable != null && enemyTable != null && waveTable != null
                && skillTable != null && skillEffectTable != null) return true;

            loadTask ??= LoadAllAsync();
            return await loadTask;
        }

        // 逐表加载：任一失败整批判败（保持 null，下次重试）；成功表靠 DataTableManager 缓存幂等
        private static async Task<bool> LoadAllAsync()
        {
            Task<SkillDataLibraryLoadGuard> skillLibraryTask = LoadSkillLibraryAsync();

            Task<DataTable<CharacterRow, string>> characterTask =
                DataTableManager.Instance.LoadTableAsync<CharacterRow, string>("CharacterTable");
            Task<DataTable<EnemyRow, string>> enemyTask =
                DataTableManager.Instance.LoadTableAsync<EnemyRow, string>("EnemyTable");
            Task<DataTable<WaveRow, string>> waveTask =
                DataTableManager.Instance.LoadTableAsync<WaveRow, string>("WaveTable");
            Task<DataTable<SkillRow, int>> skillTask =
                DataTableManager.Instance.LoadTableAsync<SkillRow, int>("SkillTable");
            Task<DataTable<SkillEffectRow, int>> effectTask =
                DataTableManager.Instance.LoadTableAsync<SkillEffectRow, int>("SkillEffectTable");

            characterTable = await characterTask;
            enemyTable = await enemyTask;
            waveTable = await waveTask;
            skillTable = await skillTask;
            skillEffectTable = await effectTask;
            bool skillLibraryOk = (await skillLibraryTask).Ok;

            if (characterTable == null || enemyTable == null || waveTable == null
                || skillTable == null || skillEffectTable == null || !skillLibraryOk)
            {
                Debug.LogError("[BattleData] 战斗表加载未全部就位（缺失表见上方 LogError），预热中止。");
                loadTask = null;
                return false;
            }
            return true;
        }

        // SkillDataLibrary 的加载包装（它内部吞失败，这里只关心就位与否）
        private static async Task<SkillDataLibraryLoadGuard> LoadSkillLibraryAsync()
        {
            await SkillDataLibrary.EnsureLoadedAsync();
            // 就位探针：随便探一条已配普攻（CH0221），定位失败说明参数索引未建成
            bool ok = SkillDataLibrary.TryGetSkillRow("CH0221", SkillRow.SkillTypes.Normal, out _);
            if (!ok) Debug.LogError("[BattleData] SkillDataLibrary 未就位（SkillTable 加载失败或参数索引未建成）。");
            return new SkillDataLibraryLoadGuard(ok);
        }

        // 探针结果包装（避免元组在 async Task 里跨框架兼容疑虑，显式命名）
        private readonly struct SkillDataLibraryLoadGuard
        {
            public readonly bool Ok;
            public SkillDataLibraryLoadGuard(bool ok) { Ok = ok; }
        }

        /// <summary>
        /// 预热域：进战斗时一次性装配——编队角色 + 关卡波次涉及的全部敌人。
        /// 战斗中查询路径（TryGet 系）从此零 async 零表 IO 零养成结算。
        /// </summary>
        /// <param name="unitIds">编队角色 id（CHxxxx）。</param>
        /// <param name="level">关卡配置（波次序列与敌人来源）。</param>
        /// <param name="statsResolver">角色属性结算入口；null 时默认走 CharacterProgressionManager。
        /// 验证脚本注入 stub 以脱离养成域独立推演。</param>
        /// <returns>全部就位返回 true；任一关键数据缺失返回 false（缺失项已 LogError 定位）。</returns>
        public static async Task<bool> PreloadAsync(IReadOnlyList<string> unitIds, LevelConfig level,
            Func<string, Task<CharacterStats>> statsResolver = null)
        {
            if (level == null)
            {
                Debug.LogError("[BattleData] 预热失败：LevelConfig 为空。");
                return false;
            }
            if (!await EnsureLoadedAsync()) return false;

            Func<string, Task<CharacterStats>> resolver =
                statsResolver ?? (id => CharacterProgressionManager.Instance.CalculateStatsAsync(id));

            bool allOk = true;

            // 编队角色：养成结算 × CharacterRow → 统一形状
            for (int i = 0; i < unitIds.Count; i++)
            {
                string unitId = unitIds[i];
                if (statsCache.ContainsKey(unitId)) continue;

                CharacterStats stats = await resolver(unitId);
                if (stats.MaxHp <= 0)
                {
                    Debug.LogError($"[BattleData] 角色属性结算异常（MaxHp<=0）：unitId={unitId}");
                    allOk = false;
                    continue;
                }
                if (!TryCacheCharacter(unitId, stats)) allOk = false;
            }

            // 波次序列：按 waveIds 有序装配（WaveIndex 升序互证）
            if (!CacheWaves(level)) allOk = false;

            // 波次涉及的敌人：数值 + 普攻
            if (waveCache.TryGetValue(level.name, out List<WaveRow> waves))
            {
                HashSet<string> enemyIds = new HashSet<string>();
                for (int i = 0; i < waves.Count; i++) enemyIds.Add(waves[i].EnemyId);

                foreach (string enemyId in enemyIds)
                {
                    if (statsCache.ContainsKey(enemyId)) continue;
                    if (!TryCacheEnemy(enemyId)) allOk = false;
                }
            }

            return allOk;
        }

        #region 查询域（战斗中纯同步，O(1) 直查）

        /// <summary>取单位数值（角色或敌人统一键）；未预热返回 false 并报错。</summary>
        public static bool TryGetUnitStats(string id, out BattleUnitStats stats)
        {
            if (statsCache.TryGetValue(id, out stats)) return true;
            Debug.LogError($"[BattleData] 单位数值未预热：id={id}（确认 PreloadAsync 已覆盖该单位）");
            return false;
        }

        /// <summary>取普攻解析包（rate/atkadd 已按等级折算）；未预热返回 false 并报错。</summary>
        public static bool TryGetNormalAttack(string id, out NormalAttackData data)
        {
            if (normalAttackCache.TryGetValue(id, out data)) return true;
            Debug.LogError($"[BattleData] 普攻数据未预热：id={id}");
            return false;
        }

        /// <summary>按主键取波次行。</summary>
        public static bool TryGetWave(string waveId, out WaveRow row)
        {
            if (waveTable != null && waveTable.TryGet(waveId, out row)) return true;
            row = null;
            Debug.LogError($"[BattleData] WaveTable 无波次行：{waveId}");
            return false;
        }

        /// <summary>取关卡波次序列（按 waveIds 顺序）；未预热返回空并报错。</summary>
        public static IReadOnlyList<WaveRow> GetWaves(LevelConfig level)
        {
            if (level != null && waveCache.TryGetValue(level.name, out List<WaveRow> waves)) return waves;
            Debug.LogError($"[BattleData] 波次序列未预热：level={level?.name}");
            return Array.Empty<WaveRow>();
        }

        #endregion

        /// <summary>失效域：清空全部战斗缓存（战斗结束调用；表缓存留在 DataTableManager，不清）。</summary>
        public static void Clear()
        {
            statsCache.Clear();
            normalAttackCache.Clear();
            waveCache.Clear();
            loadTask = null;
        }

        // 装配角色：CharacterRow（战斗字段）× CharacterStats（养成结算）→ 统一形状 + 普攻
        private static bool TryCacheCharacter(string unitId, CharacterStats stats)
        {
            if (!characterTable.TryGet(unitId, out CharacterRow row))
            {
                Debug.LogError($"[BattleData] CharacterTable 无角色行：unitId={unitId}");
                return false;
            }

            statsCache[unitId] = new BattleUnitStats(
                stats.MaxHp, stats.Atk, stats.Def, stats.CritRate, stats.CritDmg,
                row.MoveSpeed, row.AtkInterval, row.Range);

            bool found = SkillDataLibrary.TryGetSkillRow(unitId, SkillRow.SkillTypes.Normal, out SkillRow skillRow);
            return CacheNormalAttack(unitId, found, skillRow, GetCharacterNormalLevel(unitId));
        }

        // 装配敌人：EnemyRow → 统一形状 + 普攻（SkillTable 主键直查，不走角色编码约定）
        private static bool TryCacheEnemy(string enemyId)
        {
            if (!enemyTable.TryGet(enemyId, out EnemyRow row))
            {
                Debug.LogError($"[BattleData] EnemyTable 无敌人行：enemyId={enemyId}");
                return false;
            }

            statsCache[enemyId] = new BattleUnitStats(
                row.Hp, row.Atk, row.Def, row.CritRate, row.CritDmg,
                row.MoveSpeed, row.AtkInterval, row.Range);

            bool found = skillTable.TryGet(row.NormalAtkId, out SkillRow skillRow);
            return CacheNormalAttack(enemyId, found, skillRow, 1);
        }

        // 普攻解析：单效果假设（首切片普攻 effectIds 单值；多值取首效果并告警），atkadd 缺键按 0
        private static bool CacheNormalAttack(string id, bool found, SkillRow skillRow, int level)
        {
            if (!found || skillRow == null)
            {
                Debug.LogError($"[BattleData] 普攻行缺失：id={id}");
                return false;
            }

            int[] effectIds = skillRow.EffectIdList;
            if (effectIds.Length == 0)
            {
                Debug.LogError($"[BattleData] 普攻无效果行：id={id}, skillId={skillRow.SkillId}");
                return false;
            }
            if (effectIds.Length > 1)
            {
                Debug.LogWarning($"[BattleData] 普攻多效果行，取首效果：skillId={skillRow.SkillId}");
            }
            if (!skillEffectTable.TryGet(effectIds[0], out SkillEffectRow effectRow))
            {
                Debug.LogError($"[BattleData] SkillEffectTable 无效果行：effectId={effectIds[0]}");
                return false;
            }

            float atkAdd = 0f;
            if (SkillDataLibrary.TryResolveValues(skillRow, level, out Dictionary<string, float> values)
                && values.TryGetValue("atkadd", out float resolved))
            {
                atkAdd = resolved;
            }

            normalAttackCache[id] = new NormalAttackData(skillRow, effectRow, level,
                effectRow.GetValueAtLevel(level), atkAdd);
            return true;
        }

        // 波次序列装配：waveIds 顺序为准，WaveIndex 升序互证（不一致当场报错拒绝）
        private static bool CacheWaves(LevelConfig level)
        {
            string[] waveIds = level.WaveIds;
            if (waveIds == null || waveIds.Length == 0)
            {
                Debug.LogError($"[BattleData] LevelConfig 未配波次：level={level.name}");
                return false;
            }

            List<WaveRow> waves = new List<WaveRow>(waveIds.Length);
            for (int i = 0; i < waveIds.Length; i++)
            {
                if (!waveTable.TryGet(waveIds[i], out WaveRow row))
                {
                    Debug.LogError($"[BattleData] WaveTable 无波次行：{waveIds[i]}（level={level.name}）");
                    return false;
                }
                waves.Add(row);
            }

            for (int i = 1; i < waves.Count; i++)
            {
                if (waves[i].WaveIndex <= waves[i - 1].WaveIndex)
                {
                    Debug.LogError($"[BattleData] 波次序号与 waveIds 顺序不一致：level={level.name}, " +
                        $"{waves[i - 1].Id}({waves[i - 1].WaveIndex}) -> {waves[i].Id}({waves[i].WaveIndex})");
                    return false;
                }
            }

            waveCache[level.name] = waves;
            return true;
        }

        // 角色普攻等级：养成档案 skillLevelNormal；仓库未命中按 1 级（未喂养）
        private static int GetCharacterNormalLevel(string unitId)
        {
            return CharacterWarehouseManager.Instance.TryGetEntry(unitId, out CharacterEntry entry)
                ? entry.skillLevelNormal
                : 1;
        }
    }
}
