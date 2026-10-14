using System.Collections.Generic;
using Core.Gear;
using UnityEngine;

namespace Framework.Gameplay.Player
{
    /// <summary>
    /// 全局角色仓库管理器：角色拥有与养成数据的唯一入口与唯一存档者，变更即保存并广播事件。
    /// 养成业务规则（升级结算、突破校验、节点消耗扣除）在上层养成系统；本类只做数据读写与防御。
    /// </summary>
    public class CharacterWarehouseManager
    {
        // 存档相对 Data/ 的子目录
        private const string SaveFolder = "PlayerData/Character/";

        // 存档文件名（不含扩展名）
        private const string SaveFileName = "Warehouse";

        private static CharacterWarehouseManager instance;
        public static CharacterWarehouseManager Instance => instance ??= new CharacterWarehouseManager();

        // 角色仓库存档数据
        private CharacterWarehouseData data;

        private CharacterWarehouseManager()
        {
            LoadData();
        }

        #region 查询

        /// <summary>
        /// 判断是否拥有指定角色。
        /// </summary>
        /// <param name="unitId">单位 ID，与 CharacterTable 主键对齐（如 CH0221）。</param>
        /// <returns>已拥有返回 true。</returns>
        public bool HasCharacter(string unitId)
        {
            return FindEntry(unitId) != null;
        }

        /// <summary>
        /// 获取指定角色的养成条目（只读用途；写操作一律走本管理器，直改不落盘）。
        /// </summary>
        /// <param name="unitId">单位 ID。</param>
        /// <param name="entry">对应的养成条目；未拥有时为 null。</param>
        /// <returns>已拥有返回 true。</returns>
        public bool TryGetEntry(string unitId, out CharacterEntry entry)
        {
            entry = FindEntry(unitId);
            return entry != null;
        }

        /// <summary>
        /// 获取全部角色条目的只读快照。
        /// </summary>
        /// <returns>只读视图；外部不得修改，写操作一律走本管理器。</returns>
        public IReadOnlyList<CharacterEntry> Snapshot()
        {
            return data.characters.AsReadOnly();
        }

        #endregion

        #region 变更

        /// <summary>
        /// 登记获得新角色（初始 1 级、未突破）；已拥有时幂等拒绝。
        /// </summary>
        /// <param name="unitId">单位 ID，必须为非空白字符串。</param>
        /// <returns>新获得返回 true；参数非法或已拥有返回 false 且无副作用。</returns>
        public bool AddCharacter(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                Debug.LogError($"登记角色参数无效：unitId={unitId}");
                return false;
            }

            if (HasCharacter(unitId)) return false;

            data.characters.Add(new CharacterEntry { unitId = unitId });
            NotifyChanged(EventNames.CharacterAcquired);
            return true;
        }

        /// <summary>
        /// 设置角色等级；上限由养成层按 LevelTable 控制，此处只做下限防御。
        /// </summary>
        /// <param name="unitId">单位 ID。</param>
        /// <param name="level">目标等级，必须不小于 1。</param>
        /// <returns>成功返回 true；未拥有该角色或参数非法返回 false。</returns>
        public bool SetLevel(string unitId, int level)
        {
            if (!TryGetMutableEntry(unitId, out var entry) || level < 1)
            {
                Debug.LogError($"设置等级无效：unitId={unitId}, level={level}");
                return false;
            }

            entry.level = level;
            NotifyChanged(EventNames.CharacterDataUpdated);
            return true;
        }

        /// <summary>
        /// 增加当前等级内经验；升级结算（经验公式 + 等级上限校验）在养成业务逻辑中，此处只累加存值。
        /// </summary>
        /// <param name="unitId">单位 ID。</param>
        /// <param name="amount">增加的经验，必须为正数。</param>
        /// <returns>成功返回 true；未拥有该角色或参数非法返回 false。</returns>
        public bool AddExp(string unitId, int amount)
        {
            if (!TryGetMutableEntry(unitId, out var entry) || amount <= 0)
            {
                Debug.LogError($"增加角色经验参数无效：unitId={unitId}, amount={amount}");
                return false;
            }

            entry.exp += amount;
            NotifyChanged(EventNames.CharacterDataUpdated);
            return true;
        }

