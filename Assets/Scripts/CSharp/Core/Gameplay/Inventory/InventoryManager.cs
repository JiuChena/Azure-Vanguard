using System.Collections.Generic;
using Core.Gear;
using UnityEngine;

namespace Framework.Gameplay.Inventory
{
    /// <summary>
    /// 全局背包管理器：物品增删查的唯一入口与唯一存档者，变更即保存并广播 BagUpdated 事件。
    /// </summary>
    public class InventoryManager
    {
        // 存档相对 Data/ 的子目录
        private const string SaveFolder = "PlayerData/Inventory/";

        // 存档文件名（不含扩展名）
        private const string SaveFileName = "Bag";

        private static InventoryManager instance;
        public static InventoryManager Instance => instance ??= new InventoryManager();

        // 背包存档数据
        private InventoryData data;

        private InventoryManager()
        {
            LoadData();
        }

        #region 查询

        /// <summary>
        /// 查询指定物品的持有数量。
        /// </summary>
        /// <param name="itemId">物品 ID，与物品配置表主键对齐。</param>
        /// <returns>持有数量；未拥有返回 0。</returns>
        public long GetCount(int itemId)
        {
            var entry = FindEntry(itemId);
            return entry?.count ?? 0;
        }

        /// <summary>
        /// 判断指定物品持有量是否达到要求数量。
        /// </summary>
        /// <param name="itemId">物品 ID。</param>
        /// <param name="amount">要求的数量，应传入正数。</param>
        /// <returns>持有量大于等于要求量返回 true，否则 false。</returns>
        public bool HasEnough(int itemId, long amount)
        {
            return GetCount(itemId) >= amount;
        }

        /// <summary>
        /// 获取全部物品条目的只读快照。
        /// </summary>
        /// <returns>只读视图；外部不得修改，写操作一律走本管理器。</returns>
        public IReadOnlyList<ItemStack> Snapshot()
        {
            return data.items.AsReadOnly();
        }

        #endregion

        #region 增删

        /// <summary>
        /// 增加指定物品数量；自动累加到既有条目，无则新增条目。
        /// </summary>
        /// <param name="itemId">物品 ID，必须为正数。</param>
        /// <param name="count">增加的数量，必须为正数。</param>
        /// <returns>成功返回 true；参数非法返回 false 且无副作用。</returns>
        public bool AddItem(int itemId, long count)
        {
            // 校验参数，非法入参视为调用方错误
            if (itemId <= 0 || count <= 0)
            {
                Debug.LogError($"背包增加物品参数无效：itemId={itemId}, count={count}");
                return false;
            }

            // 累加到既有条目，无则新增
            var entry = FindEntry(itemId);
            if (entry == null) data.items.Add(new ItemStack { itemId = itemId, count = count });
            else entry.count += count;

            // 变更即保存并广播
            NotifyChanged();
            return true;
        }

        /// <summary>
        /// 扣除指定物品数量；数量不足时整体失败，不做部分扣除（原子操作）。
        /// </summary>
        /// <param name="itemId">物品 ID，必须为正数。</param>
        /// <param name="count">扣除的数量，必须为正数。</param>
        /// <returns>成功返回 true；数量不足或参数非法返回 false 且无副作用。</returns>
        public bool TryRemove(int itemId, long count)
        {
            // 校验参数，非法入参视为调用方错误
            if (itemId <= 0 || count <= 0)
            {
                Debug.LogError($"背包扣除物品参数无效：itemId={itemId}, count={count}");
                return false;
            }

            // 数量不足时整体失败
            var entry = FindEntry(itemId);
            if (entry == null || entry.count < count) return false;

            // 扣除并清理归零条目
            entry.count -= count;
            if (entry.count == 0) data.items.Remove(entry);

            // 变更即保存并广播
            NotifyChanged();
            return true;
        }

        #endregion

        #region 持久化

        /// <summary>
        /// 将当前背包数据序列化到本地文件。
        /// </summary>
        public void SaveData()
        {
            BinaryDataManager.Instance.Save(SaveFolder, SaveFileName, data);
        }

        /// <summary>
        /// 从本地文件加载背包数据，文件不存在或反序列化失败时使用空背包。
        /// </summary>
        public void LoadData()
        {
            data = BinaryDataManager.Instance.Load<InventoryData>(SaveFolder, SaveFileName) ?? new InventoryData();

            // 防御外部或旧档产生的脏数据
            Normalize();
        }

        #endregion

        #region Private

        /// <summary>
        /// 查找指定物品的条目。
        /// </summary>
        /// <param name="itemId">物品 ID。</param>
        /// <returns>对应条目；不存在返回 null。</returns>
        private ItemStack FindEntry(int itemId)
        {
            for (var i = 0; i < data.items.Count; i++)
            {
                if (data.items[i].itemId == itemId) return data.items[i];
            }

            return null;
        }

        /// <summary>
        /// 加载后清理：合并同 ID 重复条目并剔除无效条目，保证同一物品只占一条。
        /// </summary>
        private void Normalize()
        {
            // itemId → 首个有效条目的索引
            var firstIndex = new Dictionary<int, int>();
            for (var i = 0; i < data.items.Count; i++)
            {
                var entry = data.items[i];
                if (entry.itemId <= 0 || entry.count <= 0) continue;

                // 同 ID 重复条目合并进首个条目
                if (firstIndex.TryGetValue(entry.itemId, out var master))
                {
                    data.items[master].count += entry.count;
                    entry.count = 0;
                }
                else firstIndex.Add(entry.itemId, i);
            }

            // 剔除脏数据与已合并条目
            data.items.RemoveAll(e => e.itemId <= 0 || e.count <= 0);
        }

        /// <summary>
        /// 变更后的统一出口：即改即存并广播 BagUpdated 事件。
        /// </summary>
        private void NotifyChanged()
        {
            SaveData();
            EventCenter.Instance.SetEventTrigger(EventNames.BagUpdated);
        }

        #endregion
    }
}
