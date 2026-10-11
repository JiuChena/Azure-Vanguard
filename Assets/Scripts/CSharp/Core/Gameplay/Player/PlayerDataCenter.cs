using Core.Gear;
using Framework.Gameplay.Inventory;
using Framework.Gameplay.Item;

namespace Framework.Gameplay.Player
{
    /// <summary>
    /// 玩家数据中心门面：统一管理所有玩家存档域（资料/背包/角色仓库/…）。
    /// 各域数据与写入入口仍在各自 Manager（唯一存档者原则不变），此处只做跨域批量操作：
    /// 全量保存、清空重置（调试用）、新账号默认数据发放，以及将来对接服务器时的上传打包入口。
    /// 新增存档域时：建 Data + Manager 按现有模板，并把 SaveData/LoadData 挂入下面两个方法。
    /// </summary>
    public static class PlayerDataCenter
    {
        // 玩家存档在 BinaryDataManager 下的根目录，清空重置时整目录删除；目录不存在即视为新账号
        private const string PlayerDataRoot = "PlayerData";

        // 新账号默认拥有的角色（初始可选，随 CharacterTable 扩充追加）
        private static readonly string[] DefaultCharacterIds = { "CH0221", "CH0209" };

        // 新账号初始金币额度；仅保证养成/商店链路可测，非数值定案
        private const long DefaultCreditPointCount = 50000;

        /// <summary>
        /// 全量保存：遍历所有存档域落盘。用于退游戏/切场景前兜底。
        /// </summary>
        public static void SaveAll()
        {
            PlayerInfoManager.Instance.SaveData();
            InventoryManager.Instance.SaveData();
            CharacterWarehouseManager.Instance.SaveData();
        }

        /// <summary>
        /// 清空全部玩家存档并重载为默认数据（调试/测试用，不可恢复）。
        /// </summary>
        public static void WipeAll()
        {
            BinaryDataManager.Instance.WipeFolder(PlayerDataRoot);
            PlayerInfoManager.Instance.LoadData();
            InventoryManager.Instance.LoadData();
            CharacterWarehouseManager.Instance.LoadData();
        }

        /// <summary>
        /// 是否为新账号：PlayerData/ 目录不存在 = 从未落过任何玩家存档。
        /// </summary>
        public static bool IsNewAccount()
        {
            return !BinaryDataManager.Instance.FolderExists(PlayerDataRoot);
        }

        /// <summary>
        /// 新账号默认数据发放：无存档时发默认角色与初始货币，有存档（老账号）幂等跳过。
        /// 游戏启动时（任何 UI 读档前）调用一次。
        /// </summary>
        public static void EnsureNewAccountDefaults(string baseCurrencyItemId)
        {
            if (!IsNewAccount()) return;

            foreach (string unitId in DefaultCharacterIds)
            {
                CharacterWarehouseManager.Instance.AddCharacter(unitId);
            }

            InventoryManager.Instance.AddItem(baseCurrencyItemId, DefaultCreditPointCount);

            // 各域变更已即存落盘；此处兜底再全量保存一次，保证整个 PlayerData/ 目录完整建立
            SaveAll();
        }
    }
}
