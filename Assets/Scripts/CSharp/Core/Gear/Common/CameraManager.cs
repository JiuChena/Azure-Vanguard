using System.Collections.Generic;
using UnityEngine;

namespace Core.Gear
{
    /// <summary>
    /// 全局相机管理器：以 CameraType 为键统一持有相机，支持 Inspector 预配置与运行时动态注册注销。
    /// Inspector 双列表仅作 Awake 导入的种子配置，运行时注册注销不回写序列化数据。
    /// </summary>
    public class CameraManager : MonoBehaviour
    {
        public static CameraManager Instance { get; private set; }

        [Header("引用")]
        [Tooltip("Inspector 预配置的相机键列表，与 inspectorValues 按下标一一对应；Awake 时全量导入运行时表。")]
        [SerializeField] private List<CameraType> inspectorKeys = new List<CameraType>();

        [Tooltip("Inspector 预配置的相机列表，与 inspectorKeys 按下标一一对应。")]
        [SerializeField] private List<Camera> inspectorValues = new List<Camera>();

        // 运行时相机表：CameraType → Camera，Inspector 导入与动态注册的统一事实源。
        private readonly Dictionary<CameraType, Camera> cameras = new Dictionary<CameraType, Camera>();

        #region 生命周期

        private void Awake()
        {
            // 场景中已有实例时自毁重复体，保证全局单例。
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 导入 Inspector 预配置为运行时初始表；长度不齐按较短一侧，重复键后者覆盖。
            int count = Mathf.Min(inspectorKeys.Count, inspectorValues.Count);
            if (inspectorKeys.Count != inspectorValues.Count)
                Debug.LogError($"[CameraManager] 键列表({inspectorKeys.Count})与值列表({inspectorValues.Count})数量不一致，请检查 Inspector 配置。");
            for (int i = 0; i < count; i++)
            {
                if (inspectorValues[i] == null)
                {
                    Debug.LogError($"[CameraManager] Inspector 配置的相机键 {inspectorKeys[i]} 值为空，已跳过。");
                    continue;
                }
                if (cameras.ContainsKey(inspectorKeys[i]))
                    Debug.LogWarning($"[CameraManager] Inspector 配置存在重复键 {inspectorKeys[i]}，后者覆盖前者。");
                cameras[inspectorKeys[i]] = inspectorValues[i];
            }
        }

        private void OnDestroy()
        {
            // 实例销毁时清空静态引用，防止悬挂访问。
            if (Instance == this) Instance = null;
        }

        #endregion

        #region 注册注销

        /// <summary>
        /// 注册或更新相机；键已注册且指向不同相机时覆盖并告警。
        /// </summary>
        /// <param name="type">相机键。</param>
        /// <param name="camera">待注册相机，为空时拒绝并记录错误。</param>
        public void Register(CameraType type, Camera camera)
        {
            if (camera == null)
            {
                Debug.LogError($"[CameraManager] 注册相机参数无效：type={type}，camera 为空。");
                return;
            }

            // 覆盖已有键时提示，防止静默顶掉使用中的相机。
            if (cameras.TryGetValue(type, out Camera old) && old != camera)
                Debug.LogWarning($"[CameraManager] 相机键 {type} 已注册，旧相机将被覆盖。");
            cameras[type] = camera;
        }

        /// <summary>
        /// 注销相机；键未注册时视为操作失败。
        /// </summary>
        /// <param name="type">相机键。</param>
        /// <returns>注销成功返回 true；键未注册返回 false。</returns>
        public bool Unregister(CameraType type)
        {
            if (!cameras.Remove(type))
            {
                Debug.LogWarning($"[CameraManager] 注销失败：相机键 {type} 未注册。");
                return false;
            }
            return true;
        }

        #endregion

        #region 查询

        /// <summary>
        /// 按键获取相机；键未注册或相机已销毁时记录错误并返回 null。
        /// </summary>
        /// <param name="type">相机键。</param>
        /// <returns>对应相机；获取失败返回 null。</returns>
        public Camera Get(CameraType type)
        {
            if (TryGet(type, out Camera camera)) return camera;
            Debug.LogError($"[CameraManager] 未获取到相机：type={type}。");
            return null;
        }

        /// <summary>
        /// 按键尝试获取相机；命中键但相机已被销毁时清理脏条目并按未注册处理。
        /// </summary>
        /// <param name="type">相机键。</param>
        /// <param name="camera">命中时返回对应相机，失败返回 null。</param>
        /// <returns>键存在且相机有效时返回 true。</returns>
        public bool TryGet(CameraType type, out Camera camera)
        {
            if (cameras.TryGetValue(type, out camera) && camera != null) return true;

            // 命中键但相机已销毁（如随场景卸载）：清理脏条目，按未注册处理。
            if (cameras.Remove(type))
                Debug.LogWarning($"[CameraManager] 相机键 {type} 指向的相机已被销毁，脏条目已移除。");
            camera = null;
            return false;
        }

        /// <summary>
        /// 判断键是否已注册且相机有效。
        /// </summary>
        /// <param name="type">相机键。</param>
        /// <returns>键存在且相机有效时返回 true。</returns>
        public bool ContainsKey(CameraType type)
        {
            return TryGet(type, out _);
        }

        #endregion
    }
}
