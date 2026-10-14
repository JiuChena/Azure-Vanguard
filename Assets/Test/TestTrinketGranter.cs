using System;
using Core.Gear;
using Framework.Gameplay.Trinket;
using UnityEngine;

/// <summary>
/// 测试装备发放探针：开关打开时向账号发放配置清单中的装备实例（仅编辑器生效）。
/// 每件 = 一个独立实例（同 id 多件聚合成背包计数）。
/// </summary>
[DisallowMultipleComponent]
public class TestTrinketGranter : MonoBehaviour
{
    /// <summary>单条发放配置：装备 ID 与件数。</summary>
    [Serializable]
    public class TrinketGrant
    {
        //装备 ID（以 TrinketTable 行为准，如 "Badge_Tier1_Q1"）
        public string itemId;

        //发放件数（每件一个独立实例），必须为正
        public int count;

        /// <summary>构造发放配置（供字段初始化器写默认清单）。</summary>
        public TrinketGrant(string itemId, int count)
        {
            this.itemId = itemId;
            this.count = count;
        }
    }

    [Header("发放")]
    [Tooltip("开关：组件启用后延迟约 1 秒自动发放下方清单（延迟是等 Preloader 的新账号默认发放先跑完，同 TestItemGranter 口径）")]
    [SerializeField]
    private bool grantOnEnable = true;

    [Tooltip("发放清单：装备 id + 件数，可在 Inspector 增删改（默认含两套合奏件、散件九类与两位数计数样本）")]
    [SerializeField]
    private TrinketGrant[] grants = new TrinketGrant[]
    {
        new TrinketGrant("Badge_Tier1_Q1", 1),     // 合奏 1（Badge 系）三件
        new TrinketGrant("Badge_Tier1_Q2", 1),
        new TrinketGrant("Badge_Tier1_Q3", 1),
        new TrinketGrant("Hat_Tier1_Q1", 2),       // 合奏 2（Hat 系）
        new TrinketGrant("Hat_Tier1_Q2", 1),
        new TrinketGrant("Necklace_Tier1_Q1", 12), // 两位数计数样本
        new TrinketGrant("Shoes_Tier1_Q1", 3),
        new TrinketGrant("Charm_Tier1_Q1", 1),
        new TrinketGrant("Gloves_Tier1_Q1", 1),
        new TrinketGrant("Watch_Tier1_Q1", 1),
        new TrinketGrant("Bag_Tier1_Q1", 1),
        new TrinketGrant("Hairpin_Tier1_Q1", 1),
    };

    /// <summary>
    /// 组件启用回调：开关打开时延迟约 1 秒自动发放（仅编辑器生效）。
    /// 延迟同 TestItemGranter：OnEnable 直接落盘会污染 IsNewAccount 判定。
    /// </summary>
    private void OnEnable()
    {
#if UNITY_EDITOR
        if (grantOnEnable) Timer.Instance.AddTimerEvent(1, GrantAll);
#endif
    }

    /// <summary>按清单逐条发放：无效条目（空 ID / 非正数量）跳过并告警；表内无该 id 由 GrantAsync 自行拒绝。</summary>
    [ContextMenu("发放清单装备")]
    public void GrantAll()
    {
#if UNITY_EDITOR
        _ = GrantAllAsync();
#endif
    }

    //顺序发放：GrantAsync 内含异步存档，并发 fire-and-forget 会在同一存档文件上赛跑
    private async System.Threading.Tasks.Task GrantAllAsync()
    {
        int granted = 0;
        foreach (TrinketGrant grant in grants)
        {
            if (string.IsNullOrEmpty(grant.itemId) || grant.count <= 0)
            {
                Debug.LogWarning($"[TestTrinketGranter] 跳过无效条目：itemId={grant.itemId}, count={grant.count}", this);
                continue;
            }
            if (await TrinketManager.Instance.GrantAsync(grant.itemId, grant.count)) granted++;
            else Debug.LogWarning($"[TestTrinketGranter] 发放被拒：itemId={grant.itemId}（表内无此行或参数非法）", this);
        }
        Debug.Log($"[TestTrinketGranter] 发放完成 {granted}/{grants.Length} 条装备配置", this);
    }
}
