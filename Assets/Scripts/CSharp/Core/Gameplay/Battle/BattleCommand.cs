using System.Collections.Generic;
using UnityEngine;

namespace Framework.Gameplay.Battle
{
    /// <summary>指令类型：⑤④→③ 的全部通道；BuyBuff/BuyUpgrade 为挂账 #30 预留位。</summary>
    public enum BattleCommandType
    {
        Deploy,
        Move,
        Revive,
        BuyBuff,
        BuyUpgrade,
    }

    /// <summary>指令记账结果：被拒指令同样入账（REJECT+原因，seq 不断号）——服务器复核口径。</summary>
    public enum BattleCommandResult
    {
        Applied,
        Rejected,
    }

    /// <summary>
    /// 指令流水条目（战报定稿落点）：seq 由 BattleController 按提交顺序分配，
    /// cost 为 ③ 按规则算出的实际扣费（Rejected 恒 0）。
    /// </summary>
    public sealed class BattleCommandRecord
    {
        public int Seq { get; }
        public BattleCommandType Type { get; }
        public string UnitId { get; }
        public Vector3 Position { get; }
        public BattleCommandResult Result { get; }
        public string RejectReason { get; }
        public int Cost { get; }

        public BattleCommandRecord(int seq, BattleCommandType type, string unitId, Vector3 position,
            BattleCommandResult result, string rejectReason, int cost)
        {
            Seq = seq;
            Type = type;
            UnitId = unitId;
            Position = position;
            Result = result;
            RejectReason = rejectReason;
            Cost = cost;
        }
    }

    /// <summary>
    /// 指令流水账本：只增不改。seq 连续性由本类保证（NextSeq 顺序分配），
    /// ValidateSequence 供服务器复核/验证脚本断言。
    /// </summary>
    public sealed class BattleCommandLog
    {
        private readonly List<BattleCommandRecord> records = new List<BattleCommandRecord>();

        // 已分配条数（下一条 seq = count + 1，从 1 起）
        public int Count => records.Count;

        public IReadOnlyList<BattleCommandRecord> Records => records;

        // 追加一条（BattleController 专用语义，公开便于验证脚本比对）
        public BattleCommandRecord Append(BattleCommandType type, string unitId, Vector3 position,
            BattleCommandResult result, string rejectReason, int cost)
        {
            BattleCommandRecord record = new BattleCommandRecord(records.Count + 1, type, unitId, position,
                result, rejectReason, cost);
            records.Add(record);
            return record;
        }

        /// <summary>seq 是否从 1 连续递增（被拒指令也占号，不应断号）。</summary>
        public bool ValidateSequence()
        {
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].Seq != i + 1) return false;
            }
            return true;
        }
    }
}
