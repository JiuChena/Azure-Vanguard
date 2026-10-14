using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 装备背包筛选项视图（两个筛选下拉的选项行，模板预制体 TrinketTypeItem.prefab）：纯引用持有，不含逻辑。
/// 一行 = 一个可多选的筛选开关（Toggle）；filterKey = 类别键或合奏 id，出池时由 Control 写入。
/// </summary>
public class TrinketFilterItemView : MonoBehaviour
{
    //筛选开关（Toggle；可多选，全不选 = 不过滤）
    public Toggle toggle;

    //选项文案（Toggle/Text (TMP)；类别键直写英文，合奏名来自 EnsembleTable）
    public TMP_Text labelText;

    //筛选键（类别键或合奏 id）：池化复用，出池时由 Control 写入
    [System.NonSerialized] public string filterKey;

    //是否套装筛选行（true = 合奏筛选，false = 类别筛选）：出池时由 Control 写入
    [System.NonSerialized] public bool isEnsemble;
}
