using System;
using System.Collections.Generic;
using TMPro;
using Core.Gear;
using Framework.Gameplay.Abilities;
using Framework.Gameplay.Abilities.BetterHSM.Combat;
using Framework.Gameplay.Battle;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// HSM 状态调试面板（F1 切换开/关，开关在 CombatManager）：
/// - 点击选中：左键射线检测（selectUnitLayers，默认 Character 层），命中视图根经
///   CombatManager.TryGetUnitByView 反查单位；UI 悬停（IsPointerOverGameObject）不穿透选中；
/// - 状态读取纯轮询（BetterHSM 无转换事件、不存旧状态）：GAS OwnerContext TryGet(BetterHSM) →
///   CurrentStateType，面板每帧 diff——变更即记录 prev→current + 时间戳（最近 8 条追加日志）；
/// - 状态名：反射各状态的 BehaviorKey 常量（Idle/Move/Attack/Death），Type.Name 兜底；
/// - 参数区：HSM 上下文标志（WantAttack/IsMoving/Downed）+ 模拟域（Hp/Vital/位置/目的地/目标）；
/// - 数据未注册（视图异步加载晚于面板刷新）显示 Not Ready，不报错（项目懒就绪惯例）；
/// - 文本全英文（字体缺中文字形），值未变不重写（TMP 重建节流）。
/// </summary>
public class HsmDebugPanel : PanelBase
{
    // 转换日志保留条数（超出移除最旧一条）
    private const int MaxTransitionEntries = 8;

    [Header("View 视图分区")]
    // 根视图（状态/参数/日志文本引用）
    [SerializeField] private HsmDebugPanelView view;

    [Header("Click Select（点击选中）")]
    [Tooltip("点击选中射线检测层（默认 Character 层；敌人视图无碰撞体且在其他层，天然选不中）")]
    [SerializeField] private LayerMask selectUnitLayers = 1 << 6;

    // 选中单位与视图（view 销毁由 Unity null 判定兜底；unit 为纯 C# 对象随模拟域存活）
    private BattleUnitState selectedUnit;
    private GameObject selectedView;
    // 状态 diff 缓存：上次见到的状态类型（null = 选中后尚未读到状态）
    private Type lastSeenStateType;
    // 转换日志（旧→新，最新追加在末尾）
    private readonly List<string> transitionLog = new List<string>();
    // 状态名映射缓存（Type → BehaviorKey/Name；反射一次后复用）
    private static readonly Dictionary<Type, string> stateNameCache = new Dictionary<Type, string>();

    #region PanelBase 生命周期

    /// <summary>事件监听与按钮绑定：本面板纯轮询+点击，无 EventCenter 订阅；仅接线视图。</summary>
    protected override void EventInit()
    {
        if (view == null)
        {
            view = GetComponent<HsmDebugPanelView>() ?? GetComponentInChildren<HsmDebugPanelView>(true);
        }
        if (view == null)
        {
            Debug.LogError("[HsmDebugPanel] 缺 HsmDebugPanelView，调试面板停摆。", this);
            return;
        }
        view.EnsureBindings();
    }

    /// <summary>组件初始化：无种子数据（选中态由点击驱动）。</summary>
    protected override void ComponentInit()
    {
    }

    /// <summary>逐帧轮询：处理点击选中 + 刷新读数（值未变不重写）。</summary>
    protected override void OnUpdate()
    {
        HandleClickSelect();
        RefreshReadout();
    }

    /// <summary>面板销毁：无订阅需退订，走基类资源释放。</summary>
    protected override void OnDestroy()
    {
        base.OnDestroy();
    }

    #endregion

    #region 点击选中

