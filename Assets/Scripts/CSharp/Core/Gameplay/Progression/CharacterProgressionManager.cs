using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Gear;
using Framework.Gameplay.Inventory;
using Framework.Gameplay.Item;
using Framework.Gameplay.Player;
using Framework.Gameplay.Trinket;
using Framework.Gameplay.Unit;
using UnityEngine;

namespace Framework.Gameplay.Progression
{
    /// <summary>
    /// 升级结算结果。
    /// </summary>
    public readonly struct LevelUpResult
    {
        // 是否结算成功
        public readonly bool Success;

        // 结算前等级
        public readonly int OldLevel;

        // 结算后等级
        public readonly int NewLevel;

        // 因达等级上限被丢弃的经验量（原神式溢出丢弃）
        public readonly int DiscardedExp;

        public LevelUpResult(bool success, int oldLevel, int newLevel, int discardedExp)
        {
            Success = success;
            OldLevel = oldLevel;
            NewLevel = newLevel;
            DiscardedExp = discardedExp;
        }

        // 统一失败结果（Success = false，其余归零）
        public static LevelUpResult Failed => default;
    }

    /// <summary>
    /// 自动填充（Fill）计划：按背包持有量算出"最多能升到哪一级"及刚好够到该级的
    /// 各技能书暂存数量；数量下标与调用方传入的技能书 id 数组一一对应。
    /// UI 的 Fill 按钮用它预暂存，确认结算仍走 UseExpItemsAsync / UseWeaponExpItemsAsync。
    /// </summary>
    public sealed class FillPlan
    {
        // 是否存在可行计划（false = 已达上限或一本都升不动，数量数组全零）
        public readonly bool HasPlan;

        // 填充可升到的目标等级（> 当前级）
        public readonly int TargetLevel;

        // 各技能书暂存数量（下标对齐调用方传入的 id 数组）
        public readonly long[] Counts;

        public FillPlan(bool hasPlan, int targetLevel, long[] counts)
        {
            HasPlan = hasPlan;
            TargetLevel = targetLevel;
            Counts = counts;
        }

        // 统一空计划（不可填充）
        public static FillPlan Empty => new FillPlan(false, 0, Array.Empty<long>());
    }

    /// <summary>
    /// 角色养成服务层：技能书喂养升级/突破/节点解锁/属性计算的业务规则入口。
    /// 只编排规则与校验：数据读写走 <see cref="CharacterWarehouseManager"/>（哑台账），
    /// 消耗扣除走 <see cref="InventoryManager"/>；配置表经 DataTableManager 惰性加载（项目首个消费者）。
    /// 任意 Async 操作前会自动确保表就绪；同步查询（GetLevelCap）需先 await EnsureReadyAsync。
    /// </summary>
    public class CharacterProgressionManager
    {
        // breakStage = 0（未突破）时的初始等级上限：突破表自 stage1 起无此行，按设计定稿（levelCap 20→80）取常量
        public const int InitialLevelCap = 20;

        /// <summary>
        /// 玩家可触发的拒绝（等级/材料/突破/节点解锁）：经事件总线（NoticeRequested）交给
        /// 常驻通知面板弹出，不发 LogError（Core 不依赖 UI）；LogError 只留给开发路径错误
        /// （调用方违约、配置表缺失、内部异常），玩家操作不走阻断日志。
        /// </summary>
        private static void NotifyPlayer(string message)
        {
            EventCenter.Instance.SetEventTrigger(EventNames.NoticeRequested, message);
        }

        // 本级所需经验公式：ExpToNext = BaseExpPerLevel × level^ExpCurvePower，向上取整（2026-10-06 拍板，常数单点可调）
        private const double BaseExpPerLevel = 80.0;

        // 经验曲线幂次
        private const double ExpCurvePower = 1.5;

        // 表名（DataTableManager 缓存键，与 ConfigTables 地址一致）
        private const string TableNameCharacter = "CharacterTable";
        private const string TableNameLevel = "LevelTable";
        private const string TableNameBreakthrough = "BreakthroughTable";
        private const string TableNameUnlockNode = "UnlockNodeTable";
        private const string TableNameWeapon = "WeaponTable";
        private const string TableNameWeaponLevel = "WeaponLevelTable";
        private const string TableNameItem = "ItemTable";
        private const string TableNameUpgradeCost = "UpgradeCostTable";
        private const string TableNameSkillLevel = "SkillLevelTable";

        private static CharacterProgressionManager instance;
        public static CharacterProgressionManager Instance => instance ??= new CharacterProgressionManager();

        // 九张养成表：EnsureTablesAsync 后可用（加载失败为 null，下次操作自动重试）
        // 角色表、武器表与物品表主键为字符串 id（CH0221 / 技能书 1011）；其余六表主键为 int
        private DataTable<CharacterRow, string> characterTable;
        private DataTable<LevelRow, int> levelTable;
        private DataTable<BreakthroughRow, int> breakthroughTable;
        private DataTable<UnlockNodeRow, int> unlockNodeTable;
        private DataTable<WeaponRow, string> weaponTable;
        private DataTable<WeaponLevelRow, int> weaponLevelTable;
        private DataTable<ItemRow, string> itemTable;
        private DataTable<UpgradeCostRow, int> upgradeCostTable;
        private DataTable<SkillLevelRow, int> skillLevelTable;

        private CharacterProgressionManager() { }

        #region 表接入

        /// <summary>
        /// 确保九张养成表已加载；LoadTableAsync 命中缓存时直接短路，重复调用无代价。
        /// 同步查询（GetLevelCap / GetUpgradeCost）或养成 UI 首次展示前 await 一次。
        /// </summary>
        /// <returns>九张表全部就绪返回 true；任一加载失败返回 false（失败详情由 DataTableManager 报错定位）。</returns>
        public async Task<bool> EnsureReadyAsync()
        {
            return await EnsureTablesAsync();
        }

