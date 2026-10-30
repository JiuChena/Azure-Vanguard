using System;
using UnityEngine;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 战场选中态（纯 C#）：当前选中的单位与其表现视图。
    /// 选中态比面板长寿——面板可 ESC 关闭后再重开，选中保留；CombatManager Start 建实例、
    /// OnDestroy 置空（挂账对接期接线）。面板经 <see cref="Current"/> 单点读选中，轮询刷新。
    /// 点击拾取经 <see cref="CombatSelectionPicker"/>；本类只管"选中了谁"的状态，不关心怎么拾取。
    /// </summary>
    public sealed class BattleSelection
    {
        // 当前战斗的选中态（CombatManager Start 建 / OnDestroy 置空；无战斗时为空）
        private static BattleSelection _current;

        // 选中单位（纯 C# 对象，随模拟域存活）与表现视图（GameObject，销毁由 Unity null 判定兜底）
        private BattleUnitState _unit;
        private GameObject _view;

        /// <summary>创建选中态并登记为当前实例（CombatManager Start 调用）。</summary>
        public BattleSelection()
        {
            _current = this;
        }

        /// <summary>当前选中态；无战斗（CombatManager 未装配/已销毁）时为 null。</summary>
        public static BattleSelection Current => _current;

        /// <summary>是否处于战斗中（存在选中态实例）。</summary>
        public static bool IsInBattle => _current != null;

        /// <summary>选中单位；未选中为 null。</summary>
        public BattleUnitState SelectedUnit => _unit;

        /// <summary>选中单位表现视图；未选中为 null。</summary>
        public GameObject SelectedView => _view;

        /// <summary>是否已有有效选中（单位与视图都非空；视图销毁后自动回到未选中）。</summary>
        public bool HasSelection => _unit != null && _view != null;

        /// <summary>选中变更事件（unit=null 表示清除；面板本轮轮询可不订阅，预留）。</summary>
        public event Action<BattleUnitState> SelectionChanged;

        /// <summary>记录选中单位（点同一个人重复选中幂等）。</summary>
        /// <param name="unit">选中的单位模拟状态；不允许为 null。</param>
        /// <param name="view">选中的表现视图；不允许为 null。</param>
        public void Select(BattleUnitState unit, GameObject view)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            if (view == null) throw new ArgumentNullException(nameof(view));
            _unit = unit;
            _view = view;
            SelectionChanged?.Invoke(unit);
        }

        /// <summary>清空选中（点空白回提示态；面板保持存活）。</summary>
        public void Clear()
        {
            _unit = null;
            _view = null;
            SelectionChanged?.Invoke(null);
        }

        /// <summary>战斗结束清理：自身置空当前实例（CombatManager OnDestroy 调用）。</summary>
        public void Dispose()
        {
            if (ReferenceEquals(_current, this)) _current = null;
            _unit = null;
            _view = null;
        }
    }
}
