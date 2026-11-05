using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Gear;
using Framework.Gameplay.Abilities.Stat;
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
    /// 缓存键：角色 = unitId，敌人 = id@等级（EnemyCacheKey，如 EN_Solider_AR_0001@1，1 级也带后缀）；两源装配到同一形状。
    /// 全部表经 DataTableManager 加载（进程级缓存，与养成域/技能域共享，无二次 IO）。
    /// </summary>
    public static class BattleDataModule
    {
        // 移动/攻击朝向默认转速（2026-11-05：rotateSpeed 已移出 Character/Enemy 表，沿用现有攻击转向链路）
        private const float DefaultMoveRotateSpeed = 360f;

        /// <summary>
        /// 普攻解析包：倍率与固定加成已按等级折算完毕，BattleMath 直接消费；
        /// Skill/Effect 行引用保留给切片 2（多效果/描述展示）。
        /// </summary>
        public readonly struct NormalAttackData
        {
            // 普攻技能行（CharacterSkillTable）
            public readonly CharacterSkillRow Skill;

            // 普攻效果行（SkillEffectTable；首切片普攻取首效果）
            public readonly SkillEffectRow Effect;

            // 结算用的普攻等级（角色 = 养成档案 skillLevelNormal，敌人恒 1）
            public readonly int Level;

            // 攻击力倍率 = Effect.GetValueAtLevel(Level)
            public readonly float Rate;

            // 固定伤害加成 = CharacterSkillRow.params 的 atkadd（缺键按 0）
            public readonly float AtkAdd;

            public NormalAttackData(CharacterSkillRow skill, SkillEffectRow effect, int level, float rate, float atkAdd)
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

        // 等级成长表缓存（敌人等级乘区数据源；v1 与角色共用 CharacterLevelTable 曲线，
        // 将来敌用独立曲线 = 本表换 EnemyLevelTable 一行 + 行类同形状）
        private static DataTable<CharacterLevelRow, int> levelTable;

        // 波次表缓存
        private static DataTable<WaveRow, string> waveTable;

        // 关卡表缓存（守点血量/点数经济/波次序列/胜负策略，一行一关）
        private static DataTable<StageRow, string> stageTable;

        // 技能表缓存（角色普攻走 SkillDataLibrary 编码定位；敌人普攻按主键直查）
        private static DataTable<CharacterSkillRow, int> skillTable;

        // 技能效果表缓存（SkillDataLibrary 只管 CharacterSkillTable，效果表战斗侧自管）
        private static DataTable<SkillEffectRow, int> skillEffectTable;

        // 解锁节点表缓存（天赋 effectKey/params 来源，2026-11-04 天赋系统）
        private static DataTable<UnlockNodeRow, int> unlockNodeTable;

        // 加载任务（并发调用共享同一次加载）
        private static Task<bool> loadTask;

        // 单位数值缓存：unitId（CHxxxx）与 enemyId（ENxxxx）统一键
        private static readonly Dictionary<string, BattleUnitStats> statsCache = new Dictionary<string, BattleUnitStats>();

        // 普攻解析包缓存：同上统一键
        private static readonly Dictionary<string, NormalAttackData> normalAttackCache = new Dictionary<string, NormalAttackData>();

        // 单位状态数据白值基底缓存（stat 能力）：键同 statsCache（角色=unitId，敌人=id@等级）；
        // 这里只存 base 字典——TryCreateStatData 按基底逐单位出独立实例（敌人同 id 多实例各持己份，
        // buff 条目不串单位）
        private static readonly Dictionary<string, Dictionary<StatType, float>> statBaseCache = new Dictionary<string, Dictionary<StatType, float>>();

        // 关卡波次编组缓存：stageId → 按 StageRow.WaveIds 顺序的波次编组
        private static readonly Dictionary<string, List<WaveGroup>> waveCache = new Dictionary<string, List<WaveGroup>>();

        /// <summary>
        /// 加载域：确保战斗相关表全部就位（幂等，并发调用共享同一次加载）；
        /// 任一表失败时保持未加载状态，下次调用重试。命中 DataTableManager 缓存时近似零开销。
        /// </summary>
        public static async Task<bool> EnsureLoadedAsync()
        {
            if (characterTable != null && enemyTable != null && waveTable != null
                && stageTable != null && skillTable != null && skillEffectTable != null
                && levelTable != null) return true;

            loadTask ??= LoadAllAsync();
            return await loadTask;
        }

        // 逐表加载：任一失败整批判败（保持 null，下次重试）；成功表靠 DataTableManager 缓存幂等。
        // 注意起载顺序：SkillDataLibrary 内部同样请求 CharacterSkillTable，必须等六表直载就位后再起——
        // Play 真异步下两路同键并发会双双穿过 DataTableManager 缓存检查（check-then-act 无去重），
        // 后完成者 Add 撞重复键；EditMode 同步表源串行完成，此竞态测不出（2026-10-17 Play 首暴）。
        private static async Task<bool> LoadAllAsync()
        {
            Task<DataTable<CharacterRow, string>> characterTask =
                DataTableManager.Instance.LoadTableAsync<CharacterRow, string>("CharacterTable");
            Task<DataTable<EnemyRow, string>> enemyTask =
                DataTableManager.Instance.LoadTableAsync<EnemyRow, string>("EnemyTable");
            Task<DataTable<WaveRow, string>> waveTask =
                DataTableManager.Instance.LoadTableAsync<WaveRow, string>("WaveTable");
            Task<DataTable<StageRow, string>> stageTask =
                DataTableManager.Instance.LoadTableAsync<StageRow, string>("StageTable");
            Task<DataTable<CharacterSkillRow, int>> skillTask =
                DataTableManager.Instance.LoadTableAsync<CharacterSkillRow, int>("CharacterSkillTable");
            Task<DataTable<SkillEffectRow, int>> effectTask =
                DataTableManager.Instance.LoadTableAsync<SkillEffectRow, int>("SkillEffectTable");
            Task<DataTable<CharacterLevelRow, int>> levelTask =
                DataTableManager.Instance.LoadTableAsync<CharacterLevelRow, int>("CharacterLevelTable");
            Task<DataTable<UnlockNodeRow, int>> unlockNodeTask =
                DataTableManager.Instance.LoadTableAsync<UnlockNodeRow, int>("UnlockNodeTable");

            characterTable = await characterTask;
            enemyTable = await enemyTask;
            waveTable = await waveTask;
            stageTable = await stageTask;
            skillTable = await skillTask;
            skillEffectTable = await effectTask;
            // CharacterLevelTable 同键消费方：Preloader（启动期先行）与养成 resolver（EnsureLoaded 完成后才跑），
            // 均与本段无同帧并发窗口，直载安全（同 CharacterSkillTable 竞态口径的手推结论）
            levelTable = await levelTask;
            unlockNodeTable = await unlockNodeTask;

            // 八表已入 DataTableManager 缓存，SkillDataLibrary 的 CharacterSkillTable 请求此刻命中缓存，无并发窗口
            bool skillLibraryOk = (await LoadSkillLibraryAsync()).Ok;

            if (characterTable == null || enemyTable == null || waveTable == null || stageTable == null
                || skillTable == null || skillEffectTable == null || levelTable == null
                || unlockNodeTable == null || !skillLibraryOk)
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
            bool ok = SkillDataLibrary.TryGetSkillRow("CH0221", CharacterSkillRow.SkillTypes.Normal, out _);
            if (!ok) Debug.LogError("[BattleData] SkillDataLibrary 未就位（CharacterSkillTable 加载失败或参数索引未建成）。");
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
        /// <param name="stageId">关卡 id（StageTable 主键；波次序列与敌人来源）。</param>
        /// <param name="statsResolver">角色属性结算入口；null 时默认走 CharacterProgressionManager。
        /// 验证脚本注入 stub 以脱离养成域独立推演。</param>
        /// <returns>全部就位返回 true；任一关键数据缺失返回 false（缺失项已 LogError 定位）。</returns>
        public static async Task<bool> PreloadAsync(IReadOnlyList<string> unitIds, string stageId,
            Func<string, Task<CharacterStats>> statsResolver = null)
        {
            if (string.IsNullOrEmpty(stageId))
            {
                Debug.LogError("[BattleData] 预热失败：stageId 为空。");
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

            // 波次序列：按 StageRow.WaveIds 顺序编组（全表按 waveId 归组）
            if (!CacheWaves(stageId)) allOk = false;

            // 波次涉及的敌人：数值 + 普攻（编组 → 行 → 时间点条目；同 id 可多等级，按 (id, 等级) 去重）
            if (waveCache.TryGetValue(stageId, out List<WaveGroup> groups))
            {
                HashSet<(string EnemyId, int Level)> enemyKeys = new HashSet<(string, int)>();
                for (int g = 0; g < groups.Count; g++)
                {
                    IReadOnlyList<WaveRow> rows = groups[g].Rows;
                    for (int r = 0; r < rows.Count; r++)
                    {
                        IReadOnlyList<WaveSpawnEntry> points = rows[r].TimePoints;
                        for (int p = 0; p < points.Count; p++)
                        {
                            for (int u = 0; u < points[p].Units.Count; u++)
                                enemyKeys.Add((points[p].Units[u].EnemyId, points[p].Units[u].Level));
                        }
                    }
                }

                foreach ((string enemyId, int level) in enemyKeys)
                {
                    string statsKey = EnemyCacheKey(enemyId, level);
                    if (statsCache.ContainsKey(statsKey)) continue;
                    if (!TryCacheEnemy(enemyId, level)) allOk = false;
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

        /// <summary>
        /// 取单位状态数据（stat 能力出口：白值 base 只读 + 加成条目注册/注销，final 读取点惰性重算）。
        /// 每次调用产出全新独立实例（构造即拷贝 base，条目表互不共享）——敌人同 id 多实例各持己份，
        /// 战斗内 buff 增删互不影响；未预热返回 false 并报错。
        /// </summary>
        public static bool TryCreateStatData(string id, out StatRuntimeData statData)
        {
            if (statBaseCache.TryGetValue(id, out Dictionary<StatType, float> baseValues))
            {
                statData = new StatRuntimeData(baseValues);
                return true;
            }
            statData = null;
            Debug.LogError($"[BattleData] 单位状态数据未预热：id={id}（确认 PreloadAsync 已覆盖该单位）");
            return false;
        }

        /// <summary>取技能 CD（秒；CharacterSkillTable.cooldown 列）；未配/未加载返回 false。</summary>
        public static bool TryGetSkillCooldown(int skillId, out float cooldown)
        {
            if (skillId > 0 && skillTable != null && skillTable.TryGet(skillId, out CharacterSkillRow row))
            {
                cooldown = row.Cooldown;
                return true;
            }
            cooldown = 0f;
            return false;
        }

        /// <summary>取技能能量回复量（energyGain 列：普攻行=命中回能 / E·EX 行=施放回能；配 0 不回能）。</summary>
        public static bool TryGetSkillEnergyGain(int skillId, out int gain)
        {
            if (skillId > 0 && skillTable != null && skillTable.TryGet(skillId, out CharacterSkillRow row))
            {
                gain = row.EnergyGain;
                return true;
            }
            gain = 0;
            return false;
        }

        /// <summary>取敌人击杀回能量（energyOnKill 列；每种敌人不同，boss 可配多）。</summary>
        public static bool TryGetEnemyEnergyOnKill(string enemyId, out int energy)
        {
            if (!string.IsNullOrEmpty(enemyId) && enemyTable != null && enemyTable.TryGet(enemyId, out EnemyRow row))
            {
                energy = row.EnergyOnKill;
                return true;
            }
            energy = 0;
            return false;
        }

        /// <summary>取技能效果 id 列表（CharacterSkillTable.effectIds 逗号分隔解析）。</summary>
        public static bool TryGetSkillEffects(int skillId, out IReadOnlyList<int> effectIds)
        {
            if (skillId > 0 && skillTable != null && skillTable.TryGet(skillId, out CharacterSkillRow row)
                && !string.IsNullOrEmpty(row.EffectIds))
            {
                string[] parts = row.EffectIds.Split(',');
                var list = new List<int>(parts.Length);
                for (int i = 0; i < parts.Length; i++)
                {
                    if (int.TryParse(parts[i].Trim(), out int id)) list.Add(id);
                }
                effectIds = list;
                return list.Count > 0;
            }
            effectIds = null;
            return false;
        }

        /// <summary>取解锁节点行（UnlockNodeTable 主键查；天赋 effectKey/params）。</summary>
        public static bool TryGetUnlockNode(int nodeId, out UnlockNodeRow row)
        {
            if (unlockNodeTable != null && unlockNodeTable.TryGet(nodeId, out row)) return true;
            row = null;
            return false;
        }

        /// <summary>取技能效果行（SkillEffectTable 主键查）。</summary>
        public static bool TryGetSkillEffect(int effectId, out SkillEffectRow row)
        {
            if (skillEffectTable != null && skillEffectTable.TryGet(effectId, out row)) return true;
            row = null;
            return false;
        }

        /// <summary>取关卡波次编组（按 StageTable.waveIds 顺序）；未预热返回空并报错。</summary>
        public static IReadOnlyList<WaveGroup> GetWaveGroups(string stageId)
        {
            if (!string.IsNullOrEmpty(stageId) && waveCache.TryGetValue(stageId, out List<WaveGroup> groups)) return groups;
            Debug.LogError($"[BattleData] 波次编组未预热：stageId={stageId}");
            return Array.Empty<WaveGroup>();
        }

        #endregion

        /// <summary>失效域：清空全部战斗缓存（战斗结束调用；表缓存留在 DataTableManager，不清）。</summary>
        public static void Clear()
        {
            statsCache.Clear();
            normalAttackCache.Clear();
            statBaseCache.Clear();
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
                row.MoveSpeed, row.AtkInterval, row.Range,
                DefaultMoveRotateSpeed, row.RotateNode,
                row.BulletSpread, row.HeadNode, 0f,
                maxEnergy: row.MaxEnergy, energyRecharge: stats.EnergyRecharge,
                skillEId: row.SkillEId, skillQId: row.SkillQId, normalAtkId: row.NormalAtkId,
                damageBonus: stats.DamageBonus, healBonus: stats.HealBonus,
                targetingPolicy: row.TargetingPolicy);

            // stat 链装配（过渡口径）：base 直接取 CharacterStats final 八项（养成加成已折入，无独立条目），
            // 战斗专有三项取 CharacterRow；迁移批换真白值 + 节点/武器/装备 modifier 灌入后本注释作废
            statBaseCache[unitId] = CreateCharacterStatBase(stats, row);

            bool found = SkillDataLibrary.TryGetSkillRow(unitId, CharacterSkillRow.SkillTypes.Normal, out CharacterSkillRow skillRow);
            return CacheNormalAttack(unitId, found, skillRow, GetCharacterNormalLevel(unitId));
        }

        // 装配敌人：EnemyRow（1 级基准）× CharacterLevelTable.multiplier(level) → 统一形状 + 普攻（主键直查，不走角色编码约定）
        private static bool TryCacheEnemy(string enemyId, int level)
        {
            if (!enemyTable.TryGet(enemyId, out EnemyRow row))
            {
                Debug.LogError($"[BattleData] EnemyTable 无敌人行：enemyId={enemyId}");
                return false;
            }
            if (!levelTable.TryGet(level, out CharacterLevelRow levelRow))
            {
                Debug.LogError($"[BattleData] CharacterLevelTable 无等级行：level={level}（enemyId={enemyId}），敌人装配拒绝。");
                return false;
            }

            // 成长三项：1 级基准 × 等级系数，向下取整（与角色白值取整口径一致）；③数值与 stat 基底共用同一组值
            int maxHp = Mathf.FloorToInt(row.Hp * levelRow.Multiplier);
            int atk = Mathf.FloorToInt(row.Atk * levelRow.Multiplier);
            int def = Mathf.FloorToInt(row.Def * levelRow.Multiplier);

            string statsKey = EnemyCacheKey(enemyId, level);
            statsCache[statsKey] = new BattleUnitStats(
                maxHp, atk, def, row.CritRate, row.CritDmg,
                row.MoveSpeed, row.AtkInterval, row.Range,
                DefaultMoveRotateSpeed, row.RotateNode,
                row.BulletSpread, row.HeadNode, row.StopLateralOffset, row.BlockRadius, row.GuardPointDamage,
                maxEnergy: 0, energyRecharge: 1f, normalAtkId: row.NormalAtkId);

            // stat 链装配：敌人无养成链，成长三项（已折算）+ 其余字段直装即真白值，
            // 空加成条目表（战斗内 buff 走 AddModifier）
            statBaseCache[statsKey] = CreateEnemyStatBase(row, maxHp, atk, def);

            bool found = skillTable.TryGet(row.NormalAtkId, out CharacterSkillRow skillRow);
            return CacheNormalAttack(statsKey, found, skillRow, 1);
        }

        /// <summary>
        /// 敌人统一缓存键：id@等级（如 EN_Solider_AR_0001@10；1 级也带后缀，键形统一）。
        /// 该键只作数据查找（stats/normalAttack/statBase 三缓存与 pack）；单位身份、索敌、视图地址仍用纯 id。
        /// </summary>
        public static string EnemyCacheKey(string enemyId, int level) => $"{enemyId}@{level}";

        // 过渡口径装配角色状态白值基底：base = CharacterStats final 八项 + CharacterRow 战斗三项
        private static Dictionary<StatType, float> CreateCharacterStatBase(CharacterStats stats, CharacterRow row)
        {
            var baseValues = new Dictionary<StatType, float>
            {
                [StatType.MaxHp] = stats.MaxHp,
                [StatType.Atk] = stats.Atk,
                [StatType.Def] = stats.Def,
                [StatType.CritRate] = stats.CritRate,
                [StatType.CritDmg] = stats.CritDmg,
                [StatType.EnergyRecharge] = stats.EnergyRecharge,
                [StatType.DamageBonus] = stats.DamageBonus,
                [StatType.HealBonus] = stats.HealBonus,
                [StatType.MoveSpeed] = row.MoveSpeed,
                [StatType.AtkInterval] = row.AtkInterval,
                [StatType.Range] = row.Range,
            };
            return baseValues;
        }

        // 敌人状态白值基底：成长三项已按等级折算传入，其余字段直装（无养成链，base 即 final）
        private static Dictionary<StatType, float> CreateEnemyStatBase(EnemyRow row, int maxHp, int atk, int def)
        {
            var baseValues = new Dictionary<StatType, float>
            {
                [StatType.MaxHp] = maxHp,
                [StatType.Atk] = atk,
                [StatType.Def] = def,
                [StatType.CritRate] = row.CritRate,
                [StatType.CritDmg] = row.CritDmg,
                [StatType.MoveSpeed] = row.MoveSpeed,
                [StatType.AtkInterval] = row.AtkInterval,
                [StatType.Range] = row.Range,
            };
            return baseValues;
        }

        // 普攻解析：单效果假设（首切片普攻 effectIds 单值；多值取首效果并告警），atkadd 缺键按 0
        private static bool CacheNormalAttack(string id, bool found, CharacterSkillRow skillRow, int level)
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

        // 波次编组装配：WaveTable.Rows 全表按 waveId 归组（行主键 = id 列，组内保表序），
        // 再按 StageRow.WaveIds 顺序编组；关卡行缺失 / waveIds 引用不存在的波次 / 重复引用 → 当场报错拒绝
        private static bool CacheWaves(string stageId)
        {
            if (!stageTable.TryGet(stageId, out StageRow stage))
            {
                Debug.LogError($"[BattleData] StageTable 无关卡行：stageId={stageId}");
                return false;
            }
            string[] waveIds = stage.WaveIds;
            if (waveIds == null || waveIds.Length == 0)
            {
                Debug.LogError($"[BattleData] StageTable 未配波次：stageId={stageId}");
                return false;
            }

            IReadOnlyList<WaveRow> rows = waveTable.Rows;
            Dictionary<string, List<WaveRow>> byWaveId = new Dictionary<string, List<WaveRow>>(rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                WaveRow row = rows[i];
                if (string.IsNullOrEmpty(row.WaveId))
                {
                    Debug.LogError($"[BattleData] WaveTable 行缺 waveId：id={row.Id}");
                    return false;
                }
                if (!byWaveId.TryGetValue(row.WaveId, out List<WaveRow> bucket))
                {
                    bucket = new List<WaveRow>();
                    byWaveId[row.WaveId] = bucket;
                }
                bucket.Add(row);
            }

            List<WaveGroup> groups = new List<WaveGroup>(waveIds.Length);
            var referenced = new HashSet<string>();
            for (int i = 0; i < waveIds.Length; i++)
            {
                string waveId = waveIds[i];
                if (!referenced.Add(waveId))
                {
                    Debug.LogError($"[BattleData] waveIds 重复引用波次：{waveId}（stageId={stageId}）");
                    return false;
                }
                if (!byWaveId.TryGetValue(waveId, out List<WaveRow> rowsOfWave))
                {
                    Debug.LogError($"[BattleData] WaveTable 无波次行：{waveId}（stageId={stageId}）");
                    return false;
                }
                groups.Add(new WaveGroup(waveId, rowsOfWave));
            }

            waveCache[stageId] = groups;
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