        /// <summary>
        /// 指定突破阶段的等级上限：stage 0 = 初始上限常量，stage N = 突破表行。
        /// </summary>
        /// <param name="breakStage">突破阶段（0 = 未突破）。</param>
        /// <returns>等级上限；表未加载或行缺失时按初始上限兜底并报错。</returns>
        public int GetLevelCap(int breakStage)
        {
            if (breakStage <= 0) return InitialLevelCap;

            if (breakthroughTable == null)
            {
                Debug.LogError("突破表未加载，请先 await EnsureReadyAsync()，按初始上限兜底");
                return InitialLevelCap;
            }

            if (breakthroughTable.TryGet(breakStage, out BreakthroughRow row)) return row.LevelCap;

            Debug.LogError($"突破表无阶段 {breakStage} 的行，按初始上限兜底");
            return InitialLevelCap;
        }

        #endregion

        #region 升级结算

        /// <summary>
        /// 本级所需经验：从 level 升到 level+1 所需，= 80 × level^1.5 向上取整（2026-10-06 拍板）。
        /// 养成 UI 的经验进度条也用它取分母。
        /// </summary>
        /// <param name="level">当前等级（1 起）。</param>
        /// <returns>升到下一级所需经验；参数非法返回 0 并报错。</returns>
        public static int ExpToNext(int level)
        {
            if (level < 1)
            {
                Debug.LogError($"经验曲线参数无效：level={level}");
                return 0;
            }

            return (int)Math.Ceiling(BaseExpPerLevel * Math.Pow(level, ExpCurvePower));
        }

        /// <summary>
        /// 经验结算纯函数：从 (level, exp) 增加 gain 经验，逐级结算（本级所需 = <see cref="ExpToNext"/>，可连升），
        /// 到达 cap 后剩余经验丢弃（原神式溢出丢弃）。UI 的升级预览与 <see cref="AddExpAsync"/> 共用此口径，
        /// 保证"按钮上写的"与"结算落账的"一致。
        /// </summary>
        /// <param name="level">当前等级。</param>
        /// <param name="exp">本级已有经验。</param>
        /// <param name="gain">新增经验（long；技能书批量喂养总量可能超 int）。</param>
        /// <param name="cap">等级上限。</param>
        /// <param name="newLevel">结算后等级。</param>
        /// <param name="newExp">结算后本级经验（到顶时为 0）。</param>
        /// <param name="discarded">因到顶被丢弃的经验。</param>
        public static void SettleExp(int level, int exp, long gain, int cap, out int newLevel, out int newExp, out int discarded)
        {
            long total = (long)exp + gain;
            if (total > int.MaxValue)
            {
                Debug.LogError($"经验总量 {total} 超出 int 上限，按 {int.MaxValue} 钳制");
                total = int.MaxValue;
            }

            int lv = level;
            int remain = (int)total;
            while (lv < cap)
            {
                int required = ExpToNext(lv);
                if (remain < required) break;
                remain -= required;
                lv++;
            }

            // 达上限：剩余经验丢弃，突破后重新吃经验
            if (lv >= cap && remain > 0)
            {
                discarded = remain;
                remain = 0;
            }
            else
            {
                discarded = 0;
            }

            newLevel = lv;
            newExp = remain;
        }

        /// <summary>
        /// 增加经验并结算升级：本地循环结算（本级所需 = <see cref="ExpToNext"/>，可连升），
        /// 到达当前上限后剩余经验丢弃（2026-10-06 拍板，原神式），突破后继续吃；
        /// 结算结果一次落账（SetLevel + SetExp），仓库照常即存即广播 CharacterDataUpdated。
        /// </summary>
        /// <param name="unitId">单位 ID。</param>
        /// <param name="amount">增加的经验，必须为正数。</param>
        /// <returns>结算结果；参数非法、表未就绪或未拥有该角色时 Success 为 false。</returns>
        public async Task<LevelUpResult> AddExpAsync(string unitId, int amount)
        {
            if (amount <= 0)
            {
                Debug.LogError($"增加经验参数无效：unitId={unitId}, amount={amount}");
                return LevelUpResult.Failed;
            }

            if (!await EnsureTablesAsync()) return LevelUpResult.Failed;
            if (!CharacterWarehouseManager.Instance.TryGetEntry(unitId, out CharacterEntry entry))
            {
                Debug.LogError($"增加经验失败：角色未拥有 unitId={unitId}");
                return LevelUpResult.Failed;
            }

            // 结算在本地值上进行，算好后一次落账，避免逐级写仓库触发多次存档+广播
            int cap = GetLevelCap(entry.breakStage);
            SettleExp(entry.level, entry.exp, amount, cap, out int newLevel, out int newExp, out int discarded);

            // 未拥有已在上方拦截，两个写入口此处必成功
            if (newLevel != entry.level) CharacterWarehouseManager.Instance.SetLevel(unitId, newLevel);
            CharacterWarehouseManager.Instance.SetExp(unitId, newExp);

            return new LevelUpResult(true, entry.level, newLevel, discarded);
        }

        #endregion

        #region 突破

