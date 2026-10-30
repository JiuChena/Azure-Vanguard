using Framework.Gameplay.Abilities;
using Framework.Gameplay.Battle;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 战场点击拾取（静态工具）：左键射线拾取单位视图，经 CombatManager 反查模拟单位。
/// 对齐 HsmDebugPanel 先例的拾取口径，收拢进独立静态类供 UnitStatPanel 副面板与
/// CombatPanel 点击探针共用：
/// - UI 悬停（IsPointerOverGameObject）不穿透——UnitStatPanel 面板 BG 已 raycastTarget=false；
/// - 掩码 SelectableLayers = Character(1&lt;&lt;6) + 敌人 Default(1&lt;&lt;0)；角色/敌人视图
///   命中碰撞体随 prefab 自带（骨盆骨骼手动挂），代码不补挂。
/// </summary>
public static class CombatSelectionPicker
{
    /// <summary>
    /// 可点选层掩码：Character 层 + Default 层（角色/敌人视图 prefab 自带命中碰撞体）。
    /// </summary>
    public static LayerMask SelectableLayers = (1 << 6) | (1 << 0);

    /// <summary>
    /// 拾取一次左键点击命中的单位（本帧仅首次左键有效，其余返回 false）。
    /// </summary>
    /// <param name="unit">命中的单位模拟状态；未命中为 null。</param>
    /// <param name="view">命中的表现视图；未命中为 null。</param>
    /// <returns>命中可点选单位返回 true。</returns>
    public static bool TryPick(out BattleUnitState unit, out GameObject view)
    {
        unit = null;
        view = null;
        if (!Input.GetMouseButtonDown(0)) return false;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return false;

        Camera cam = Camera.main;
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, SelectableLayers.value)) return false;

        GameObject clicked = hit.collider.gameObject;
        CombatManager mgr = CombatManager.Current;
        if (mgr == null || !mgr.TryGetUnitByView(clicked, out BattleUnitState picked)) return false;

        unit = picked;
        // 上溯到根视图：命中可能是子骨骼节点（CH0221 碰撞体挂在 Bip001 Pelvis 子骨骼上），
        // 能力组件 GAS 在根——面板取 Display 数据 / 聚焦相机 Follow/LookAt 都要根上的 GAS transform
        // （2026-10-27 修，与 UnitStatPanel Not ready 同根因；TryGetUnitByView 只反查单位不上溯视图）
        view = clicked;
        GameplayAbilitySystem gas = view.GetComponentInParent<GameplayAbilitySystem>();
        if (gas != null) view = gas.gameObject;
        return true;
    }

    /// <summary>
    /// 本帧是否存在"落在世界内"的左键点击（未悬停 UI、相机可用）。
    /// 语义：点单位 = 选中；点单位以外（地面/空地） = 清选中回提示态。
    /// 与 <see cref="TryPick"/> 组合使用：先 TryPick 命中 → Select；未命中且本方法为 true → Clear。
    /// </summary>
    /// <returns>本帧左键落在世界（非 UI）返回 true。</returns>
    public static bool IsWorldClick()
    {
        if (!Input.GetMouseButtonDown(0)) return false;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return false;
        return Camera.main != null;
    }
}
