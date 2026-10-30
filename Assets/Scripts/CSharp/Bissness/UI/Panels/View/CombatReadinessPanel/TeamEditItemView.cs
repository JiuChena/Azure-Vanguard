using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 编辑队伍列表项 View（MVC 的 V）：单个候选角色的引用集合（选中开关/头像/编队角标/位置数字），
/// 纯组件持有者不含逻辑。选中显隐与数字填充由 FormationPanel 负责。
/// </summary>
public class TeamEditItemView : MonoBehaviour
{
    [Header("展示")]
    //选中开关（Icon 节点上）：选中 = 编入当前队伍；挂入 MultiToggleGroup 由容器约束上限
    public Toggle toggle;
    //角色头像（Icon Image）：异步加载 Collection 变体头像
    public Image iconImg;
    //编队角标：选中时显示
    public Image markImg;
    //位置数字底图：选中时随数字一并显示
    public Image numBgImg;
    //位置数字（D_Font 数字切片）：选中时显示，sprite 按槽位切换
    public Image numImg;

    [Header("数字图")]
    //位置数字切片（D_Font_1~D_Font_8，下标 = 槽位 0~7）；Inspector 拖拽赋值
    public Sprite[] numSprites = new Sprite[8];

    [Header("运行时绑定")]
    //当前绑定的角色 ID：池化复用取放后单元格不记得数据来源，由 Control 绑定时重写；异步头像回包时校验防串图
    public string unitId;
}
