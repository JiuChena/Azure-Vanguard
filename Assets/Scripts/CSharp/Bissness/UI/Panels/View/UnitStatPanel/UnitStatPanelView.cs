using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UnitStatPanel 副面板视图（MVC 的 V，嵌入 CombatPanel 的副面板，非 PanelBase）：
/// 序列化引用全部由 SetupUnitStatPanel 装配脚本接线（Inspector 拖拽等价物），
/// 未接线时 <see cref="EnsureBindings"/> 按节点路径 transform.Find 兜底（先例 CombatPanelView）。
/// 节点约定（对齐用户自搭结构）：
///   UnitStatPanel/
///     CloseBtn                                    [Button]
///     Container/UnitName                          [TMP]
///     Container/HealthText                        [TMP]
///     Container/HelathBG/HelathFillContainer/Fill [Image 填充]
///     Container/UnitAvatar                        [Image]
///     Container/Toggles/List/Item/BG              [Toggle]  ← Stat 页
///     Container/Toggles/List/Item (1)/BG          [Toggle]  ← Skills 页
///     Container/Toggles/List/Item (2)/BG          [Toggle]  ← Effect 页
///     Container/Page/StatPage / SkillsPage / EffectPage     ← 三页根
///     Container/Page/StatPage/Scroll View/Viewport/Content/Item..Item (6)
///         /Name /Number                                        ← 7 静态字段行
///     Container/Page/StatPage/Archetype/Icon + /Text           ← 职业定位（敌人 v1 无角色表行整块隐藏）
///     Container/Page/StatPage/Tags/Viewport/Content            ← 角色标签容器（CHTagView 池化）
///     Container/Page/SkillsPage/SkillsContainer/Scroll View/Viewport/Content/
///         NormalAttack|Talent|Skill|EX /SkillName /SkillDescription/Viewport/Content /Icon ← 4 技能行
///         （SkillDescription 自带 ScrollRect，TMP 文本挂在 Viewport/Content 上）
///     Container/Page/EffectPage/SkillsContainer/Scroll View/Viewport/Content ← buff 动态行容器（池化）
///         /EffectItem（行模板，当前嵌套实例 1 个）← Buff 行：Icon [Image] / EffectName [TMP]
///         /EffectDescription/Viewport/Content [TMP + 自带 ScrollRect]
///   兄弟节点（显示面板时失活）：CharacterList / Status / Pause
/// </summary>
public class UnitStatPanelView : MonoBehaviour
{
    [Header("头部")]
    [Tooltip("关闭按钮（点击关闭面板并恢复兄弟节点）")]
    [SerializeField] private Button closeBtn;

    [Tooltip("单位显示名（UnitName）")]
    [SerializeField] private TMP_Text unitNameText;

    [Tooltip("生命文本（HealthText）")]
    [SerializeField] private TMP_Text healthText;

    [Tooltip("生命填充条（HelathBG/HelathFillContainer/Fill）")]
    [SerializeField] private Image hpFill;

    [Tooltip("单位头像（UnitAvatar；敌人 v1 保留默认图）")]
    [SerializeField] private Image avatarImage;

    [Header("Tab 页")]
    [Tooltip("三页切换 Toggle（序 = 页序：Stat / Skills / Effect）")]
    [SerializeField] private Toggle[] pageToggles;

    [Tooltip("三页根节点（与 pageToggles 同序）")]
    [SerializeField] private GameObject[] pageRoots;

    [Header("StatPage 静态字段行（7 行）")]
    [Tooltip("字段标签（每行 Name 节点，序 = Item..Item(6)）")]
    [SerializeField] private TMP_Text[] statRowNames;

    [Tooltip("字段数值（每行 Number 节点，与 statRowNames 同序）")]
    [SerializeField] private TMP_Text[] statRowValues;

    [Header("StatPage 定位与标签")]
    [Tooltip("职业定位整块根节点（敌人 v1 无角色表行时整体失活）")]
    [SerializeField] private GameObject archetypeRoot;

    [Tooltip("职业定位文本（Archetype/Text）")]
    [SerializeField] private TMP_Text archetypeText;

    [Tooltip("职业定位图标（Archetype/Icon；Tag 图集 Tag_Small_* 切片）")]
    [SerializeField] private Image archetypeIcon;

