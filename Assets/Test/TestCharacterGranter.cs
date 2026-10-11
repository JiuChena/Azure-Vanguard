using Core.Gear;
using Framework.Gameplay.Player;
using UnityEngine;

/// <summary>
/// 测试角色发放探针：开关打开时向角色仓库自动发放配置清单中的角色（仅编辑器生效）。
/// AddCharacter 已拥有时幂等拒绝，重复启用不产生副作用；与 TestItemGranter 同族，后期随 Assets/Test 整体清理。
/// </summary>
[DisallowMultipleComponent]
public class TestCharacterGranter : MonoBehaviour
{
    [Header("发放")]
    [Tooltip("开关：组件启用后延迟约 1 秒自动发放下方清单（延迟是等 Preloader 的新账号默认发放先跑完，重复启用幂等不重复发）")]
    [SerializeField]
    private bool grantOnEnable = true;

    [Tooltip("发放角色 id 清单（CharacterTable 主键），Inspector 可增删改；新角色上表后在此追加")]
    [SerializeField]
    private string[] characterIds = new string[] { "CH0221", "CH0209" };

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
    /// 按清单逐个登记角色：无效 id（空）跳过并告警；已拥有由 AddCharacter 幂等拒绝无副作用。
    /// 延迟发放是硬要求：新账号判定是「PlayerData/ 目录不存在」，探针在 OnEnable 直接落盘
    /// 会先建出该目录，污染 IsNewAccount 判定后默认角色与玩家信息整段跳过（2026-10-09 实证）。
    /// </summary>
    [ContextMenu("发放清单角色")]
    public void GrantAll()
    {
#if UNITY_EDITOR
        int granted = 0;
        foreach (string unitId in characterIds)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                Debug.LogWarning($"[TestCharacterGranter] 跳过无效条目：unitId={unitId}", this);
                continue;
            }
            if (CharacterWarehouseManager.Instance.AddCharacter(unitId)) granted++;
        }
        Debug.Log($"[TestCharacterGranter] 清单 {characterIds.Length} 条，新发放 {granted} 个（其余已拥有幂等跳过）", this);
#endif
    }
}