        /// <summary>
        /// 突破：校验等级达当前上限且材料足够 → 整体扣耗 → breakStage+1 → 免费解锁突破表附带节点。
        /// </summary>
        /// <param name="unitId">单位 ID。</param>
        /// <returns>成功返回 true；表未就绪、未拥有该角色、已达最高阶段、等级未达上限或材料不足返回 false 且不扣耗。</returns>
        public async Task<bool> BreakthroughAsync(string unitId)
        {
            if (!await EnsureTablesAsync()) return false;
            if (!CharacterWarehouseManager.Instance.TryGetEntry(unitId, out CharacterEntry entry))
            {
                Debug.LogError($"突破失败：角色未拥有 unitId={unitId}");
                return false;
            }

            int nextStage = entry.breakStage + 1;
            if (!breakthroughTable.TryGet(nextStage, out BreakthroughRow row))
            {
                NotifyPlayer("Already at max breakthrough stage");
                return false;
            }

            // 前置一：等级达当前上限
            int cap = GetLevelCap(entry.breakStage);
            if (entry.level < cap)
            {
                NotifyPlayer("Level has not reached the cap");
                return false;
            }

            // 前置二：材料足够（校验先行，全部足够才扣，不做部分扣除）
            ItemCost[] costs = row.CostList;
            foreach (ItemCost cost in costs)
            {
                if (!InventoryManager.Instance.HasEnough(cost.ItemId, cost.Count))
                {
                    NotifyPlayer("Not enough materials");
                    return false;
                }
            }

            return RunProgressionOp(() =>
            {
                foreach (ItemCost cost in costs)
                {
                    // 校验后扣除失败属于异常路径（单线程下不应发生），如实报错；事务整体回滚
                    if (!InventoryManager.Instance.TryRemove(cost.ItemId, cost.Count))
                    {
                        Debug.LogError($"突破扣耗异常：{ItemCost.Format(costs)}（unitId={unitId}）");
                        return false;
                    }
                }

                CharacterWarehouseManager.Instance.SetBreakStage(unitId, nextStage);

                // 突破附带节点免费解锁（UnlockNode 幂等，重复无副作用）
                foreach (int nodeId in row.UnlockNodeIdList)
                {
                    CharacterWarehouseManager.Instance.UnlockNode(unitId, nodeId);
                }

                return true;
            }, false);
        }

        #endregion

        #region 技能升级

        /// <summary>
        /// 下一级技能升级的消耗清单（展示用，只读不扣）：目标 = 当前等级 + 1，已满级返回空数组。
        /// 全部技能（普攻/天赋/E/Q）共用 SkillLevelTable（行 id = 目标等级）。
        /// </summary>
        /// <param name="unitId">单位 ID。</param>
        /// <param name="skillType">技能类型（SkillRow.SkillTypes 取值）。</param>
        /// <returns>下一级消耗；表未就绪、未拥有、无该技能行或已满级返回空数组（定位失败报错）。</returns>
        public async Task<ItemCost[]> GetNextSkillLevelCostAsync(string unitId, string skillType)
        {
            if (!await EnsureTablesAsync()) return Array.Empty<ItemCost>();
            await SkillDataLibrary.EnsureLoadedAsync();
            if (!CharacterWarehouseManager.Instance.TryGetEntry(unitId, out CharacterEntry entry))
            {
                Debug.LogError($"技能升级消耗查询失败：角色未拥有 unitId={unitId}");
                return Array.Empty<ItemCost>();
            }
            if (!SkillDataLibrary.TryGetSkillRow(unitId, skillType, out SkillRow row))
            {
                Debug.LogError($"技能升级消耗查询失败：SkillTable 无该类型技能 unitId={unitId}, skillType={skillType}");
                return Array.Empty<ItemCost>();
            }

            if (EntrySkillLevel(entry, row.Type) >= row.MaxLevel) return Array.Empty<ItemCost>();
            if (!skillLevelTable.TryGet(EntrySkillLevel(entry, row.Type) + 1, out SkillLevelRow costRow))
            {
                return Array.Empty<ItemCost>();
            }
            return costRow.CostList;
        }

        /// <summary>
        /// 技能升级（交互与突破同款：材料足够即整体扣耗，玩家无需手动填充）：校验（满级/材料）→
        /// 事务内整体扣耗 + 等级落账。失败不扣耗弹 Notice；成功经仓库广播回流刷新。
        /// </summary>
        /// <param name="unitId">单位 ID。</param>
        /// <param name="skillType">技能类型（SkillRow.SkillTypes 取值）。</param>
        /// <returns>成功返回 true；表未就绪、未拥有、无技能行、已满级、表缺行或材料不足返回 false 且不扣耗。</returns>
        public async Task<bool> SkillLevelUpAsync(string unitId, string skillType)
        {
            if (!await EnsureTablesAsync()) return false;
            await SkillDataLibrary.EnsureLoadedAsync();
            if (!CharacterWarehouseManager.Instance.TryGetEntry(unitId, out CharacterEntry entry))
            {
                Debug.LogError($"技能升级失败：角色未拥有 unitId={unitId}");
                return false;
            }
            if (!SkillDataLibrary.TryGetSkillRow(unitId, skillType, out SkillRow row))
            {
                Debug.LogError($"技能升级失败：SkillTable 无该类型技能 unitId={unitId}, skillType={skillType}");
                return false;
            }

            int level = EntrySkillLevel(entry, row.Type);
            if (level >= row.MaxLevel)
            {
                NotifyPlayer("Skill has reached the max level");
                return false;
            }

            // SkillLevelTable 行 = 目标等级（2..10 连号），缺行属于配表错误，报错拒绝
            if (!skillLevelTable.TryGet(level + 1, out SkillLevelRow costRow))
            {
                Debug.LogError($"技能升级失败：SkillLevelTable 缺目标等级 {level + 1} 的行（unitId={unitId}, skillType={skillType}）");
                return false;
            }

            // 材料校验先行（全部足够才扣，不做部分扣除）
            ItemCost[] costs = costRow.CostList;
            foreach (ItemCost cost in costs)
            {
                if (!InventoryManager.Instance.HasEnough(cost.ItemId, cost.Count))
                {
                    NotifyPlayer("Not enough materials");
                    return false;
                }
            }

            return RunProgressionOp(() =>
            {
                foreach (ItemCost cost in costs)
                {
                    // 校验后扣除失败属于异常路径（单线程下不应发生），如实报错；事务整体回滚
                    if (!InventoryManager.Instance.TryRemove(cost.ItemId, cost.Count))
                    {
                        Debug.LogError($"技能升级扣耗异常：{ItemCost.Format(costs)}（unitId={unitId}）");
                        return false;
                    }
                }

                SetSkillLevel(unitId, level + 1, skillType);
                return true;
            }, false);
        }