    [Tooltip("角色标签容器（Tags/Viewport/Content；CHTagView 池化单元格）")]
    [SerializeField] private RectTransform tagContent;

    [Header("SkillsPage 技能行（4 行）")]
    [Tooltip("技能行绑定（序 = NormalAttack / Talent / Skill / EX）")]
    [SerializeField] private SkillRowBinding[] skillRows;

    [Header("EffectPage")]
    [Tooltip("buff 动态行容器（EffectPage 内 Scroll View 的 Content；v1 恒空）")]
    [SerializeField] private GameObject effectContentRoot;

    [Tooltip("buff 动态行模板（Content 下嵌套实例 EffectItem；SetupUnitStatPanel 拖引用，兜底按路径找）")]
    [SerializeField] private GameObject effectRowTemplate;

    [Header("面板打开时失活的兄弟节点")]
    [Tooltip("显示本面板时失活、关闭时重新激活（CombatPanel 的 CharacterList / Status / Pause）")]
    [SerializeField] private GameObject[] siblingsToHideOnOpen;

    /// <summary>关闭按钮。</summary>
    public Button CloseBtn => closeBtn;

    /// <summary>单位显示名。</summary>
    public TMP_Text UnitNameText => unitNameText;

    /// <summary>生命文本。</summary>
    public TMP_Text HealthText => healthText;

    /// <summary>生命填充条。</summary>
    public Image HpFill => hpFill;

    /// <summary>单位头像。</summary>
    public Image AvatarImage => avatarImage;

    /// <summary>三页切换 Toggle（序 = 页序）。</summary>
    public Toggle[] PageToggles => pageToggles;

    /// <summary>三页根节点（与 Toggle 同序）。</summary>
    public GameObject[] PageRoots => pageRoots;

    /// <summary>StatPage 字段标签（7 行）。</summary>
    public TMP_Text[] StatRowNames => statRowNames;

    /// <summary>StatPage 字段数值（7 行）。</summary>
    public TMP_Text[] StatRowValues => statRowValues;

    /// <summary>职业定位整块根节点（敌人 v1 隐藏）。</summary>
    public GameObject ArchetypeRoot => archetypeRoot;

    /// <summary>职业定位文本。</summary>
    public TMP_Text ArchetypeText => archetypeText;

    /// <summary>职业定位图标。</summary>
    public Image ArchetypeIcon => archetypeIcon;

    /// <summary>角色标签容器（CHTagView 池化单元格）。</summary>
    public RectTransform TagContent => tagContent;

    /// <summary>技能行绑定（4 行）。</summary>
    public SkillRowBinding[] SkillRows => skillRows;

    /// <summary>buff 动态行容器（v1 恒空）。</summary>
    public GameObject EffectContentRoot => effectContentRoot;

    /// <summary>buff 动态行模板（null 表示面板未装配模板，Control 首见告警一次后跳过）。</summary>
    public GameObject EffectRowTemplate => effectRowTemplate;

    /// <summary>面板打开时失活的兄弟节点。</summary>
    public GameObject[] SiblingsToHideOnOpen => siblingsToHideOnOpen;

