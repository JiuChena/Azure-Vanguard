using System;
using UnityEngine;
using UnityEngine.Events;

namespace Core.Gear
{
    /// <summary>
    /// 公共 MonoBehaviour，将外部委托统一聚合到 Update 中执行，避免每个模块各自创建 MonoBehaviour。
    /// </summary>
    public class PublicMono : MonoBehaviour
    {
        private static PublicMono instance;

        public static PublicMono Instance
        {
            get
            {
                if (instance is null)
                {
                    GameObject obj = new GameObject("PublicMono");
                    instance = obj.AddComponent<PublicMono>();
                    DontDestroyOnLoad(obj);
                }
                return instance;
            }
        }

        private UnityAction actions;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            actions?.Invoke();
        }

        /// <summary>
        /// 向 Mono Update 注册一个每帧回调。
        /// </summary>
        /// <param name="action">要注册的回调委托</param>
        public void AddListener(UnityAction action)
        {
            actions += action;
        }

        /// <summary>
        /// 从 Mono Update 中移除一个回调。
        /// </summary>
        /// <param name="action">要移除的回调委托</param>
        public void RemoveListener(UnityAction action)
        {
            actions -= action;
        }

        /// <summary>
        /// 清空所有已注册的 Update 回调。
        /// </summary>
        public void ClearListeners()
        {
            actions = null;
        }
    }
}