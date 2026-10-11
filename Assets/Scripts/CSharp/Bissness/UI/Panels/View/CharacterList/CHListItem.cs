using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 角色列表项 View（MVC 的 V）：单个角色卡片的引用集合（稀有度底图/图标/名字/等级），纯组件持有者不含逻辑。
/// 数据填充与点击行为由 CharacterListPanel 负责。
/// </summary>
public class CHListItem : MonoBehaviour
{
    [Header("展示")]
    //稀有度底图：按稀有度换 sprite（下标 = 稀有度-1），素材未配置时保留默认图
    public Image rarityBg;
    //角色图标：CharacterTable 暂无 icon 列，接入前保留默认图
    public Image iconImg;
    //角色名
    public TMP_Text nameText;
    //等级文本
    public TMP_Text levelText;

    [Header("星级")]
    //星级角标（Status 下 StarIcon）：稀有度 0 时隐藏整个角标
    public Image starIcon;
    //星数文本（StarIcon 下 Num）：显示稀有度数值
    public TMP_Text starNumText;

    [Header("交互")]
    //点击打开养成面板的按钮：Inspector 拖 CHIcon 上的 Button（序列化引用，不做运行时组件查找）
    public Button clickButton;

    [Header("运行时绑定")]
    //当前绑定的角色 ID（如 CH0221）：池化复用取放后单元格不记得数据来源，由 Control 绑定时重写；异步头像回包时校验防串图
    public string unitId;
}