        /// <summary>条目上的技能等级（普攻/天赋/E/Q → 对应字段；未知类型按普攻）。</summary>
        private static int EntrySkillLevel(CharacterEntry entry, string skillType)
        {
            switch (skillType)
            {
                case SkillRow.SkillTypes.E: return entry.skillLevelE;
                case SkillRow.SkillTypes.Burst: return entry.skillLevelQ;
                case SkillRow.SkillTypes.Talent: return entry.skillLevelTalent;
                default: return entry.skillLevelNormal;
            }
        }

        /// <summary>落账技能等级（事务变更段调用；仓库广播在批量域内只标脏，EndBatch 统一提交）。</summary>
        private static void SetSkillLevel(string unitId, int level, string skillType)
        {
            switch (skillType)
            {
                case SkillRow.SkillTypes.E:
                    CharacterWarehouseManager.Instance.SetSkillLevelE(unitId, level);
                    break;
                case SkillRow.SkillTypes.Burst:
                    CharacterWarehouseManager.Instance.SetSkillLevelQ(unitId, level);
                    break;
                case SkillRow.SkillTypes.Talent:
                    CharacterWarehouseManager.Instance.SetSkillLevelTalent(unitId, level);
                    break;
                default:
                    CharacterWarehouseManager.Instance.SetSkillLevelNormal(unitId, level);
                    break;
            }
        }

        #endregion

        #region 技能书喂养升级（暂存 + 确认结算）

        /// <summary>
        /// 使用技能书升级角色（暂存喂养的结算入口）：整体校验（到顶拒绝/经验书有效性/库存对账）→
        /// 整批扣耗（技能书 + 跨级信用点）→ 经验一次结算落账。失败不扣耗；
        /// 成功经仓库广播 CharacterDataUpdated 回流刷新。
        /// 信用点消耗 = 实际跨过的级数逐行聚合（行 L = L → L+1，见 <see cref="GetUpgradeCost"/>），
        /// 经验只累积不跨级时不收。
        /// </summary>
        /// <param name="unitId">单位 ID。</param>
        /// <param name="books">待消耗的技能书（同 id 可多条，数量必须为正，结算前按 id 合并）。</param>
        /// <returns>结算结果；表未就绪、未拥有、已达等级上限、非经验书或材料不足时 Success 为 false 且不扣耗。</returns>
        public Task<LevelUpResult> UseExpItemsAsync(string unitId, List<ItemStack> books)
        {
            return UseBooksCoreAsync(unitId, books, false);
        }

        /// <summary>
        /// 使用技能书升级武器：与角色共用经验曲线与上限口径（武器等级上限随角色突破走），
        /// 落账 SetWeaponLevel + SetWeaponExp。
        /// </summary>
        /// <param name="unitId">单位 ID（武器挂在该角色身上）。</param>
        /// <param name="books">待消耗的武器技能书（同 id 可多条，数量必须为正）。</param>
        /// <returns>结算结果；拒绝口径同 <see cref="UseExpItemsAsync"/>。</returns>
        public Task<LevelUpResult> UseWeaponExpItemsAsync(string unitId, List<ItemStack> books)
        {
            return UseBooksCoreAsync(unitId, books, true);
        }

