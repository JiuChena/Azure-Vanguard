using UnityEngine;

namespace Core.Gear
{
    /// <summary>
    /// UI 面板抽象基类。生命周期：EventInit（Awake）→ ComponentInit（Start）→ OnUpdate（Update）。
    /// 关闭时由 PanelManager 通过 Timer 延迟销毁，给退出动画留出播放时间。
    /// </summary>
    public abstract class PanelBase : MonoBehaviour
    {
        /// <summary>
        /// 隐藏面板后延迟销毁的时间（秒）。
        /// </summary>
        [SerializeField, Tooltip("隐藏面板后延迟销毁的时间（秒），给 Animator 退出动画留出播放时间。")]
        protected float hideDelay = 2f;

        /// <summary>
        /// 面板关闭延迟（秒）。PanelManager 读取它编排 Timer 延迟销毁。
        /// </summary>
        public float CloseDelay => hideDelay;

        /// <summary>
        /// 面板上的 Animator 组件（延迟缓存，首次访问时 GetComponent）。
        /// </summary>
        protected Animator animator
        {
            get
            {
                if (_animator == null)
                {
                    _animator = GetComponent<Animator>();
                    _animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                }
                return _animator;
            }
        }
        // 延迟缓存的 Animator 组件引用，避免每次访问都查找组件。
        private Animator _animator;

        /// <summary>
        /// 面板资源作用域,由 PanelManager 在打开时注入。面板用它加载自己的业务资源,
        /// 释放统一在 <see cref="OnDestroy"/> 处理,面板业务无需手动管理资源释放。
        /// </summary>
        public ResourceScope ResourceScope { get; internal set; }

        /// <summary>
        /// 面板在 PanelManager 中注册的名字（即 OpenPanel 的 panelName），由管理器在打开时注入。
        /// </summary>
        public string PanelName { get; internal set; }

        private void Awake() { EventInit(); }
        private void Start() { ComponentInit(); }
        private void Update() { OnUpdate(); }

        /// <summary>
        /// 面板销毁时释放资源作用域,保证名下所有 Addressable 资源随面板一起释放。
        /// 任何销毁路径(管理器关闭、场景卸载、直接 Destroy)都会在此兜住。
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (ResourceScope == null) return;

            ResourceScope.Dispose();
            ResourceScope = null;
        }

        /// <summary>
        /// 面板显示时由 PanelManager 调用。子类可覆写以播放入场动画。
        /// </summary>
        public virtual void DisplayPanel() { }

        /// <summary>
        /// 面板隐藏时由 PanelManager 调用，只负责播退出动画。销毁与资源释放
        /// 由 PanelManager 读取 <see cref="CloseDelay"/> 后通过 Timer 延迟统一编排，
        /// 面板自身不再自毁。
        /// </summary>
        public virtual void HidePanel()
        {
            // 仅触发退出动画（子类覆写时在此播放 Animator），销毁由 PanelManager 的 Timer 回调处理。
        }

        /// <summary>
        /// 立即销毁当前面板 GameObject。
        /// </summary>
        /// <param name="delay">延迟秒数，0 表示立即销毁</param>
        public void DestroyPanel(float delay = 0f)
        {
            Destroy(gameObject, delay);
        }

        /// <summary>
        /// Awake 时调用，子类在此注册事件监听（EventCenter.AddEventListener），此时子物体可能尚未创建。
        /// </summary>
        protected abstract void EventInit();

        /// <summary>
        /// Start 时调用，子类在此查找组件引用（GetComponent、Find），此时子物体已就绪。
        /// </summary>
        protected abstract void ComponentInit();

        /// <summary>
        /// Update 时调用，子类按需覆写以执行逐帧刷新。
        /// </summary>
        protected virtual void OnUpdate() { }

        /// <summary>
        /// ESC 键按下时由 PanelManager 调用。默认关闭自己（调用 PanelManager.ColsePanel）。
        /// 子类可覆写以实现自定义返回逻辑（如弹确认框、先返回上一页）；若不想关闭，
        /// 覆写中不调用 ColsePanel 即可，面板保持存活并继续留在关闭栈中。
        /// </summary>
        public virtual void OnEscapePressed()
        {
            PanelManager.Instance.ClosePanel(this);
        }
    }
}