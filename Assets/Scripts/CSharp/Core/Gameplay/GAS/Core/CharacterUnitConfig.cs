using UnityEngine;

namespace Framework.Gameplay.Abilities
{
    /// <summary>
    /// 角色单位配置：UnitConfig 基类 + 角色独有的数据表引用。
    /// dataTables 收拢角色养成共享表（CharacterTable/LevelTable/SkillTable/SkillEffectTable/UnlockNodeTable），
    /// breakthroughTable 是角色独有的突破表（敌人无养成不引用）；
    /// 表名一律取资产名（TextAsset.name），运行时按名注入 DataTableManager 缓存全队共享。
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterUnitConfig", menuName = "Framework/Gameplay/Character Unit Config")]
    public sealed class CharacterUnitConfig : UnitConfig
    {
        [Header("角色养成数据表")]
        //共享养成表 .bytes 引用；资源生命周期跟本资产走，反序列化结果由 DataTableManager 按表名缓存
        [Tooltip("养成共享表（CharacterTable/LevelTable/SkillTable/SkillEffectTable/UnlockNodeTable 的 .bytes），表名 = 资产名")]
        [SerializeField] private TextAsset[] dataTables;

        [Header("突破数据表")]
        //突破表 .bytes 引用；突破仅角色有
        [Tooltip("突破表（BreakthroughTable.bytes），角色独有")]
        [SerializeField] private TextAsset breakthroughTable;

        /// <summary>获取角色养成共享表引用列表。</summary>
        public TextAsset[] DataTables => dataTables;

        /// <summary>获取突破表引用。</summary>
        public TextAsset BreakthroughTable => breakthroughTable;
    }
}
