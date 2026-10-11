using System;
using Core.Gear;
using Framework.Gameplay.Inventory;
using UnityEngine;

/// <summary>
/// 测试道具发放探针：开关打开时向玩家背包自动发放配置清单中的各类道具（仅编辑器生效）。
/// </summary>
[DisallowMultipleComponent]
public class TestItemGranter : MonoBehaviour
{
    /// <summary>单条发放配置：物品 ID 与数量。</summary>
    [Serializable]
    public class ItemGrant
    {
        //物品 ID（以 ItemTable 行为准，如信用点 "Currency_CreditPoint"、角色技能书 "Exp_Character_0"）
        public string itemId;

        //发放数量，必须为正
        public long count;

        /// <summary>构造发放配置（供字段初始化器写默认清单）。</summary>
        /// <param name="itemId">物品 ID。</param>
        /// <param name="count">发放数量。</param>
        public ItemGrant(string itemId, long count)
        {
            this.itemId = itemId;
            this.count = count;
        }
    }

    [Header("发放")]
    [Tooltip("开关：组件启用后延迟约 1 秒自动发放下方清单（延迟是等 Preloader 的新账号默认发放先跑完，避免污染新账号判定）")]
    [SerializeField]
    private bool grantOnEnable = true;

    [Tooltip("发放清单：itemId + 数量，可在 Inspector 增删改（默认覆盖当前全部物品行）")]
    [SerializeField]
    private ItemGrant[] grants = new ItemGrant[]
    {
        new ItemGrant("Currency_StarlightStone", 10000),   // 星辉石
        new ItemGrant("Currency_CreditPoint", 1000000),    // 信用点
        new ItemGrant("Material_UpgradeManual", 999),      // 升级手册
        new ItemGrant("Material_BreakthroughCore", 999),   // 突破核心
        new ItemGrant("Material_RefineCrystal", 999),      // 精炼结晶
        new ItemGrant("Exp_Character_0", 999),             // 角色技能书 I
        new ItemGrant("Exp_Character_1", 300),             // 角色技能书 II
        new ItemGrant("Exp_Character_2", 100),             // 角色技能书 III
        new ItemGrant("Exp_Character_3", 50),              // 角色技能书 IV
        new ItemGrant("Exp_Weapon_0", 999),                // 武器技能书 I
        new ItemGrant("Exp_Weapon_1", 300),                // 武器技能书 II
        new ItemGrant("Exp_Weapon_2", 100),                // 武器技能书 III
        new ItemGrant("Exp_Weapon_3", 50),                 // 武器技能书 IV
    };

    /// <summary>
    /// 组件启用回调：开关打开时延迟约 1 秒自动发放（仅编辑器生效，防止打进正式包污染玩家存档）。
    /// </summary>
    private void OnEnable()
    {
#if UNITY_EDITOR
        if (grantOnEnable) Timer.Instance.AddTimerEvent(1, GrantAll);
#endif
    }

    /// <summary>
    /// 按清单逐条发放：无效条目（空 ID / 非正数量）跳过并告警；
    /// AddItem 变更即存档并广播 BagUpdated（货币与背包 UI 自动跟随刷新）。
    /// 延迟发放是硬要求：新账号判定是「PlayerData/ 目录不存在」，探针在 OnEnable 直接落盘
    /// 会先建出该目录，污染 IsNewAccount 判定后默认角色与玩家信息整段跳过（2026-10-09 实证）。
    /// </summary>
    [ContextMenu("发放清单道具")]
    public void GrantAll()
    {
#if UNITY_EDITOR
        // 无效条目过滤：避免触发 InventoryManager 的入参 LogError 刷屏
        foreach (ItemGrant grant in grants)
        {
            if (string.IsNullOrEmpty(grant.itemId) || grant.count <= 0)
            {
                Debug.LogWarning($"[TestItemGranter] 跳过无效条目：itemId={grant.itemId}, count={grant.count}", this);
                continue;
            }
            InventoryManager.Instance.AddItem(grant.itemId, grant.count);
        }
        Debug.Log($"[TestItemGranter] 已按清单发放 {grants.Length} 条道具配置", this);
#endif
    }
}
