using System.Collections.Generic;
using BehaviorEditor;
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
        //单位唯一标识；与配置表第 1 列主键对齐（如 CH0221），同时是头像图集 sprite 名与存档仓库键
        [Tooltip("单位唯一标识（string，与角色/敌人配置表第 1 列主键对齐，如 CH0221）")]
        [SerializeField] private string unitId;

        //单位显示名；配置表 name 列同源，SO 为准绳——UI 展示角色名一律读这里，表行 Name 只作表内冗余
        [Tooltip("单位显示名（与配置表 name 列同源，UI 展示以此为准）")]
        [SerializeField] private string unitName;

        [Header("行为资产表")]
        //BE 行为资产列表；clip 资产名即行为键，与叶子状态类的 BehaviorKey 常量对齐，运行时懒构建查询字典
        [Tooltip("BehaviorClip 列表；clip 资产名即行为键（与叶子状态类 BehaviorKey 常量对齐），运行时懒构建字典查询")]
        [SerializeField] private List<BehaviorClip> behaviorClips = new List<BehaviorClip>();

        /// <summary>获取单位唯一标识（如 CH0221）。</summary>
        public string UnitId => unitId;

        /// <summary>获取单位显示名；UI 展示角色名以此为准（字段名避开 Object.name 防遮蔽 CS0108）。</summary>
        public string Name => unitName;

        /// <summary>获取单位 BE 行为资产列表；clip 资产名即行为键，查询请走 <see cref="TryGetBehaviorClip"/>。</summary>
        public List<BehaviorClip> BehaviorClips => behaviorClips;

        //懒构建的 clip 名查询表；仅运行时读，不序列化
        private Dictionary<string, BehaviorClip> _behaviorClipLookup;

        /// <summary>
        /// 按行为键（clip 资产名）查询 BE 行为资产；首次查询懒构建字典，重名资产首者生效并报错。
        /// </summary>
        /// <param name="behaviorKey">行为键，即 clip 资产名；与叶子状态类 BehaviorKey 常量对齐。</param>
        /// <param name="clip">命中时返回对应资产；未命中为 null。</param>
        /// <returns>命中且资产有效返回 true。</returns>
        public bool TryGetBehaviorClip(string behaviorKey, out BehaviorClip clip)
        {
            clip = null;
            if (string.IsNullOrEmpty(behaviorKey)) return false;

            // 字典懒构建：clip 表仅在运行期首次查询时索引一次，之后只读
            if (_behaviorClipLookup == null)
            {
                _behaviorClipLookup = new Dictionary<string, BehaviorClip>(behaviorClips.Count);
                for (int i = 0; i < behaviorClips.Count; i++)
                {
                    BehaviorClip entry = behaviorClips[i];
                    // 空槽与未命名资产跳过，不进入查询表
                    if (entry == null || string.IsNullOrEmpty(entry.name)) continue;
                    if (_behaviorClipLookup.ContainsKey(entry.name))
                    {
                        Debug.LogError($"[UnitConfig] '{unitId}' 行为资产重名 '{entry.name}'，字典取首个，请检查 Inspector 列表。", this);
                        continue;
                    }

                    _behaviorClipLookup.Add(entry.name, entry);
                }
            }

            return _behaviorClipLookup.TryGetValue(behaviorKey, out clip);
        }

#if UNITY_EDITOR
        /// <summary>编辑器期重名预警：clip 名重复会让运行时字典首者生效，早暴露早修。</summary>
        protected virtual void OnValidate()
        {
            if (behaviorClips == null) return;
            var seen = new HashSet<string>();
            for (int i = 0; i < behaviorClips.Count; i++)
            {
                BehaviorClip entry = behaviorClips[i];
                if (entry == null || string.IsNullOrEmpty(entry.name) || seen.Add(entry.name)) continue;
                Debug.LogWarning($"[UnitConfig] '{unitId}' 行为资产重名 '{entry.name}'，运行时仅首个生效。", this);
            }
        }
#endif
    }
}
