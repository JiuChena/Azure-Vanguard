using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 装备背包分区视图（TrinketPage/TrinketBag）：纯引用持有，不含逻辑。
/// 列表内容与筛选行为由 Control 绑定；筛选下拉 = 按钮 + 选项模板容器结构（无 Dropdown 组件），
/// 点击按钮开合选项列表（互斥展开），选项行 Toggle 可多选。
/// </summary>
public class TrinketBagView : MonoBehaviour
{
    [Header("背包列表")]
    //背包条目内容区（Scroll View/Viewport/Content，GridLayout）
    public RectTransform content;

    [Header("类别筛选")]
    //筛选按钮（DropDown_TrinketType，点击开合选项列表）
    public Button categoryButton;
    //选项列表容器（按钮下 Scroll View，激活 = 展开）
    public GameObject categoryList;
    //选项挂载点（…/Scroll View/Viewport/Content）
    public RectTransform categoryOptionContent;

    [Header("套装筛选")]
    //筛选按钮（DropDown_TrinketSetBonusType，点击开合选项列表）
    public Button ensembleButton;
    //选项列表容器
    public GameObject ensembleList;
    //选项挂载点
    public RectTransform ensembleOptionContent;

    [Header("详情卡")]
    //详情卡根节点（Msg，点背包条目弹出）
    public GameObject msgGo;
    //鼠标对齐标记（Msg/CurosrPoint，用户预制体已摆位）：对齐到鼠标位置，详情卡主体呈现在鼠标右侧
    public RectTransform msgCursorPoint;
    //详情装备图标（Msg/Icon）
    public Image msgIcon;
    //详情合奏名（Msg/SetBonusName；未配合奏写 "-"）
    public TMP_Text msgSetNameText;
    //详情主词条（Msg/Property/Main Stat/Text (TMP)）
    public TMP_Text msgMainStatText;
    //详情副词条 ×3（Msg/Property/Main Stat (1)/(2)/(3) 的 Text (TMP)）
    public TMP_Text[] msgSubStatTexts;

    [Header("关闭")]
    //背包关闭按钮（Top/CloseBtn；收起选项列表与详情卡并隐藏整个背包分区）
    public Button closeBtn;
}
