using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 多选上限 Toggle 组（原生 ToggleGroup 的多选扩展）：约束同组内最多 MaxOnCount 个 Toggle 同时选中，
/// 始终支持取消选中。组不经 Toggle.group 挂接（避免原生单选互斥逻辑），由容器经
/// Register/Unregister 动态管理——池化复用的列表项在入池/出池时注销/注册。
/// 注册时已选中的 Toggle 直接计入集合（绑定态同步，不触发选中回调）；仅用户点击引发的
/// 变化广播 SelectionChanged，回调参数按选中先后排序（新选中排尾、取消后自动前移压缩）。
/// </summary>
public class MultiToggleGroup : MonoBehaviour
{
    [Tooltip("同组最多同时选中的 Toggle 数量")]
    [SerializeField] private int maxOnCount = 8;

    //已注册的 Toggle
    private readonly List<Toggle> registeredToggles = new List<Toggle>();

    //当前选中的 Toggle（按选中先后排序）
    private readonly List<Toggle> onToggles = new List<Toggle>();

    //各 Toggle 的值变化监听（池化注销时按引用移除，闭包监听无法直接 RemoveListener）
    private readonly Dictionary<Toggle, UnityAction<bool>> handlers = new Dictionary<Toggle, UnityAction<bool>>();

    /// <summary>选中集合变化回调（参数 = 按选中先后排序的当前选中项，只读视图）。</summary>
    public event Action<IReadOnlyList<Toggle>> SelectionChanged;

    /// <summary>同组最多同时选中的 Toggle 数量（写入时下限钳到 1）。</summary>
    public int MaxOnCount
    {
        get => maxOnCount;
        set => maxOnCount = Mathf.Max(1, value);
    }

    /// <summary>当前选中的 Toggle（按选中先后排序，只读视图）。</summary>
    public IReadOnlyList<Toggle> OnToggles => onToggles;

    /// <summary>
    /// 注册 Toggle 进组：监听其值变化并按当前 isOn 计入选中集合（不触发选中回调）。
    /// 重复注册幂等忽略。
    /// </summary>
    public void Register(Toggle toggle)
    {
        if (toggle == null || registeredToggles.Contains(toggle)) return;

        registeredToggles.Add(toggle);
        UnityAction<bool> handler = isOn => HandleToggleValueChanged(toggle, isOn);
        handlers[toggle] = handler;
        toggle.onValueChanged.AddListener(handler);

        //初始选中直接计入集合（绑定态同步，非用户操作，不广播）
        if (toggle.isOn) onToggles.Add(toggle);
    }

    /// <summary>
    /// 注销 Toggle：移除监听并移出选中集合（不广播——池化回收等程序性注销不是选中变化）。
    /// 未注册时忽略。
    /// </summary>
    public void Unregister(Toggle toggle)
    {
        if (toggle == null) return;

        if (handlers.TryGetValue(toggle, out UnityAction<bool> handler))
        {
            toggle.onValueChanged.RemoveListener(handler);
            handlers.Remove(toggle);
        }
        registeredToggles.Remove(toggle);
        onToggles.Remove(toggle);
    }

    #region Private

    /// <summary>
    /// 成员值变化：选中未满计入并广播；已满弹回关闭（回调再入走移除分支广播一次）；取消移除并广播。
    /// </summary>
    private void HandleToggleValueChanged(Toggle toggle, bool isOn)
    {
        if (isOn)
        {
            if (onToggles.Contains(toggle)) return;

            //满员拒绝：弹回关闭（isOn 赋值触发回调再入 false 分支，统一移除并广播一次）
            if (onToggles.Count >= MaxOnCount)
            {
                toggle.isOn = false;
                return;
            }
            onToggles.Add(toggle);
        }
        else
        {
            onToggles.Remove(toggle);
        }

        SelectionChanged?.Invoke(onToggles);
    }

    /// <summary>
    /// 组件销毁时移除全部成员监听，防止向已销毁组件回调写入。
    /// </summary>
    private void OnDestroy()
    {
        foreach (KeyValuePair<Toggle, UnityAction<bool>> pair in handlers)
        {
            if (pair.Key != null) pair.Key.onValueChanged.RemoveListener(pair.Value);
        }
        handlers.Clear();
        registeredToggles.Clear();
        onToggles.Clear();
    }

    #endregion
}
