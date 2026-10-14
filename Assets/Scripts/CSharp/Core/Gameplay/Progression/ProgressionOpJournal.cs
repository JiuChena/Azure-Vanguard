using System;
using MessagePack;

namespace Framework.Gameplay.Progression
{
    /// <summary>
    /// 养成操作事务日志：一次养成操作（升级/突破/解锁节点）提交前把背包与角色仓库的
    /// 整份内存数据序列化落盘；操作被打断时（崩溃/异常），下次启动据此整体回滚到操作前状态。
    /// 只存前状态不存增量——恢复即整体替换，天然幂等。
    /// </summary>
    [Serializable]
    [MessagePackObject]
    public class ProgressionOpJournal
    {
        // 格式版本
        [Key(0)]
        public int version = 1;

        // 操作前的背包数据（前状态快照）
        [Key(1)]
        public Framework.Gameplay.Inventory.InventoryData bag;

        // 操作前的角色仓库数据（前状态快照）
        [Key(2)]
        public Framework.Gameplay.Player.CharacterWarehouseData warehouse;
    }
}
