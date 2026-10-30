using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 部署卡片拖拽事件源（View 支撑件）：挂在部署槽位根节点（CHOccupyN）上，
/// 实现 uGUI 拖拽三接口并转发给控制层——uGUI 拖拽事件锁定按下对象，指针划出卡片仍持续回调。
/// 运行时由 CombatPanel 在槽位上自动 AddComponent（拖拽是行为接线，不进预制体）。
/// </summary>
public class DeployCardDragSource : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    /// <summary>槽位下标（0 基，CHOccupy1 → 0）。</summary>
    public int SlotIndex { get; set; }

    /// <summary>开始拖动（参数 = 槽位下标）。</summary>
    public Action<int> DragStarted;

    /// <summary>拖动中（参数 = 槽位下标、指针屏幕坐标）。</summary>
    public Action<int, Vector2> DragMoved;

    /// <summary>松开结束（参数 = 槽位下标、指针屏幕坐标）。</summary>
    public Action<int, Vector2> DragEnded;

    void IBeginDragHandler.OnBeginDrag(PointerEventData eventData) => DragStarted?.Invoke(SlotIndex);

    void IDragHandler.OnDrag(PointerEventData eventData) => DragMoved?.Invoke(SlotIndex, eventData.position);

    void IEndDragHandler.OnEndDrag(PointerEventData eventData) => DragEnded?.Invoke(SlotIndex, eventData.position);
}
