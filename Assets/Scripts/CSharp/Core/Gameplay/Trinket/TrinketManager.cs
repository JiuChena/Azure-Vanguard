using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Gear;
using Framework.Gameplay.Progression;
using Framework.Gameplay.Unit;
using UnityEngine;

namespace Framework.Gameplay.Trinket
{
    /// <summary>
    /// 全局装备管理器：装备实例发放/穿脱的唯一入口与唯一存档者，变更即保存并广播 TrinketUpdated 事件。
    /// 实例模型：装备逐件存在（instanceId 自增），单一事实源 = 实例上的 wearerId（空 = 未穿），
    /// 角色身上在穿 = 扫实例反查；CharacterEntry 零改动，穿脱只动 wearerId。
    /// 配置侧依赖 TrinketTable/EnsembleTable（itemId 派生一切：类别/合奏/词条/图标）；
    /// 三自由格不绑类别，穿脱无限制，已穿再穿给他人默认拒绝（先脱再穿）。
    /// </summary>
    public class TrinketManager
    {
        // 存档相对 Data/ 的子目录
        private const string SaveFolder = "PlayerData/Trinket/";

        // 存档文件名（不含扩展名）
        private const string SaveFileName = "Trinkets";

        // 配置表名（与 Excel 页签名一致）
        private const string TableNameTrinket = "TrinketTable";
        private const string TableNameEnsemble = "EnsembleTable";

        /// <summary>单角色在穿上限（三自由格，不绑类别）。</summary>
        public const int MaxWornCount = 3;

        private static TrinketManager instance;
        public static TrinketManager Instance => instance ??= new TrinketManager();

        // 装备存档数据
        private TrinketData data;

        // 配置表缓存（EnsureTablesAsync 装载；某张失败为 null 时下次调用自动重试）
        private DataTable<TrinketRow, string> trinketTable;
        private DataTable<EnsembleRow, string> ensembleTable;

        private TrinketManager()
        {
            LoadData();
        }

        #region 表接入

        /// <summary>
        /// 确保两张装备表已加载；LoadTableAsync 命中缓存时直接短路，重复调用无代价。
        /// 同步查询或装备页首次展示前 await 一次。
        /// </summary>
        /// <returns>两张表全部就绪返回 true；任一加载失败返回 false（失败详情由 DataTableManager 报错定位）。</returns>
        public async Task<bool> EnsureTablesAsync()
        {
            trinketTable = await DataTableManager.Instance.LoadTableAsync<TrinketRow, string>(TableNameTrinket);
            ensembleTable = await DataTableManager.Instance.LoadTableAsync<EnsembleRow, string>(TableNameEnsemble);
            return trinketTable != null && ensembleTable != null;
        }

        #endregion

        #region 查询

        /// <summary>
        /// 查询角色当前在穿的全部装备实例。
        /// </summary>
        /// <param name="characterId">角色 ID。</param>
        /// <returns>在穿实例列表（新列表；实例本体为引用，外部不得修改，写操作一律走本管理器）；未穿返回空列表。</returns>
        public List<TrinketInstance> GetWornBy(string characterId)
        {
            var worn = new List<TrinketInstance>();
            if (string.IsNullOrEmpty(characterId)) return worn;

            for (var i = 0; i < data.trinkets.Count; i++)
            {
                if (data.trinkets[i].wearerId == characterId) worn.Add(data.trinkets[i]);
            }

            return worn;
        }

        /// <summary>
        /// 查询装备实例的持有角色。
        /// </summary>
        /// <param name="instanceId">实例 ID。</param>
        /// <returns>持有角色 ID；未穿返回空串，实例不存在返回 null。</returns>
        public string GetWearer(ulong instanceId)
        {
            return FindInstance(instanceId)?.wearerId;
        }

        /// <summary>
        /// 获取全部装备实例的只读快照。
        /// </summary>
        /// <returns>只读视图；外部不得修改，写操作一律走本管理器。</returns>
        public IReadOnlyList<TrinketInstance> Snapshot()
        {
            return data.trinkets.AsReadOnly();
        }

