using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗 HUD 根视图（MVC 的 V）：持有状态区四组 Icon+Text、八个部署槽位与暂停按钮的引用。
/// 挂在 CombatPanel 预制体根节点上；引用由 Inspector 拖拽接入，EnsureBindings 仅作未接线时的兜底。
/// 槽位结构（CHOccupyN）：CHContainer{BG/Icon/Cost/Status} 为有角色卡内容，Empty 为空槽占位。
/// </summary>
public class CombatPanelView : MonoBehaviour
{
    [Header("状态区（Status 分区）")]
    // 当前点数（CostPointIcon + CostpointText）
    public TMP_Text costPointText;
    // 剩余波次（RemainingWavesIcon + RemainingWavesText）
    public TMP_Text remainingWavesText;
    // 守点剩余血量（RemainingHPAtThePointIcon + RemainingHPAtThePointText）
    public TMP_Text remainingHpText;
    // 商店出现倒计时（NextStoreTimeIcon + NextStoreTimeText）——语义为商店系统倒计时，
    // 商店未接入前不驱动（勿接 PointLedger 供给秒数，那是 cost 补给节奏，不是本栏语义）
    public TMP_Text nextStoreTimeText;

    [Header("部署槽位（CharacterList/Container/CHOccupy1~8）")]
    // 槽位根节点（CHOccupyN）
    public GameObject[] slotRoots = new GameObject[8];
    // 有角色卡内容节点（CHOccupyN/CHContainer）
    public GameObject[] slotContentNodes = new GameObject[8];
    // 空槽占位节点（CHOccupyN/Empty）
    public GameObject[] slotEmptyNodes = new GameObject[8];
    // 部署费用文本（CHOccupyN/CHContainer/Cost）
    public TMP_Text[] slotCostTexts = new TMP_Text[8];
    // 角色头像（CHOccupyN/CHContainer/Icon；CHAvatar_Collection_<unitId> 异步填充）
    public Image[] slotIconImages = new Image[8];

    [Header("拖放音效（全卡片共用）")]
    // 卡片抬起/放回切换音效（AudioClip 拖拽赋值；全部卡片同一份）
    public AudioClip cardToggleSfx;

    [Header("暂停（Pause/Button）")]
    // 暂停按钮
    public Button pauseButton;

    private void Awake()
    {
        EnsureBindings();
    }

    /// <summary>
    /// 自动绑定子节点引用，在未在 Inspector 中显式配置时作为安全兜底。
    /// </summary>
    public void EnsureBindings()
    {
        // 状态区四组文本：都在根节点 Status 分区下，节点名即引用语义
        Transform status = transform.Find("Status");
        if (status != null)
        {
            if (costPointText == null) costPointText = status.Find("CostpointText")?.GetComponent<TMP_Text>();
            if (remainingWavesText == null) remainingWavesText = status.Find("RemainingWavesText")?.GetComponent<TMP_Text>();
            if (remainingHpText == null) remainingHpText = status.Find("RemainingHPAtThePointText")?.GetComponent<TMP_Text>();
            if (nextStoreTimeText == null) nextStoreTimeText = status.Find("NextStoreTimeText")?.GetComponent<TMP_Text>();
        }

        // 暂停按钮
        if (pauseButton == null)
        {
            pauseButton = transform.Find("Pause/Button")?.GetComponent<Button>();
        }

        // 八个部署槽位：CharacterList/Container/CHOccupy1~8，逐槽兜底
        Transform container = transform.Find("CharacterList/Container");
        for (int i = 0; i < slotRoots.Length; i++)
        {
            if (slotRoots[i] == null && container != null)
            {
                slotRoots[i] = container.Find($"CHOccupy{i + 1}")?.gameObject;
            }
            if (slotRoots[i] == null) continue;

            Transform content = slotRoots[i].transform.Find("CHContainer");
            if (slotContentNodes[i] == null) slotContentNodes[i] = content?.gameObject;
            if (slotEmptyNodes[i] == null) slotEmptyNodes[i] = slotRoots[i].transform.Find("Empty")?.gameObject;
            if (slotCostTexts[i] == null && content != null) slotCostTexts[i] = content.Find("Cost")?.GetComponent<TMP_Text>();
            if (slotIconImages[i] == null && content != null) slotIconImages[i] = content.Find("Icon")?.GetComponent<Image>();
        }
    }
}
