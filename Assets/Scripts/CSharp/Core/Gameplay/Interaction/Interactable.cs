using UnityEngine;
using UnityEngine.Events;

namespace Core.Gear
{
    /// <summary>
    /// 可交互物体组件：声明交互选项数据与交互行为。子类可重写扩展。
    /// </summary>
    public class Interactable : MonoBehaviour
    {
        // 交互提示文本，由 Inspector 或代码注入。
        [SerializeField, Tooltip("交互提示文本，如 F - 打开宝箱")]
        private string _promptText;

        // 交互选项图标。
        [SerializeField, Tooltip("选项图标")]
        private Sprite _icon;

        // 交互执行回调（UnityEvent，Inspector 配置或运行时 AddListener 注入）。
        [SerializeField, Tooltip("交互执行时触发的回调")]
        private UnityEvent _onInteract;

        /// <summary>获取运行时稳定 GUID（GetInstanceID，同物体多次扫描键稳定）。</summary>
        public int Guid => GetInstanceID();

        /// <summary>获取交互提示文本。</summary>
        public string PromptText => _promptText;

        /// <summary>获取交互选项图标。</summary>
        public Sprite Icon => _icon;

        /// <summary>获取交互执行回调，供 InteractOption 注入。</summary>
        public UnityEvent OnInteractEvent => _onInteract;

        /// <summary>
        /// 是否允许玩家交互。子类可覆写做状态/条件检测。
        /// </summary>
        /// <param name="interactor">发起交互的玩家对象。</param>
        /// <returns>允许交互时返回 true。</returns>
        public virtual bool CanInteract(GameObject interactor) => true;

        /// <summary>
        /// 执行交互：触发注入的 UnityEvent 并调用子类扩展点。
        /// </summary>
        /// <param name="interactor">发起交互的玩家对象。</param>
        public virtual void Interact(GameObject interactor)
        {
            _onInteract?.Invoke();
            OnInteracted(interactor);
        }

        /// <summary>交互后的子类扩展点，基类为空实现。</summary>
        protected virtual void OnInteracted(GameObject interactor)
        {
        }
    }
}