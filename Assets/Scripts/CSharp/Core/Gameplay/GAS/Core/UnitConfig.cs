using BehaviorEditor;
using Core.Gear;
using UnityEngine;

namespace Framework.Gameplay.Abilities
{
    /// <summary>
    /// 单位静态配置基类：收拢一个单位全部相关资源引用（标识、BE 行为资产、数据表），
    /// 单位入队加载该资产即一次到位，卸载也跟随释放；表数据本身经 DataTableManager 缓存共享（待接入）。
    /// 不要直接创建本基类资产——角色用 <see cref="CharacterUnitConfig"/>，敌人将来另开子类继承扩展。
    /// 数值与身份（属性/技能/突破等可调平衡数据）一律进 Excel 配置表，SO 只留资产引用。
    /// </summary>
    public class UnitConfig : ScriptableObject
    {
        [Header("单位标识")]
        //单位唯一标识；与配置表第 1 列主键对齐（CH0221 = 221）
        [Tooltip("单位唯一标识（int，与角色/敌人配置表第 1 列主键对齐）")]
        [SerializeField] private int unitId;

        [Header("行为资产表")]
        //叶子状态键 → BE 行为资产；键与叶子状态类的 BehaviorKey 常量对齐
        [Tooltip("叶子状态键 → BehaviorClip；键使用叶子状态类中声明的 BehaviorKey 常量字符串")]
        [SerializeField] private SerializableDictionary<string, BehaviorClip> behaviorClips = new SerializableDictionary<string, BehaviorClip>();

        /// <summary>获取单位唯一标识。</summary>
        public int UnitId => unitId;

        /// <summary>获取叶子状态键到 BE 行为资产的查询表。</summary>
        public SerializableDictionary<string, BehaviorClip> BehaviorClips => behaviorClips;
    }
}
