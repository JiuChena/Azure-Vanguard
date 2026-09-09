using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Framework.Gameplay.Abilities.Input
{
    /// <summary>定义一个通用按钮标识与 Input Action 的配置映射。</summary>
    [Serializable]
    public sealed class InputButtonBinding
    {
        // 写入输入上下文的通用按钮标识。
        [Tooltip("读取到动作状态后写入输入上下文的通用按钮标识")]
        [SerializeField] private InputButton _button;
        // PlayerInput 动作资产中的直接动作引用。
        [Tooltip("直接选择需要读取的 Input Action，避免手写动作名称")]
        [SerializeField] private InputActionReference _action;
        // 旧版本动作名称，仅用于已有资产迁移期间的隐藏回退。
        [Tooltip("旧版动作名称，仅用于兼容已有输入配置")]
        [HideInInspector]
        [FormerlySerializedAs("_actionName")]
        [SerializeField] private string _legacyActionName;

        /// <summary>获取绑定写入的通用按钮标识。</summary>
        public InputButton Button => _button;

        /// <summary>获取 PlayerInput 动作资产中的直接动作引用。</summary>
        public InputActionReference Action => _action;

        /// <summary>获取旧版动作名称回退值。</summary>
        internal string LegacyActionName => _legacyActionName;

        /// <summary>创建供 Unity Inspector 填写的空按钮动作映射。</summary>
        public InputButtonBinding() { }

        /// <summary>创建默认输入配置使用的按钮动作映射。</summary>
        /// <param name="button">写入输入上下文的按钮标识。</param>
        /// <param name="actionName">PlayerInput 动作资产中的动作名称。</param>
        public InputButtonBinding(InputButton button, string actionName)
        {
            _button = button;
            _legacyActionName = actionName;
        }
    }
}