        /// <summary>技能书喂养结算核心：先验后扣（全部足够才扣，不做部分扣除），经验结算复用 <see cref="SettleExp"/>。</summary>
        private async Task<LevelUpResult> UseBooksCoreAsync(string unitId, List<ItemStack> books, bool isWeapon)
        {
            if (books == null || books.Count == 0)
            {
                Debug.LogError($"技能书喂养参数无效：books 为空（unitId={unitId}）");
                return LevelUpResult.Failed;
            }

            if (!await EnsureTablesAsync()) return LevelUpResult.Failed;
            if (!CharacterWarehouseManager.Instance.TryGetEntry(unitId, out CharacterEntry entry))
            {
                Debug.LogError($"技能书喂养失败：角色未拥有 unitId={unitId}");
                return LevelUpResult.Failed;
            }

            // 同 id 合并 + 校验（数量为正、表有行、是经验书）；任一违约都属调用方错误，整体拒绝
            Dictionary<string, long> merged = new Dictionary<string, long>();
            foreach (ItemStack stack in books)
            {
                if (stack.count <= 0)
                {
                    Debug.LogError($"技能书喂养参数无效：{stack.itemId} 数量 {stack.count} 非正（unitId={unitId}）");
                    return LevelUpResult.Failed;
                }
                merged[stack.itemId] = (merged.TryGetValue(stack.itemId, out long sum) ? sum : 0L) + stack.count;
            }

            long totalExp = 0;
            foreach (KeyValuePair<string, long> pair in merged)
            {
                if (!itemTable.TryGet(pair.Key, out ItemRow row))
                {
                    Debug.LogError($"技能书喂养失败：ItemTable 无物品 {pair.Key}（unitId={unitId}）");
                    return LevelUpResult.Failed;
                }
                if (row.Exp <= 0)
                {
                    Debug.LogError($"技能书喂养失败：物品 {pair.Key} 不是经验书（exp={row.Exp}，unitId={unitId}）");
                    return LevelUpResult.Failed;
                }
                totalExp += (long)row.Exp * pair.Value;
            }

            // 到顶提前拒绝：升级无意义，不扣耗（玩家可触发，走弹窗）
            int cap = GetLevelCap(entry.breakStage);
            int oldLevel = isWeapon ? Math.Max(1, entry.weaponLevel) : entry.level;
            int oldExp = isWeapon ? entry.weaponExp : entry.exp;
            if (oldLevel >= cap)
            {
                NotifyPlayer("Level has reached the max level");
                return LevelUpResult.Failed;
            }

            // 预结算定目标级（纯函数无副作用）：信用点只按实际跨过的级数收，跨级前不扣
            SettleExp(oldLevel, oldExp, totalExp, cap, out int newLevel, out int newExp, out int discarded);

            // 库存对账：技能书 + 跨级信用点，先验后扣任一不足整体拒绝（玩家可触发，走弹窗）
            foreach (KeyValuePair<string, long> pair in merged)
            {
                if (InventoryManager.Instance.GetCount(pair.Key) < pair.Value)
                {
                    NotifyPlayer("Not enough materials");
                    return LevelUpResult.Failed;
                }
            }
            ItemCost[] creditCosts = newLevel > oldLevel ? GetUpgradeCost(oldLevel, newLevel) : Array.Empty<ItemCost>();
            foreach (ItemCost cost in creditCosts)
            {
                if (InventoryManager.Instance.GetCount(cost.ItemId) < cost.Count)
                {
                    NotifyPlayer("Not enough materials");
                    return LevelUpResult.Failed;
                }
            }

            return RunProgressionOp(() =>
            {
                foreach (KeyValuePair<string, long> pair in merged)
                {
                    // 校验后扣除失败属于异常路径（单线程下不应发生），如实报错；事务整体回滚
                    if (!InventoryManager.Instance.TryRemove(pair.Key, pair.Value))
                    {
                        Debug.LogError($"技能书喂养扣耗异常：{pair.Key} x{pair.Value}（unitId={unitId}）");
                        return LevelUpResult.Failed;
                    }
                }
                foreach (ItemCost cost in creditCosts)
                {
                    if (!InventoryManager.Instance.TryRemove(cost.ItemId, cost.Count))
                    {
                        Debug.LogError($"技能书喂养信用点扣耗异常：{cost.ItemId} x{cost.Count}（unitId={unitId}）");
                        return LevelUpResult.Failed;
                    }
                }

                // 未拥有已在上方拦截，写入口必成功；武器侧落账武器字段
                if (isWeapon)
                {
                    if (newLevel != oldLevel) CharacterWarehouseManager.Instance.SetWeaponLevel(unitId, newLevel);
                    CharacterWarehouseManager.Instance.SetWeaponExp(unitId, newExp);
                }
                else
                {
                    if (newLevel != oldLevel) CharacterWarehouseManager.Instance.SetLevel(unitId, newLevel);
                    CharacterWarehouseManager.Instance.SetExp(unitId, newExp);
                }

                return new LevelUpResult(true, oldLevel, newLevel, discarded);
            }, LevelUpResult.Failed);
        }

        /// <summary>
        /// 自动填充计划（角色）：按背包持有量算最多能升到哪一级（≤ 当前上限，书本经验与
        /// 逐级消耗任一不足即止），并给出刚好够到该级的各技能书暂存数量（大书整填、小书补零头，
        /// 溢出不超过最小单本经验）。
        /// </summary>
        /// <param name="unitId">单位 ID。</param>
        /// <param name="bookItemIds">参与填充的技能书 id（按品质升序排列，倒序遍历即大书优先）。</param>
        /// <returns>填充计划；已达上限或一本都升不动时 HasPlan 为 false。</returns>
        public Task<FillPlan> GetFillPlanAsync(string unitId, string[] bookItemIds)
        {
            return GetFillPlanCoreAsync(unitId, bookItemIds, false);
        }

        /// <summary>自动填充计划（武器）：口径同 <see cref="GetFillPlanAsync"/>，按武器等级与武器经验计算。</summary>
        public Task<FillPlan> GetWeaponFillPlanAsync(string unitId, string[] bookItemIds)
        {
            return GetFillPlanCoreAsync(unitId, bookItemIds, true);
        }

