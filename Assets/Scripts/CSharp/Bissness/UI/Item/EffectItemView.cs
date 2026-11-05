using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Effect 页 buff 行视图（EffectItem 预制体，2026-11-01 用户拍板：Item 挂 ItemView 直接用，不走路径绑定）：
/// 图标 + 名称 + 描述，纯引用持有（Control 驱动赋值）。
/// </summary>
public class EffectItemView : MonoBehaviour
{
    // buff 图标（Icon 下的 Image；SkillEffectRow.icon 按需加载，未登记保留默认图）
    public Image buffIcon;

    // buff 名称（EffectName TMP）
    public TMP_Text buffName;

    // buff 描述（EffectDescription/Viewport/Content TMP）
    public TMP_Text buffDesc;
}