    /// <summary>
    /// 按节点路径补绑未接线的序列化引用（装配脚本优先；本方法只填 null 槽位）。
    /// 技能行的 skillType 在兜底路径按行节点名推导（NormalAttack→normal / Skill→e / EX→burst / Talent→talent）。
    /// </summary>
    public void EnsureBindings()
    {
        Transform t = transform;
        if (closeBtn == null) closeBtn = FindComponent<Button>(t, "CloseBtn");
        if (unitNameText == null) unitNameText = FindComponent<TMP_Text>(t, "Container/UnitName");
        if (healthText == null) healthText = FindComponent<TMP_Text>(t, "Container/HealthText");
        if (hpFill == null) hpFill = FindComponent<Image>(t, "Container/HelathBG/HelathFillContainer/Fill");
        if (avatarImage == null) avatarImage = FindComponent<Image>(t, "Container/UnitAvatar");

        if (pageToggles == null || pageToggles.Length == 0)
        {
            pageToggles = new[]
            {
                FindComponent<Toggle>(t, "Container/Toggles/List/Item/BG"),
                FindComponent<Toggle>(t, "Container/Toggles/List/Item (1)/BG"),
                FindComponent<Toggle>(t, "Container/Toggles/List/Item (2)/BG"),
            };
        }
        if (pageRoots == null || pageRoots.Length == 0)
        {
            pageRoots = new[]
            {
                FindGameObject(t, "Container/Page/StatPage"),
                FindGameObject(t, "Container/Page/SkillsPage"),
                FindGameObject(t, "Container/Page/EffectPage"),
            };
        }

        if (statRowNames == null || statRowNames.Length == 0)
        {
            statRowNames = new TMP_Text[7];
            statRowValues = new TMP_Text[7];
            for (int i = 0; i < 7; i++)
            {
                string row = i == 0 ? "Item" : $"Item ({i})";
                string basePath = $"Container/Page/StatPage/Scroll View/Viewport/Content/{row}";
                statRowNames[i] = FindComponent<TMP_Text>(t, $"{basePath}/Name");
                statRowValues[i] = FindComponent<TMP_Text>(t, $"{basePath}/Number");
            }
        }

        if (archetypeRoot == null)
            archetypeRoot = FindGameObject(t, "Container/Page/StatPage/Archetype");
        if (archetypeText == null) archetypeText = FindComponent<TMP_Text>(t, "Container/Page/StatPage/Archetype/Text");
        if (archetypeIcon == null) archetypeIcon = FindComponent<Image>(t, "Container/Page/StatPage/Archetype/Icon");
        if (tagContent == null) tagContent = FindComponent<RectTransform>(t, "Container/Page/StatPage/Tags/Viewport/Content");

        if (skillRows == null || skillRows.Length == 0)
        {
            string[] rowNames = { "NormalAttack", "Talent", "Skill", "EX" };
            skillRows = new SkillRowBinding[rowNames.Length];
            for (int i = 0; i < rowNames.Length; i++)
            {
                string basePath = $"Container/Page/SkillsPage/SkillsContainer/Scroll View/Viewport/Content/{rowNames[i]}";
                skillRows[i] = new SkillRowBinding
                {
                    rowRoot = FindGameObject(t, basePath),
                    skillName = FindComponent<TMP_Text>(t, $"{basePath}/SkillName"),
                    skillDescription = FindComponent<TMP_Text>(t, $"{basePath}/SkillDescription/Viewport/Content"),
                    skillIcon = FindComponent<Image>(t, $"{basePath}/Icon"),
                    skillType = SkillTypeForRow(rowNames[i]),
                };
            }
        }

        if (effectContentRoot == null)
        {
            effectContentRoot = FindGameObject(t,
                "Container/Page/EffectPage/SkillsContainer/Scroll View/Viewport/Content");
        }

        // buff 行模板：缺失不抛（Control 侧首见 null 告警一次后跳过，模板装配属 SetupUnitStatPanel 接线项）
        if (effectRowTemplate == null)
        {
            effectRowTemplate = TryFindGameObject(t,
                "Container/Page/EffectPage/SkillsContainer/Scroll View/Viewport/Content/EffectItem");
        }

        if (siblingsToHideOnOpen == null || siblingsToHideOnOpen.Length == 0)
        {
            Transform parent = t.parent;
            if (parent != null)
            {
                siblingsToHideOnOpen = new[]
                {
                    FindChildGameObject(parent, "CharacterList"),
                    FindChildGameObject(parent, "Status"),
                    FindChildGameObject(parent, "Pause"),
                };
            }
        }
    }

    // 行节点名 → 技能表槽位类型（与 SkillRow.SkillTypes 约定一致；本项目命名 EX = burst/Q）
    private static string SkillTypeForRow(string rowName)
    {
        switch (rowName)
        {
            case "Talent": return Framework.Gameplay.Unit.SkillRow.SkillTypes.Talent;
            case "Skill": return Framework.Gameplay.Unit.SkillRow.SkillTypes.E;
            case "EX": return Framework.Gameplay.Unit.SkillRow.SkillTypes.Burst;
            default: return Framework.Gameplay.Unit.SkillRow.SkillTypes.Normal;
        }
    }