        /// <summary>填充计划核心：预预算算可达目标级，再按"大书整填、小书补零头"贪心暂存。</summary>
        private async Task<FillPlan> GetFillPlanCoreAsync(string unitId, string[] bookItemIds, bool isWeapon)
        {
            if (bookItemIds == null || bookItemIds.Length == 0)
            {
                Debug.LogError($"填充计划参数无效：bookItemIds 为空（unitId={unitId}）");
                return FillPlan.Empty;
            }

            if (!await EnsureTablesAsync()) return FillPlan.Empty;
            if (!CharacterWarehouseManager.Instance.TryGetEntry(unitId, out CharacterEntry entry))
            {
                Debug.LogError($"填充计划失败：角色未拥有 unitId={unitId}");
                return FillPlan.Empty;
            }

            int n = bookItemIds.Length;
            long[] counts = new long[n];
            long[] held = new long[n];
            int[] exp = new int[n];
            long totalAvail = 0;
            for (int i = 0; i < n; i++)
            {
                held[i] = InventoryManager.Instance.GetCount(bookItemIds[i]);
                if (held[i] <= 0) continue;
                if (!itemTable.TryGet(bookItemIds[i], out ItemRow row) || row.Exp <= 0)
                {
                    Debug.LogError($"填充计划：物品 {bookItemIds[i]} 不是有效经验书，该格按 0 处理（unitId={unitId}）");
                    held[i] = 0;
                    continue;
                }
                exp[i] = row.Exp;
                totalAvail += (long)row.Exp * held[i];
            }

            int curLevel = isWeapon ? Math.Max(1, entry.weaponLevel) : entry.level;
            long curExp = isWeapon ? entry.weaponExp : entry.exp;
            int cap = GetLevelCap(entry.breakStage);

            // 预预算：从当前级逐级试买，书本经验预算或逐级消耗（信用点）任一不足即为目标级
            long budget = totalAvail;
            int target = curLevel;
            Dictionary<string, long> creditsSpent = new Dictionary<string, long>();
            while (target < cap)
            {
                long required = ExpToNext(target) - curExp;
                if (budget < required) break;

                // 本级消耗对账：任一物品的累计需求超过持有即停（升级消耗行 L = L → L+1）
                if (upgradeCostTable != null)
                {
                    if (!upgradeCostTable.TryGet(target, out UpgradeCostRow costRow))
                    {
                        Debug.LogError($"填充计划：升级消耗表无等级 {target} 的行，该级消耗按 0 处理（unitId={unitId}）");
                    }
                    else
                    {
                        bool creditShort = false;
                        foreach (ItemCost cost in costRow.CostList)
                        {
                            long cum = (creditsSpent.TryGetValue(cost.ItemId, out long sum) ? sum : 0L) + cost.Count;
                            if (InventoryManager.Instance.GetCount(cost.ItemId) < cum)
                            {
                                creditShort = true;
                                break;
                            }
                            creditsSpent[cost.ItemId] = cum;
                        }
                        if (creditShort) break;
                    }
                }

                budget -= required;
                curExp = 0;
                target++;
            }

            // 一级都升不动：无可行计划
            if (target <= curLevel) return new FillPlan(false, curLevel, counts);

            // 目标级总需求：从当前级按精确口径重算一次
            long remaining = 0;
            long inLevel = isWeapon ? entry.weaponExp : entry.exp;
            for (int lvl = curLevel; lvl < target; lvl++)
            {
                remaining += ExpToNext(lvl) - inLevel;
                inLevel = 0;
            }

            // 贪心暂存：大书整填（高到低），小书补零头（低到高）
            long left = remaining;
            for (int i = n - 1; i >= 0; i--)
            {
                if (exp[i] <= 0) continue;
                long take = Math.Min(held[i], left / exp[i]);
                counts[i] = take;
                left -= take * exp[i];
            }
            for (int i = 0; i < n; i++)
            {
                if (left <= 0) break;
                if (exp[i] <= 0) continue;
                long need = (left + exp[i] - 1) / exp[i];
                long take = Math.Min(held[i] - counts[i], need);
                if (take <= 0) continue;
                counts[i] += take;
                left -= take * exp[i];
            }

            return new FillPlan(true, target, counts);
        }

        /// <summary>
        /// 升级消耗聚合（展示与结算同口径）：行 L = L → L+1 的消耗（当前为信用点），聚合
        /// [fromLevel, toLevel) 逐行消耗并按物品 id 合并。fromLevel ≥ toLevel（未跨级）返回空数组
        /// ——经验只累积不跨级时不收消耗。同步查询，需先 await EnsureReadyAsync（养成面板初始化已保证）。
        /// </summary>
        /// <param name="fromLevel">起始等级（含）。</param>
        /// <param name="toLevel">目标等级（不含）。</param>
        /// <returns>聚合后的消耗清单；表未就绪返回空数组并报错。</returns>
        public ItemCost[] GetUpgradeCost(int fromLevel, int toLevel)
        {
            if (fromLevel >= toLevel) return Array.Empty<ItemCost>();
            if (upgradeCostTable == null)
            {
                Debug.LogError("升级消耗表未加载，请先 await EnsureReadyAsync()");
                return Array.Empty<ItemCost>();
            }

            Dictionary<string, long> merged = new Dictionary<string, long>();
            for (int lvl = fromLevel; lvl < toLevel; lvl++)
            {
                if (!upgradeCostTable.TryGet(lvl, out UpgradeCostRow row))
                {
                    Debug.LogError($"升级消耗表无等级 {lvl} 的行（聚合 {fromLevel}→{toLevel}），该级消耗按 0 处理");
                    continue;
                }
                foreach (ItemCost cost in row.CostList)
                {
                    merged[cost.ItemId] = (merged.TryGetValue(cost.ItemId, out long sum) ? sum : 0L) + cost.Count;
                }
            }

            ItemCost[] result = new ItemCost[merged.Count];
            int index = 0;
            foreach (KeyValuePair<string, long> pair in merged)
            {
                result[index++] = new ItemCost(pair.Key, (int)Math.Min(pair.Value, int.MaxValue));
            }
            return result;
        }

        /// <summary>
        /// 下一阶段突破的消耗清单（展示用，只读不扣）：UI 突破列表按它渲染 need；
        /// 已达最高突破阶段返回空数组（UI 显示为已满破）。
        /// </summary>
        /// <param name="unitId">单位 ID。</param>
        /// <returns>下一阶段消耗；表未就绪或未拥有返回空数组并报错。</returns>
        public async Task<ItemCost[]> GetNextBreakthroughCostsAsync(string unitId)
        {
            if (!await EnsureTablesAsync()) return Array.Empty<ItemCost>();
            if (!CharacterWarehouseManager.Instance.TryGetEntry(unitId, out CharacterEntry entry))
            {
                Debug.LogError($"突破消耗查询失败：角色未拥有 unitId={unitId}");
                return Array.Empty<ItemCost>();
            }

            if (!breakthroughTable.TryGet(entry.breakStage + 1, out BreakthroughRow row)) return Array.Empty<ItemCost>();
            return row.CostList;
        }

        #endregion

        #region 武器贡献