        /// <summary>
        /// 设置当前等级内经验（升级结算后的落点）；结算公式与上限校验在养成业务逻辑中，此处只做下限防御。
        /// </summary>
        /// <param name="unitId">单位 ID。</param>
        /// <param name="exp">目标经验，必须不小于 0。</param>
        /// <returns>成功返回 true；未拥有该角色或参数非法返回 false。</returns>
        public bool SetExp(string unitId, int exp)
        {
            if (!TryGetMutableEntry(unitId, out var entry) || exp < 0)
            {
                Debug.LogError($"设置经验无效：unitId={unitId}, exp={exp}");
                return false;
            }

            entry.exp = exp;
            NotifyChanged(EventNames.CharacterDataUpdated);
            return true;
        }

        /// <summary>
        /// 设置绑定武器等级（升级结算后的落点）；结算公式与上限校验在养成业务逻辑中，此处只做下限防御。
        /// </summary>
        /// <param name="unitId">单位 ID。</param>
        /// <param name="level">目标武器等级，必须不小于 1。</param>
        /// <returns>成功返回 true；未拥有该角色或参数非法返回 false。</returns>
        public bool SetWeaponLevel(string unitId, int level)
        {
            if (!TryGetMutableEntry(unitId, out var entry) || level < 1)
            {
                Debug.LogError($"设置武器等级无效：unitId={unitId}, level={level}");
                return false;
            }

            entry.weaponLevel = level;
            NotifyChanged(EventNames.CharacterDataUpdated);
            return true;
        }

        /// <summary>
        /// 设置绑定武器当前等级内经验（升级结算后的落点）；结算公式与上限校验在养成业务逻辑中，此处只做下限防御。
        /// </summary>
        /// <param name="unitId">单位 ID。</param>
        /// <param name="exp">目标经验，必须不小于 0。</param>
        /// <returns>成功返回 true；未拥有该角色或参数非法返回 false。</returns>
        public bool SetWeaponExp(string unitId, int exp)
        {
            if (!TryGetMutableEntry(unitId, out var entry) || exp < 0)
            {
                Debug.LogError($"设置武器经验无效：unitId={unitId}, exp={exp}");
                return false;
            }

            entry.weaponExp = exp;
            NotifyChanged(EventNames.CharacterDataUpdated);
            return true;
        }

        /// <summary>
        /// 设置突破阶段；0 = 未突破，1~N 对应 BreakthroughTable 行，阶段校验由养成层负责。
        /// </summary>
        /// <param name="unitId">单位 ID。</param>
        /// <param name="stage">目标突破阶段，必须不小于 0。</param>
        /// <returns>成功返回 true；未拥有该角色或参数非法返回 false。</returns>
        public bool SetBreakStage(string unitId, int stage)
        {
            if (!TryGetMutableEntry(unitId, out var entry) || stage < 0)
            {
                Debug.LogError($"设置突破阶段无效：unitId={unitId}, stage={stage}");
                return false;
            }

            entry.breakStage = stage;
            NotifyChanged(EventNames.CharacterDataUpdated);
            return true;
        }

        /// <summary>
        /// 设置普攻技能等级；技能上限由养成层控制，此处只做下限防御。
        /// </summary>
        /// <param name="unitId">单位 ID。</param>
        /// <param name="level">目标等级，必须不小于 1。</param>
        /// <returns>成功返回 true；未拥有该角色或参数非法返回 false。</returns>
        public bool SetSkillLevelNormal(string unitId, int level)
        {
            return SetSkillLevel(unitId, level, nameof(SetSkillLevelNormal),
                (entry, value) => entry.skillLevelNormal = value);
        }

        /// <summary>
        /// 设置小技能（E）等级；技能上限由养成层控制，此处只做下限防御。
        /// </summary>
        /// <param name="unitId">单位 ID。</param>
        /// <param name="level">目标等级，必须不小于 1。</param>
        /// <returns>成功返回 true；未拥有该角色或参数非法返回 false。</returns>
        public bool SetSkillLevelE(string unitId, int level)
        {
            return SetSkillLevel(unitId, level, nameof(SetSkillLevelE),
                (entry, value) => entry.skillLevelE = value);
        }

