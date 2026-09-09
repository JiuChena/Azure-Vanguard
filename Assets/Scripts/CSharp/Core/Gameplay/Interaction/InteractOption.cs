using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Core.Gear
{
    /// <summary>
    /// 交互选项视图组件：显示图标/文本，处理选中高亮。挂载在选项预制体上，由对象池复用。
    /// </summary>
    public class InteractOption : Poolable<InteractOption>
    {
        // 选中状态 Animator 参数名，全系统统一（只读静态常量）。
        public const string SelectedAnimatorKey = "Selected";

        // 选项图标显示组件。
        [SerializeField, Tooltip("选项图标 Image")]
        private Image _icon;

        // 选项文本显示组件。
        [SerializeField, Tooltip("选项文本 TMP")]
        private TMPro.TMP_Text _text;

        // 选中/点击动画组件。
        [SerializeField, Tooltip("选中与点击动画 Animator")]
        private Animator _animator;

        // 从 Interactable 注入的交互事件；回池时必须清空防止复用残留。
        private UnityEvent _injectedEvent;

        /// <summary>
        /// 用 Interactable 的数据填充本视图，并注入其交互事件。
        /// </summary>
        /// <param name="interactable">当前选项对应的可交互物体。</param>
        public virtual void Bind(Interactable interactable)
        {
            if (_icon != null) _icon.sprite = interactable.Icon;
            if (_text != null) _text.text = interactable.PromptText;
            _injectedEvent = interactable.OnInteractEvent;
        }

        /// <summary>
        /// 执行注入的交互事件。
        /// </summary>
        public virtual void ExecuteInteract()
        {
            _injectedEvent?.Invoke();
        }

        /// <summary>
        /// 设置选中状态，驱动 Animator 高亮表现。
        /// </summary>
        /// <param name="selected">是否选中。</param>
        public virtual void SetSelected(bool selected)
        {
            if (_animator != null) _animator.SetBool(SelectedAnimatorKey, selected);
        }

        /// <summary>
        /// 播放点击反馈动画。
        /// </summary>
        public virtual void PlayClick()
        {
        }

        /// <summary>
        /// 回池清理：清空注入事件与显示数据，防止对象池复用残留。
        /// </summary>
        protected override void OnPut()
        {
            _injectedEvent = null;
            if (_icon != null) _icon.sprite = null;
            if (_text != null) _text.text = null;
        }
    }
}