        /// <summary>
        /// 合奏判定（纯判定，不写状态）：三件套规则——数量恰为 3、ensembleId 全同非空、三件类别互异
        /// 才触发（三个帽子不触发——不同款帽子也是帽子；两件/一件无任何效果）。
        /// 表未就绪或任一实例查不到行时按不触发处理，不炸运行时。
        /// </summary>
        /// <param name="worn">在穿实例列表（通常传 GetWornBy 的结果）。</param>
        /// <returns>生效的合奏 id；不触发返回 null。</returns>
        public string EvaluateEnsemble(IReadOnlyList<TrinketInstance> worn)
        {
            if (trinketTable == null || worn == null || worn.Count != MaxWornCount) return null;

            string ensembleId = null;
            var categories = new string[MaxWornCount];
            for (var i = 0; i < worn.Count; i++)
            {
                if (!trinketTable.TryGet(worn[i].itemId, out TrinketRow row)) return null;

                // 同一合奏：全部在穿件 ensembleId 一致且非空
                if (i == 0)
                {
                    ensembleId = row.EnsembleId;
                    if (string.IsNullOrEmpty(ensembleId)) return null;
                }
                else if (row.EnsembleId != ensembleId) return null;

                categories[i] = row.Category;
            }

            // 类别互异：三件两两不同
            for (var i = 0; i < categories.Length; i++)
            {
                for (var j = i + 1; j < categories.Length; j++)
                {
                    if (categories[i] == categories[j]) return null;
                }
            }

            return ensembleId;
        }

        #endregion

        #region 发放与穿脱

        /// <summary>
        /// 发放装备实例：按 itemId 生成 count 件独立实例（同 id 多件互不影响，各自可穿）。
        /// </summary>
        /// <param name="itemId">装备 id，必须存在于 TrinketTable。</param>
        /// <param name="count">发放件数，必须为正数。</param>
        /// <returns>成功返回 true；表未就绪、id 不存在或参数非法返回 false 且无副作用。</returns>
        public async Task<bool> GrantAsync(string itemId, int count)
        {
            // 校验参数，非法入参视为调用方错误
            if (string.IsNullOrEmpty(itemId) || count <= 0)
            {
                Debug.LogError($"装备发放参数无效：itemId={itemId}, count={count}");
                return false;
            }

            if (!await EnsureTablesAsync()) return false;
            if (!trinketTable.TryGet(itemId, out _))
            {
                Debug.LogError($"装备发放失败：TrinketTable 无 id={itemId} 的行");
                return false;
            }

            for (var i = 0; i < count; i++)
            {
                data.trinkets.Add(new TrinketInstance { instanceId = data.nextInstanceId++, itemId = itemId, wearerId = "" });
            }

            // 变更即保存并广播
            NotifyChanged();
            return true;
        }

        /// <summary>
        /// 穿戴装备：把指定实例写给指定角色（写实例 wearerId 即在穿）。
        /// 三自由格不绑类别，任意组合可穿；同件不可同时穿多人。
        /// </summary>
        /// <param name="instanceId">实例 ID。</param>
        /// <param name="characterId">角色 ID。</param>
        /// <returns>成功返回 true；实例不存在、已在穿或该角色在穿已满 3 件返回 false 且无副作用。</returns>
        public bool Equip(ulong instanceId, string characterId)
        {
            // 校验参数，非法入参视为调用方错误
            if (string.IsNullOrEmpty(characterId))
            {
                Debug.LogError($"装备穿戴参数无效：characterId 为空（instanceId={instanceId}）");
                return false;
            }

            var inst = FindInstance(instanceId);
            if (inst == null)
            {
                Debug.LogError($"装备穿戴失败：实例不存在 instanceId={instanceId}");
                return false;
            }

            // 已在穿：拒绝（先脱再穿），玩家可触发走通知通道
            if (!string.IsNullOrEmpty(inst.wearerId))
            {
                NotifyPlayer("Already worn");
                return false;
            }

            // 三自由格上限：该角色在穿不足 3 件才可穿戴
            if (GetWornBy(characterId).Count >= MaxWornCount)
            {
                NotifyPlayer("Trinket slots are full");
                return false;
            }

            inst.wearerId = characterId;

            // 变更即保存并广播
            NotifyChanged();
            return true;
        }

        /// <summary>
        /// 脱下装备：清空实例 wearerId。穿脱无限制，随时可脱。
        /// </summary>
        /// <param name="instanceId">实例 ID。</param>
        /// <returns>成功返回 true；实例不存在或本就未穿返回 false。</returns>
        public bool Unequip(ulong instanceId)
        {
            var inst = FindInstance(instanceId);
            if (inst == null)
            {
                Debug.LogError($"装备脱下失败：实例不存在 instanceId={instanceId}");
                return false;
            }

            if (string.IsNullOrEmpty(inst.wearerId)) return false;

            inst.wearerId = "";

            // 变更即保存并广播
            NotifyChanged();
            return true;
        }

        #endregion

        #region 属性贡献