        /// <summary>
        /// 设置爆发技能（Q）等级；技能上限由养成层控制，此处只做下限防御。
        /// </summary>
        /// <param name="unitId">单位 ID。</param>
        /// <param name="level">目标等级，必须不小于 1。</param>
        /// <returns>成功返回 true；未拥有该角色或参数非法返回 false。</returns>
        public bool SetSkillLevelQ(string unitId, int level)
        {
            return SetSkillLevel(unitId, level, nameof(SetSkillLevelQ),
                (entry, value) => entry.skillLevelQ = value);
        }

        /// <summary>
        /// 设置天赋技能等级；技能上限由养成层控制，此处只做下限防御。
        /// </summary>
        /// <param name="unitId">单位 ID。</param>
        /// <param name="level">目标等级，必须不小于 1。</param>
        /// <returns>成功返回 true；未拥有该角色或参数非法返回 false。</returns>
        public bool SetSkillLevelTalent(string unitId, int level)
        {
            return SetSkillLevel(unitId, level, nameof(SetSkillLevelTalent),
                (entry, value) => entry.skillLevelTalent = value);
        }

        /// <summary>
        /// 解锁指定节点（天赋/属性加成）；解锁即永久，重复解锁幂等拒绝。
        /// </summary>
        /// <param name="unitId">单位 ID。</param>
        /// <param name="nodeId">节点 ID，对应 UnlockNodeTable 主键。</param>
        /// <returns>新解锁返回 true；未拥有该角色、参数非法或已解锁返回 false。</returns>
        public bool UnlockNode(string unitId, int nodeId)
        {
            if (!TryGetMutableEntry(unitId, out var entry) || nodeId <= 0)
            {
                Debug.LogError($"解锁节点参数无效：unitId={unitId}, nodeId={nodeId}");
                return false;
            }

            if (entry.unlockedNodes.Contains(nodeId)) return false;

            entry.unlockedNodes.Add(nodeId);
            NotifyChanged(EventNames.CharacterDataUpdated);
            return true;
        }

        #endregion

        #region 批量提交（养成事务）

        //批量域深度：>0 时变更只标脏不落盘不出广播，域闭合统一提交一次
        private int batchDepth;

        //批量域内发生过变更
        private bool batchDirty;

        /// <summary>开启批量提交域：域内变更不落盘不出广播，EndBatch 统一提交（养成事务用，可嵌套）。</summary>
        public void BeginBatch()
        {
            batchDepth++;
        }

        /// <summary>关闭批量提交域：有脏数据时统一落盘并广播一次；无配对 BeginBatch 时报错忽略。</summary>
        public void EndBatch()
        {
            if (batchDepth <= 0)
            {
                Debug.LogError("[CharacterWarehouseManager] EndBatch 无配对 BeginBatch，忽略");
                return;
            }

            batchDepth--;
            if (batchDepth == 0 && batchDirty)
            {
                batchDirty = false;
                SaveData();
                EventCenter.Instance.SetEventTrigger(EventNames.CharacterDataUpdated);
            }
        }

        /// <summary>内部数据引用（养成事务快照专用，调用方只做序列化，不得改动）。</summary>
        public CharacterWarehouseData SnapshotData()
        {
            return data;
        }

        /// <summary>整份数据替换（养成事务回滚/启动恢复专用）：替换后按当前批量域状态落盘广播。</summary>
        public void RestoreData(CharacterWarehouseData restored)
        {
            data = restored ?? new CharacterWarehouseData();
            Normalize();
            NotifyChanged(EventNames.CharacterDataUpdated);
        }

        #endregion

        #region 持久化

        /// <summary>
        /// 将当前角色仓库数据序列化到本地文件。
        /// </summary>
        public void SaveData()
        {
            BinaryDataManager.Instance.Save(SaveFolder, SaveFileName, data);
        }

