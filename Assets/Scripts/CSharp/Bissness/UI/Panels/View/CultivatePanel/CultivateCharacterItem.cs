using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 养成面板左侧角色选择单元格视图（MVC 的 V）：纯引用持有，不含逻辑。
/// 独立预制体 Assets/Prefabs/UI/Character/Item/CultivateCHListItem.prefab，经 ObjectsPool 出池入池复用。
/// </summary>
public class CultivateCharacterItem : MonoBehaviour
{
    [Header("角色选择单元格")]
    //选择开关：选中即切到该角色（无 ToggleGroup，互斥由 Control 维护）
    public Toggle toggle;
    //角色头像（CharacterTable 补 icon 列前保留默认图）
    public Image icon;
    //选中标记显隐（预制体已配 markImageList）
    public ToggleMarkList markList;
    //绑定的角色 ID（如 CH0221）：池化复用取放后单元格不记得数据来源，由 Control 绑定时重写
    public string unitId;
}
