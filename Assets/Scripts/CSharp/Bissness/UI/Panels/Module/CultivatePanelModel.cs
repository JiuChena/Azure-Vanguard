using Framework.Gameplay.Player;
using Framework.Gameplay.Progression;
using Framework.Gameplay.Unit;
using UnityEngine;

/// <summary>
/// 养成面板展示数据模型（MVC 的 M）：Control 选中角色后填充，View 渲染只读本模型。
/// 数据源 = 角色仓库条目 × CharacterTable 静态行 × 养成服务属性结算。
/// </summary>
public class CultivatePanelModel
{
    //当前查看的角色 ID（null = 未选中）
    public string UnitId;

    //角色显示名：以 UnitConfig（SO）的 Name 为准绳，加载失败回退表行 Name；null = 未解析
    public string UnitName;

    //角色表行（静态：名字/稀有度/职业）
    public CharacterRow Row;

    //仓库养成条目（等级/经验/突破/已解锁节点），每次刷新从仓库重取
    public CharacterEntry Entry;

    //属性结算结果（CalculateStatsAsync 产出）
    public CharacterStats Stats;

    //是否已装载可用数据（表行与仓库条目齐备）
    public bool HasData => Row != null && Entry != null;

    //当前等级上限（按突破阶段查表；未就绪时按初始上限兜底）
    public int LevelCap => Entry == null
        ? CharacterProgressionManager.InitialLevelCap
        : CharacterProgressionManager.Instance.GetLevelCap(Entry.breakStage);

    //升到下一级所需经验（升级/突破结算共用公式）
    public int ExpToNext => Entry == null ? 0 : CharacterProgressionManager.ExpToNext(Entry.level);

    //经验进度 0~1（本级已累积 / 本级所需）
    public float ExpProgress => ExpToNext > 0 ? Mathf.Clamp01((float)Entry.exp / ExpToNext) : 0f;

    //武器经验进度 0~1（本级已累积 / 本级所需；武器与角色共用经验曲线与上限口径）
    public float WeaponExpProgress
    {
        get
        {
            if (Entry == null) return 0f;
            int level = Mathf.Max(1, Entry.weaponLevel);
            int need = CharacterProgressionManager.ExpToNext(level);
            return need > 0 ? Mathf.Clamp01((float)Entry.weaponExp / need) : 0f;
        }
    }
}