        /// <summary>
        /// 从本地文件加载角色仓库数据，文件不存在或反序列化失败时使用空仓库。
        /// </summary>
        public void LoadData()
        {
            data = BinaryDataManager.Instance.Load<CharacterWarehouseData>(SaveFolder, SaveFileName) ?? new CharacterWarehouseData();

            // 防御外部或旧档产生的脏数据
            Normalize();
        }

        #endregion

        #region Private

        /// <summary>
        /// 查找指定角色的条目。
        /// </summary>
        /// <param name="unitId">单位 ID。</param>
        /// <returns>对应条目；不存在返回 null。</returns>
        private CharacterEntry FindEntry(string unitId)
        {
            for (var i = 0; i < data.characters.Count; i++)
            {
                if (data.characters[i].unitId == unitId) return data.characters[i];
            }

            return null;
        }

        /// <summary>
        /// 取可变更条目：未拥有该角色时返回 false（含日志），供变更方法统一前置校验。
        /// </summary>
        /// <param name="unitId">单位 ID。</param>
        /// <param name="entry">对应条目。</param>
        /// <returns>已拥有返回 true。</returns>
        private bool TryGetMutableEntry(string unitId, out CharacterEntry entry)
        {
            entry = FindEntry(unitId);
            if (entry == null) Debug.LogError($"角色未拥有：unitId={unitId}");
            return entry != null;
        }

        /// <summary>
        /// 技能等级设置的公共骨架：下限防御 + 赋值 + 统一出口。
        /// </summary>
        /// <param name="unitId">单位 ID。</param>
        /// <param name="level">目标等级。</param>
        /// <param name="methodName">调用方方法名，用于日志定位。</param>
        /// <param name="apply">赋值动作。</param>
        /// <returns>成功返回 true；未拥有该角色或参数非法返回 false。</returns>
        private bool SetSkillLevel(string unitId, int level, string methodName, System.Action<CharacterEntry, int> apply)
        {
            if (!TryGetMutableEntry(unitId, out var entry) || level < 1)
            {
                Debug.LogError($"设置技能等级无效（{methodName}）：unitId={unitId}, level={level}");
                return false;
            }

            apply(entry, level);
            NotifyChanged(EventNames.CharacterDataUpdated);
            return true;
        }

        /// <summary>
        /// 加载后清理：同一 unitId 只保留首个条目，剔除非法条目与非法字段值。
        /// </summary>
        private void Normalize()
        {
            // 剔除非法条目
            data.characters.RemoveAll(e => e == null || string.IsNullOrWhiteSpace(e.unitId));

            // 同 unitId 重复条目保留首个（养成数据无可合并语义）
            var seen = new HashSet<string>();
            for (var i = data.characters.Count - 1; i >= 0; i--)
            {
                if (!seen.Add(data.characters[i].unitId))
                {
                    data.characters.RemoveAt(i);
                    continue;
                }

                // 字段值下限防御
                var entry = data.characters[i];
                if (entry.level < 1) entry.level = 1;
                if (entry.breakStage < 0) entry.breakStage = 0;
                if (entry.skillLevelNormal < 1) entry.skillLevelNormal = 1;
                if (entry.skillLevelE < 1) entry.skillLevelE = 1;
                if (entry.skillLevelQ < 1) entry.skillLevelQ = 1;
                if (entry.skillLevelTalent < 1) entry.skillLevelTalent = 1; // 旧档缺 [Key(10)] 反序列化为 0
                entry.exp = System.Math.Max(0, entry.exp);

                // 节点列表去重并剔除非法 ID
                var nodes = new HashSet<int>();
                entry.unlockedNodes.RemoveAll(n =>
                {
                    if (n <= 0 || !nodes.Add(n)) return true;
                    return false;
                });
            }
        }

        /// <summary>
        /// 变更后的统一出口：批量域内只标脏（EndBatch 统一提交），域外即改即存并广播指定事件。
        /// </summary>
        /// <param name="eventName">要广播的事件名（EventNames 常量）。</param>
        private void NotifyChanged(string eventName)
        {
            if (batchDepth > 0)
            {
                batchDirty = true;
                return;
            }

            SaveData();
            EventCenter.Instance.SetEventTrigger(eventName);
        }

        #endregion
    }
}