    // 兜底路径查找组件；节点/组件缺失抛异常（早炸好过静默漏接）
    private static T FindComponent<T>(Transform root, string path) where T : Component
    {
        Transform t = root.Find(path);
        if (t == null) throw new InvalidOperationException($"[UnitStatPanelView] 节点缺失：{path}");
        T c = t.GetComponent<T>();
        if (c == null) throw new InvalidOperationException($"[UnitStatPanelView] {path} 上无 {typeof(T).Name}");
        return c;
    }

    private static GameObject FindGameObject(Transform root, string path)
    {
        Transform t = root.Find(path);
        if (t == null) throw new InvalidOperationException($"[UnitStatPanelView] 节点缺失：{path}");
        return t.gameObject;
    }

    // 兜底路径查找节点；缺失返回 null（仅 buff 行模板这类可选装配项使用，不抛）
    private static GameObject TryFindGameObject(Transform root, string path)
    {
        Transform t = root.Find(path);
        return t == null ? null : t.gameObject;
    }

    // 直接子节点查找（兄弟节点）；缺失抛异常
    private static GameObject FindChildGameObject(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t == null) throw new InvalidOperationException($"[UnitStatPanelView] 兄弟节点缺失：{name}");
        return t.gameObject;
    }

    /// <summary>一条技能行的节点绑定（行根 + 三引用 + 槽位类型）。</summary>
    [Serializable]
    public sealed class SkillRowBinding
    {
        [Tooltip("技能行根节点（行显隐用）")]
        [SerializeField] internal GameObject rowRoot;

        [Tooltip("技能名（SkillName）")]
        [SerializeField] internal TMP_Text skillName;

        [Tooltip("技能描述（SkillDescription）")]
        [SerializeField] internal TMP_Text skillDescription;

        [Tooltip("技能图标（Icon）")]
        [SerializeField] internal Image skillIcon;

        [Tooltip("槽位类型（SkillRow.SkillTypes：normal/e/burst/talent）")]
        [SerializeField] internal string skillType;

        /// <summary>技能行根节点（行显隐用）。</summary>
        public GameObject RowRoot => rowRoot;

        /// <summary>技能名。</summary>
        public TMP_Text SkillName => skillName;

        /// <summary>技能描述。</summary>
        public TMP_Text SkillDescription => skillDescription;

        /// <summary>技能图标。</summary>
        public Image SkillIcon => skillIcon;

        /// <summary>槽位类型（SkillRow.SkillTypes）。</summary>
        public string SkillType => skillType;
    }

    /// <summary>
    /// 绑定一条 buff 动态行（Control 池化克隆行后调用）：
    /// 按模板节点路径取组件，路径缺失抛异常（模板结构是装配契约，缺即装配错误）。
    /// </summary>
    /// <param name="rowRoot">克隆出的行根节点（EffectItem 实例）。</param>
    /// <returns>该行的节点绑定。</returns>
    public EffectRowBinding BindEffectRow(GameObject rowRoot)
    {
        Transform t = rowRoot.transform;
        return new EffectRowBinding
        {
            rowRoot = rowRoot,
            buffIcon = FindComponent<Image>(t, "Icon"),
            buffName = FindComponent<TMP_Text>(t, "EffectName"),
            buffDesc = FindComponent<TMP_Text>(t, "EffectDescription/Viewport/Content"),
        };
    }

    /// <summary>一条 buff 动态行的节点绑定（行根 + 图标 + 名称 + 描述）。</summary>
    [Serializable]
    public sealed class EffectRowBinding
    {
        [Tooltip("buff 行根节点（回池/显隐用）")]
        [SerializeField] internal GameObject rowRoot;

        [Tooltip("buff 图标（Icon）")]
        [SerializeField] internal Image buffIcon;

        [Tooltip("buff 名称（EffectName）")]
        [SerializeField] internal TMP_Text buffName;

        [Tooltip("buff 描述（EffectDescription/Viewport/Content）")]
        [SerializeField] internal TMP_Text buffDesc;

        /// <summary>buff 行根节点（回池用）。</summary>
        public GameObject RowRoot => rowRoot;

        /// <summary>buff 图标。</summary>
        public Image BuffIcon => buffIcon;

        /// <summary>buff 名称。</summary>
        public TMP_Text BuffName => buffName;

        /// <summary>buff 描述。</summary>
        public TMP_Text BuffDesc => buffDesc;
    }
}
