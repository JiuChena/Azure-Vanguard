using TMPro;
using UnityEngine;

/// <summary>
/// 角色列表页 View（MVC 的 V）：滚动列表内容区与排序下拉框的引用集合，纯组件持有者不含逻辑。
/// </summary>
public class CHListView : MonoBehaviour
{
    [Header("列表")]
    //滚动列表内容区：列表项实例化容器（GridLayoutGroup 自动排布，ContentSizeFitter 撑高）
    public RectTransform scrollContent;

    [Header("排序")]
    //排序方式下拉框：选项由 Control 运行时填充，预制体只留默认占位项
    public TMP_Dropdown sortType;
}
