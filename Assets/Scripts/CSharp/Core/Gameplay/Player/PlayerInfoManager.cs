using Core.Gear;
using UnityEngine;

namespace Framework.Gameplay.Player
{
    /// <summary>
    /// 全局玩家信息管理器：玩家资料（头像/昵称/等级/经验）的唯一入口与唯一存档者，变更即保存。
    /// </summary>
    public class PlayerInfoManager
    {
        // 存档相对 Data/ 的子目录
        private const string SaveFolder = "PlayerData/Player/";

        // 存档文件名（不含扩展名）
        private const string SaveFileName = "Info";

        private static PlayerInfoManager instance;
        public static PlayerInfoManager Instance => instance ??= new PlayerInfoManager();

        // 玩家信息存档数据
        private PlayerInfo data;
        public PlayerInfo Data => data;

        private PlayerInfoManager()
        {
            LoadData();
        }

        #region 变更

        /// <summary>
        /// 修改玩家昵称，变更即保存。
        /// </summary>
        /// <param name="nickName">新昵称，空串视为非法入参。</param>
        public void SetNickName(string nickName)
        {
            if (string.IsNullOrEmpty(nickName))
            {
                Debug.LogError($"设置昵称参数无效：nickName={nickName}");
                return;
            }

            data.nickName = nickName;
            SaveData();
        }

        /// <summary>
        /// 更换玩家头像（存头像图集内的资源名），变更即保存。
        /// </summary>
        /// <param name="avatarName">头像资源名称，与 SpriteAtlas 内 sprite 名一致。</param>
        public void SetAvatar(string avatarName)
        {
            if (string.IsNullOrEmpty(avatarName))
            {
                Debug.LogError($"设置头像参数无效：avatarName={avatarName}");
                return;
            }

            data.avatarName = avatarName;
            SaveData();
        }

        /// <summary>
        /// 增加经验并结算连续升级；升级后经验上限沿用当前值，接入等级曲线表后在此结算。
        /// </summary>
        /// <param name="amount">增加的经验，必须为正数。</param>
        public void AddExp(int amount)
        {
            if (amount <= 0)
            {
                Debug.LogError($"增加经验参数无效：amount={amount}");
                return;
            }

            data.exp += amount;
            while (data.exp >= data.maxExp)
            {
                data.exp -= data.maxExp;
                data.level++;
            }

            SaveData();
        }

        #endregion

        #region 持久化

        /// <summary>
        /// 将当前玩家信息序列化到本地文件。
        /// </summary>
        public void SaveData()
        {
            BinaryDataManager.Instance.Save(SaveFolder, SaveFileName, data);
        }

        /// <summary>
        /// 从本地文件加载玩家信息，文件不存在或反序列化失败时使用默认数据。
        /// </summary>
        public void LoadData()
        {
            data = BinaryDataManager.Instance.Load<PlayerInfo>(SaveFolder, SaveFileName) ?? new PlayerInfo();
        }

        #endregion
    }
}