        /// <summary>
        /// 解析角色在穿装备的属性贡献（主 + 3 副词条 + 生效合奏加成，全部表配死无成长）。
        /// 属性结算与装备页 UI 共用此入口，保证展示与结算同源。
        /// </summary>
        /// <param name="characterId">角色 ID。</param>
        /// <returns>贡献载荷；表未就绪或未穿装备返回 default（无贡献）。</returns>
        public async Task<TrinketStatContribution> GetTrinketContributionAsync(string characterId)
        {
            if (!await EnsureTablesAsync()) return default;

            List<TrinketInstance> worn = GetWornBy(characterId);
            if (worn.Count == 0) return default;

            // 主 + 3 副词条：全部在穿件依序摊平（每件 4 条），平面/百分比键由计算器按键路由
            var keys = new List<string>(worn.Count * 4);
            var values = new List<float>(worn.Count * 4);
            for (var i = 0; i < worn.Count; i++)
            {
                if (!trinketTable.TryGet(worn[i].itemId, out TrinketRow row))
                {
                    Debug.LogError($"装备属性结算：TrinketTable 无 id={worn[i].itemId} 的行，该件贡献跳过");
                    continue;
                }

                AppendStat(keys, values, row.MainStatType, row.MainStatValue);
                AppendStat(keys, values, row.SubStat1Type, row.SubStat1Value);
                AppendStat(keys, values, row.SubStat2Type, row.SubStat2Value);
                AppendStat(keys, values, row.SubStat3Type, row.SubStat3Value);
            }

            // 生效合奏的加成并入（不触发则无此段）
            string ensembleId = EvaluateEnsemble(worn);
            if (ensembleId != null && ensembleTable.TryGet(ensembleId, out EnsembleRow ensemble))
            {
                ensemble.GetBonus(out string[] bonusKeys, out float[] bonusValues);
                for (var i = 0; i < bonusKeys.Length; i++) AppendStat(keys, values, bonusKeys[i], bonusValues[i]);
            }

            if (keys.Count == 0) return default;
            return new TrinketStatContribution(keys.ToArray(), values.ToArray());
        }

        #endregion

        #region 持久化

        /// <summary>
        /// 将当前装备数据序列化到本地文件。
        /// </summary>
        public void SaveData()
        {
            BinaryDataManager.Instance.Save(SaveFolder, SaveFileName, data);
        }

        /// <summary>
        /// 从本地文件加载装备数据，文件不存在或反序列化失败时使用空数据。
        /// </summary>
        public void LoadData()
        {
            try
            {
                data = BinaryDataManager.Instance.Load<TrinketData>(SaveFolder, SaveFileName) ?? new TrinketData();
            }
            catch (Exception e)
            {
                Debug.LogError($"装备存档反序列化失败，装备数据重置：{e.Message}");
                data = new TrinketData();
            }

            // 防御外部或旧档产生的脏数据
            Normalize();
        }

        #endregion

        #region Private

        /// <summary>查找指定实例；不存在返回 null。</summary>
        private TrinketInstance FindInstance(ulong instanceId)
        {
            for (var i = 0; i < data.trinkets.Count; i++)
            {
                if (data.trinkets[i].instanceId == instanceId) return data.trinkets[i];
            }

            return null;
        }

        /// <summary>追加一条词条到键值列表；空键滤除（表配错不炸运行时）。</summary>
        private static void AppendStat(List<string> keys, List<float> values, string key, float value)
        {
            if (string.IsNullOrEmpty(key)) return;

            keys.Add(key);
            values.Add(value);
        }

        /// <summary>
        /// 加载后清理：剔除无效实例（空 itemId / 非法实例 ID），并保证自增计数器大于既有最大实例 ID（防外部改档撞号）。
        /// </summary>
        private void Normalize()
        {
            data.trinkets.RemoveAll(t => t == null || t.instanceId == 0 || string.IsNullOrEmpty(t.itemId));
            foreach (TrinketInstance t in data.trinkets)
            {
                if (data.nextInstanceId <= t.instanceId) data.nextInstanceId = t.instanceId + 1;
            }
        }

        /// <summary>
        /// 变更后的统一出口：即改即存并广播 TrinketUpdated 事件。
        /// </summary>
        private void NotifyChanged()
        {
            SaveData();
            EventCenter.Instance.SetEventTrigger(EventNames.TrinketUpdated);
        }

        /// <summary>
        /// 玩家可触发的拒绝（已在穿/格子已满）：经事件总线（NoticeRequested）交给常驻通知面板弹出，
        /// 不发 LogError（Core 不依赖 UI）；LogError 只留给开发路径错误（调用方违约、配置表缺失）。
        /// </summary>
        private static void NotifyPlayer(string message)
        {
            EventCenter.Instance.SetEventTrigger(EventNames.NoticeRequested, message);
        }

        #endregion
    }
}
