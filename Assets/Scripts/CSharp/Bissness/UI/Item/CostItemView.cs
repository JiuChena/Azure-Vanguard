using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 养成材料条目视图（CostItem 预制体）：图标 + Has 文本 + Reduce 按钮，纯引用持有。
/// 两种用法由 Control 驱动：
/// ① 技能书喂养格（固定 4 格引用）：Icon 点击 = 暂存 +1，Reduce 点击 = 暂存 -1
///    （Reduce 仅在暂存量 &gt; 0 时可点），Has 文本 = 当前使用量/持有总量；
/// ② 突破材料只读展示格（池化）：两按钮禁用，Has 文本 = 持有/需要。
/// </summary>
public class CostItemView : MonoBehaviour
{
    //物品图标（Icon 下的 Image；ItemRow.icon 按需加载，未登记时保留默认图）
    public Image iconImg;

    //图标本体按钮（Icon 下的 Button）：喂养格点击 = 暂存 +1；只读展示格禁用
    public Button iconButton;

    //使用量/持有量文本（Has；模板 "{0} / {1}"，{0}=使用量（或持有），{1}=持有总量（或需要））
    public TMP_Text hasText;

    //减少按钮（Reduce）：喂养格 = 暂存 -1（使用量为 0 时置灰）；只读展示格恒禁用
    public Button reduceButton;

    //当前绑定的物品 id（字符串）：异步回包校验（防串写），运行时由 Control 绑定
    [System.NonSerialized] public string itemId;
}