    // 左键点击选中：UI 悬停不穿透；射线只打 selectUnitLayers（默认 Character 层）；
    // 碰撞体随 prefab 挂在视图骨盆骨骼上（手动挂），命中即得视图实例。
    private void HandleClickSelect()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, selectUnitLayers.value)) return;

        GameObject clicked = hit.collider.gameObject;
        CombatManager mgr = CombatManager.Current;
        if (mgr == null || !mgr.TryGetUnitByView(clicked, out BattleUnitState unit)) return;

        SelectUnit(unit, clicked);
    }

    // 记录选中并重置 diff/日志缓存（换人后转换日志从零计）
    private void SelectUnit(BattleUnitState unit, GameObject view)
    {
        selectedUnit = unit;
        selectedView = view;
        lastSeenStateType = null;
        transitionLog.Clear();
    }

    #endregion

    #region 读数刷新

    // 刷新面板读数：无选中 → 提示；GAS/数据未就绪 → Not Ready；就绪 → 状态 diff + 参数 + 日志。
    private void RefreshReadout()
    {
        if (view == null) return;

        if (selectedUnit == null || selectedView == null)
        {
            WriteText(view.unitNameText, "No unit selected (left-click a character)");
            WriteText(view.prevStateText, "Prev State: -");
            WriteText(view.currStateText, "Current State: -");
            WriteText(view.paramsText, string.Empty);
            WriteText(view.transitionLogText, string.Empty);
            return;
        }

        // 数据链：视图 → GAS → OwnerContext（BetterHSM 数据注册晚于视图出现时显示 Not Ready）。
        // GetComponentInParent：命中视图可能是子骨骼节点（CH0221 碰撞体在 Bip001 Pelvis 上），GAS 在根——
        // 与 UnitStatPanel Not ready 同根因（2026-10-27），子节点取不到能力组件面板失效。
        GameplayAbilitySystem gas = selectedView.GetComponentInParent<GameplayAbilitySystem>();
        BetterHSMAbilityRuntimeData hsmData = null;
        if (gas != null && gas.OwnerContext != null)
        {
            gas.OwnerContext.TryGet(AbilityRuntimeDataType.BetterHSM, out hsmData);
        }

        WriteText(view.unitNameText,
            $"{selectedUnit.UnitId} ({selectedUnit.Camp})  HP {selectedUnit.Hp} [{selectedUnit.Vital}]");

        Type currentType = hsmData != null ? hsmData.AbilityContext.CurrentStateType : null;
        string currentName = currentType != null ? ResolveStateName(currentType) : "NotReady";
        string prevName = lastSeenStateType != null ? ResolveStateName(lastSeenStateType) : "None";

        // 状态 diff：转换发生（非首次读到状态）时记录旧→新并刷新日志
        if (currentType != lastSeenStateType)
        {
            if (lastSeenStateType != null)
            {
                transitionLog.Add($"{Time.time:F2}s {ResolveStateName(lastSeenStateType)} -> {currentName}");
                if (transitionLog.Count > MaxTransitionEntries) transitionLog.RemoveAt(0);
            }
            lastSeenStateType = currentType;
        }

        WriteText(view.prevStateText, $"Prev State: {prevName}");
        WriteText(view.currStateText, $"Current State: {currentName}");

        // 参数区：HSM 上下文标志（驱动写入的决策数据）+ 模拟域关键值
        CombatStateContext context = hsmData != null
            ? hsmData.AbilityContext.StateContext as CombatStateContext
            : null;
        string paramsText = context != null
            ? $"WantAttack={context.WantAttack} IsMoving={context.IsMoving} Downed={context.Downed}\n"
              + $"Dest={Format(selectedUnit.MoveDestination)} Target={(string.IsNullOrEmpty(selectedUnit.TargetUnitId) ? "-" : selectedUnit.TargetUnitId)}\n"
              + $"GuardPt={selectedUnit.TargetIsGuardPoint} Pos={Format(selectedUnit.Position)}"
            : "HSM context not ready";
        WriteText(view.paramsText, paramsText);

        WriteText(view.transitionLogText,
            transitionLog.Count > 0 ? string.Join("\n", transitionLog) : string.Empty);
    }

    // 文本写入节流：内容未变不重写（TMP 重建开销）
    private static void WriteText(TMP_Text target, string content)
    {
        if (target == null || target.text == content) return;
        target.text = content;
    }

    // 状态名：反射 BehaviorKey 常量（各状态 public const string），缺失兜底 Type.Name
    private static string ResolveStateName(Type stateType)
    {
        if (stateNameCache.TryGetValue(stateType, out string cached)) return cached;
        string name = stateType.GetField("BehaviorKey",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            ?.GetValue(null) as string ?? stateType.Name;
        stateNameCache[stateType] = name;
        return name;
    }

    // 向量定宽格式（调试读数对齐）
    private static string Format(Vector3 v)
    {
        return $"({v.x:F2}, {v.y:F2}, {v.z:F2})";
    }

    #endregion
}