        /// <summary>
        /// 解析绑定武器的属性贡献（白值 + 主属性，均按武器等级结算取整）。
        /// 属性结算与武器页 UI 共用此入口，保证展示与结算同源。
        /// </summary>
        /// <param name="unitId">单位 ID。</param>
        /// <returns>贡献载荷；未拥有角色/无武器表行/表未就绪时返回 default（无武器）。</returns>
        public async Task<WeaponStatContribution> GetWeaponContributionAsync(string unitId)
        {
            if (!await EnsureTablesAsync()) return default;
            if (!CharacterWarehouseManager.Instance.TryGetEntry(unitId, out CharacterEntry entry)) return default;
            if (!weaponTable.TryGet(unitId, out WeaponRow weaponRow)) return default;

            int weaponLevel = Math.Max(1, entry.weaponLevel);
            float mult = 1f;
            if (weaponLevelTable.TryGet(weaponLevel, out WeaponLevelRow levelRow)) mult = levelRow.Multiplier;
            else Debug.LogError($"武器属性结算：WeaponLevelTable 无等级 {weaponLevel} 的行，系数按 1.0 兜底（unitId={unitId}）");

            // 白值向下取整并入基数；主属性按同曲线成长（比率值不取整）
            int whiteValue = (int)Math.Floor(weaponRow.BaseStatValue * mult + 1e-4);
            string[] bonusKeys = { weaponRow.MainStatType };
            float[] bonusValues = { weaponRow.MainStatValue * mult };

            return new WeaponStatContribution(weaponRow.BaseStatType, whiteValue, bonusKeys, bonusValues);
        }

        #endregion

        #region 养成事务（整体提交）

        //事务日志子目录与文件名（相对于 Data/）
        private const string JournalFolder = "Journal";
        private const string JournalFileName = "ProgressionOp";

        /// <summary>
        /// 以整体事务执行一次养成操作的变更段：进入前把背包+仓库的前状态写快照日志 →
        /// 批量域内变更（不落盘不出广播）→ 全部成功后统一落盘一次并删日志；
        /// body 提前结束（任何拒绝/异常）则从快照整体回滚再提交——材料不丢、养成不生效。
        /// body 内变更段必须全同步（其间不得 await）；拒绝路径直接 return 即可，无需手工回滚。
        /// </summary>
        /// <typeparam name="T">操作结果类型。</typeparam>
        /// <param name="body">变更段：扣耗与落账。</param>
        /// <param name="failedResult">拒绝时 body 返回的失败值（false / LevelUpResult.Failed 等）。</param>
        /// <returns>body 的返回值。</returns>
        private static T RunProgressionOp<T>(Func<T> body, T failedResult)
        {
            //先写快照日志（写失败异常上抛，此时尚未动任何内存状态）
            BinaryDataManager.Instance.Save(JournalFolder, JournalFileName, new ProgressionOpJournal
            {
                bag = InventoryManager.Instance.SnapshotData(),
                warehouse = CharacterWarehouseManager.Instance.SnapshotData(),
            });

            InventoryManager.Instance.BeginBatch();
            CharacterWarehouseManager.Instance.BeginBatch();
            bool open = true;
            try
            {
                T result = body();
                open = false;
                EndOpBatches();
                BinaryDataManager.Instance.Delete(JournalFolder, JournalFileName);
                return result;
            }
            finally
            {
                if (open)
                {
                    //body 提前结束：内存状态可能已部分变更 → 从快照整体回滚，批量域闭合时一次落盘
                    ProgressionOpJournal journal = ReadJournalSnapshot();
                    if (journal != null)
                    {
                        InventoryManager.Instance.RestoreData(journal.bag);
                        CharacterWarehouseManager.Instance.RestoreData(journal.warehouse);
                    }
                    else
                    {
                        Debug.LogError("[CharacterProgressionManager] 事务日志无法读取，回滚失败，保留当前状态（可能部分变更）");
                    }
                    EndOpBatches();
                    BinaryDataManager.Instance.Delete(JournalFolder, JournalFileName);
                }
            }
        }

        /// <summary>
        /// 养成事务中断恢复（启动时调用一次，须在任何读档 UI 与默认发放之前）：
        /// 存在未提交的事务日志 = 上次操作被打断，按快照整体回滚并清日志；无日志时零开销返回。
        /// </summary>
        public static void RecoverInterruptedOp()
        {
            ProgressionOpJournal journal = ReadJournalSnapshot();
            BinaryDataManager.Instance.Delete(JournalFolder, JournalFileName);
            if (journal == null) return;

            InventoryManager.Instance.RestoreData(journal.bag);
            CharacterWarehouseManager.Instance.RestoreData(journal.warehouse);
            Debug.LogWarning("[CharacterProgressionManager] 检测到上次养成操作被中断，已回滚到操作前状态");
        }

        /// <summary>读取事务日志快照；文件缺失返回 null，损坏/版本不符/字段缺失报错并返回 null（调用方不得据此回滚）。</summary>
        private static ProgressionOpJournal ReadJournalSnapshot()
        {
            if (!BinaryDataManager.Instance.FileExists(JournalFolder, JournalFileName)) return null;

            ProgressionOpJournal journal = BinaryDataManager.Instance.Load<ProgressionOpJournal>(JournalFolder, JournalFileName);
            if (journal == null || journal.version != 1 || journal.bag == null || journal.warehouse == null)
            {
                Debug.LogError($"[CharacterProgressionManager] 事务日志损坏或版本不符（{JournalFileName}），无法据其回滚");
                return null;
            }
            return journal;
        }

        /// <summary>闭合背包与仓库两个批量域（有脏数据时各自落盘+广播一次）。</summary>
        private static void EndOpBatches()
        {
            InventoryManager.Instance.EndBatch();
            CharacterWarehouseManager.Instance.EndBatch();
        }

        #endregion

        #region 节点解锁

