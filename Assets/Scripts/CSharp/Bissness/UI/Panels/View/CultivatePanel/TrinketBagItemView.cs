using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 装备背包单格视图（TrinketBag 列表条目）：纯引用持有 + 轻交互转发，业务在 Control。
/// 点击（ClickCatcher 的 Button）= 穿戴该 id 一件；悬停 = 弹详情卡（指针事件从子节点冒泡到本根）。
/// 计数数字走 numberSheet 底图（D_Font 0-9 数字表，5 列 × 2 行）运行时切片。
/// </summary>
public class TrinketBagItemView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    //装备图标（Icon）
    public Image iconImg;

    //套装角标（SetBonusIconBG；该款式有合奏时激活）
    public GameObject setBadgeGo;

    //计数容器（Count；数字底图未配置时整体隐藏）
    public GameObject countGo;

    //十位数（Count/tens digit；数量 >= 10 才显示）
    public Image tensDigit;

    //个位数（Count/units digit）
    public Image unitsDigit;

    //整格点击（ClickCatcher 上的 Button）：点击 = 穿戴；点击音效由同行 InteractionAudioHelper 持久化接线承担
    public Button clickButton;

    //数字底图（D_Font，上排 0-4、下排 5-9；Inspector 拖入，运行时按 5×2 等分网格切片）
    public Sprite numberSheet;

    //悬停回调：进入 = 弹详情卡，移动 = 详情卡跟随鼠标，离开 = 收起（Control 重建行时直接覆写赋值，池化复用免解绑）
    [NonSerialized] public Action hoverEntered;
    [NonSerialized] public Action<Vector2> hoverMoved;
    [NonSerialized] public Action hoverExited;

    //当前绑定的装备 id：穿戴取实例 / 图标回包校验（防串写），运行时由 Control 绑定
    [System.NonSerialized] public string boundItemId;

    //底图切片缓存（底图 → 0-9 十个精灵；面板所有格子共享一份）
    private static readonly Dictionary<Sprite, Sprite[]> digitCache = new Dictionary<Sprite, Sprite[]>();

    //指针进入（ClickCatcher 子节点是射线靶，事件冒泡到本根）
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hoverEntered != null) hoverEntered();
    }

    //指针离开
    public void OnPointerExit(PointerEventData eventData)
    {
        if (hoverExited != null) hoverExited();
    }

    //悬停中移动：详情卡跟随鼠标（屏幕坐标转交给 Control 对齐）
    public void OnPointerMove(PointerEventData eventData)
    {
        if (hoverMoved != null) hoverMoved(eventData.position);
    }

    /// <summary>取数字精灵（0-9）：首次对底图切片并缓存。</summary>
    public Sprite GetDigitSprite(int digit)
    {
        if (numberSheet == null || digit < 0 || digit > 9) return null;
        if (!digitCache.TryGetValue(numberSheet, out Sprite[] digits))
        {
            digits = SliceDigits(numberSheet);
            digitCache[numberSheet] = digits;
        }
        return digits[digit];
    }

    /// <summary>把数字底图切成 0-9 十个精灵：5 列 × 2 行等分，上排 0-4、下排 5-9。</summary>
    private static Sprite[] SliceDigits(Sprite sheet)
    {
        Texture2D texture = sheet.texture;
        float cellWidth = texture.width / 5f;
        float cellHeight = texture.height / 2f;
        Sprite[] digits = new Sprite[10];
        for (int digit = 0; digit < 10; digit++)
        {
            int row = digit / 5;   //0 = 上排（0-4），1 = 下排（5-9）
            int col = digit % 5;
            //Unity 纹理坐标原点在左下：第 0 行在纹理顶部，y 取高位
            Rect rect = new Rect(col * cellWidth, texture.height - (row + 1) * cellHeight, cellWidth, cellHeight);
            digits[digit] = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), sheet.pixelsPerUnit);
        }
        return digits;
    }
}
