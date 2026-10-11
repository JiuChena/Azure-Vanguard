using UnityEngine;

/// <summary>
/// 养成面板左侧角色选择列表视图分区（MVC 的 V）：纯引用持有，不含逻辑。
/// 单元格为独立预制体（CultivateCHListItem，Control 的 itemPrefab 持有），
/// 经 ObjectsPool 池化取放，本类只提供内容区引用。
/// </summary>
public class CultivateCharacterBarView : MonoBehaviour
{
    [Header("角色选择列表")]
    //滚动内容区：单元格池化取放的父节点
    public RectTransform content;
}