        /// <summary>
        /// 独立解锁节点：校验未解锁、非突破附带节点且材料足够 → 扣耗 → 仓库登记。
        /// 突破附带的节点（cost 为空）只能随突破解锁；重复解锁幂等拒绝。
        /// </summary>
        /// <param name="unitId">单位 ID。</param>
        /// <param name="nodeId">节点 ID（UnlockNodeTable 主键）。</param>
        /// <returns>成功返回 true；表未就绪、未拥有该角色、已解锁、节点不存在、随突破解锁或材料不足返回 false 且不扣耗。</returns>
        public async Task<bool> UnlockNodeAsync(string unitId, int nodeId)
        {
            if (!await EnsureTablesAsync()) return false;
            if (!CharacterWarehouseManager.Instance.TryGetEntry(unitId, out CharacterEntry entry))
            {
                Debug.LogError($"解锁节点失败：角色未拥有 unitId={unitId}");
                return false;
            }

            // 已解锁幂等拒绝（在扣耗之前拦截）
            if (entry.unlockedNodes.Contains(nodeId)) return false;

            if (!unlockNodeTable.TryGet(nodeId, out UnlockNodeRow node))
            {
                Debug.LogError($"解锁节点失败：UnlockNodeTable 无节点 {nodeId}");
                return false;
            }

            if (node.UnlockWithBreakthrough)
            {
                NotifyPlayer("This node unlocks with breakthrough");
                return false;
            }

            ItemCost[] costs = node.CostList;
            foreach (ItemCost cost in costs)
            {
                if (!InventoryManager.Instance.HasEnough(cost.ItemId, cost.Count))
                {
                    NotifyPlayer("Not enough materials");
                    return false;
                }
            }

            return RunProgressionOp(() =>
            {
                foreach (ItemCost cost in costs)
                {
                    // 校验后扣除失败属于异常路径（单线程下不应发生），如实报错；事务整体回滚
                    if (!InventoryManager.Instance.TryRemove(cost.ItemId, cost.Count))
                    {
                        Debug.LogError($"解锁节点扣耗异常：{ItemCost.Format(costs)}（节点 {nodeId}）");
                        return false;
                    }
                }

                // 登记失败（参数/重复等异常路径）同样触发事务整体回滚，材料不丢
                if (!CharacterWarehouseManager.Instance.UnlockNode(unitId, nodeId))
                {
                    Debug.LogError($"解锁节点登记失败：unitId={unitId}, nodeId={nodeId}");
                    return false;
                }
                return true;
            }, false);
        }

        #endregion

        #region 属性结算

        /// <summary>
        /// 结算角色当前属性（单一入口，规则见 <see cref="CharacterStatsCalculator.Calculate"/>）。
        /// </summary>
        /// <param name="unitId">单位 ID。</param>
        /// <param name="star">星级；v1 恒传 1（星级乘区未启用）。</param>
        /// <returns>属性结果；表未就绪、未拥有该角色或角色不在 CharacterTable 时返回 default（全零）。</returns>
        public async Task<CharacterStats> CalculateStatsAsync(string unitId, int star = 1)
        {
            if (!await EnsureTablesAsync()) return default;
            if (!CharacterWarehouseManager.Instance.TryGetEntry(unitId, out CharacterEntry entry))
            {
                Debug.LogError($"属性结算失败：角色未拥有 unitId={unitId}");
                return default;
            }

            if (!characterTable.TryGet(unitId, out CharacterRow row))
            {
                Debug.LogError($"属性结算失败：CharacterTable 无角色 {unitId} 的行");
                return default;
            }

            // 等级系数：脏档越界时按 1.0 兜底并报错（正常结算下等级恒在表范围内）
            float mult = 1f;
            if (levelTable.TryGet(entry.level, out LevelRow levelRow)) mult = levelRow.Multiplier;
            else Debug.LogError($"属性结算：LevelTable 无等级 {entry.level} 的行，系数按 1.0 兜底（unitId={unitId}）");

            // 武器贡献解析失败时返回无武器载荷，角色侧属性照常结算
            WeaponStatContribution weapon = await GetWeaponContributionAsync(unitId);

            // 装备贡献解析失败时返回无装备载荷，角色侧属性照常结算
            TrinketStatContribution trinkets = await TrinketManager.Instance.GetTrinketContributionAsync(unitId);

            return CharacterStatsCalculator.Calculate(row, mult, entry.unlockedNodes, LookupNode, weapon, trinkets, star);
        }

        #endregion

        #region Private

        /// <summary>确保九张养成表已加载并缓存到字段；某张失败为 null 时下次调用自动重试。</summary>
        private async Task<bool> EnsureTablesAsync()
        {
            characterTable = await DataTableManager.Instance.LoadTableAsync<CharacterRow, string>(TableNameCharacter);
            levelTable = await DataTableManager.Instance.LoadTableAsync<LevelRow, int>(TableNameLevel);
            breakthroughTable = await DataTableManager.Instance.LoadTableAsync<BreakthroughRow, int>(TableNameBreakthrough);
            unlockNodeTable = await DataTableManager.Instance.LoadTableAsync<UnlockNodeRow, int>(TableNameUnlockNode);
            weaponTable = await DataTableManager.Instance.LoadTableAsync<WeaponRow, string>(TableNameWeapon);
            weaponLevelTable = await DataTableManager.Instance.LoadTableAsync<WeaponLevelRow, int>(TableNameWeaponLevel);
            itemTable = await DataTableManager.Instance.LoadTableAsync<ItemRow, string>(TableNameItem);
            upgradeCostTable = await DataTableManager.Instance.LoadTableAsync<UpgradeCostRow, int>(TableNameUpgradeCost);
            skillLevelTable = await DataTableManager.Instance.LoadTableAsync<SkillLevelRow, int>(TableNameSkillLevel);

            return characterTable != null && levelTable != null
                && breakthroughTable != null && unlockNodeTable != null
                && weaponTable != null && weaponLevelTable != null
                && itemTable != null && upgradeCostTable != null
                && skillLevelTable != null;
        }

        /// <summary>节点查询委托：表未命中返回 null，由计算器跳过该节点。</summary>
        private UnlockNodeRow LookupNode(int nodeId)
        {
            return unlockNodeTable.TryGet(nodeId, out UnlockNodeRow node) ? node : null;
        }

        #endregion
    }
}
