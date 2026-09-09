using System.Collections.Generic;
using Framework.Gameplay.Abilities.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Framework.Gameplay.Abilities.Configuration
{
    /// <summary>保存输入监听能力的动作资产和通用按钮映射配置。</summary>
    [CreateAssetMenu(fileName = "InputListenerAbility", menuName = "Framework/Gameplay/Abilities/Input Listener")]
    public sealed class InputListenerAbilitySO : AbilityDefinitionSO
    {
        // 输入能力自动创建 PlayerInput 时使用的动作资产。
        [Header("输入动作")]
        [Tooltip("目标单位没有 PlayerInput 时自动创建组件并绑定的 Input Actions 资产")]
        [SerializeField] private InputActionAsset _actions;
        // 平面移动动作的直接引用。
        [Tooltip("直接选择读取 Vector2 平面移动输入的 Input Action")]
        [SerializeField] private InputActionReference _moveAction;
        // 旧版动作地图名称，仅用于已有资产迁移期间的隐藏回退。
        [Tooltip("旧版动作地图名称，仅用于兼容已有输入配置")]
        [HideInInspector]
        [FormerlySerializedAs("_actionMapName")]
        [SerializeField] private string _legacyActionMapName = "Player";
        // 旧版移动动作名称，仅用于已有资产迁移期间的隐藏回退。
        [Tooltip("旧版移动动作名称，仅用于兼容已有输入配置")]
        [HideInInspector]
        [FormerlySerializedAs("_moveActionName")]
        [SerializeField] private string _legacyMoveActionName = "Move";
        // 通用按钮与 Input Action 的映射表。
        [Header("按钮映射")]
        [Tooltip("每项将一个 Input Action 的按住、按下和松开状态写入对应通用按钮；同一按钮只应配置一次")]
        [SerializeField] private List<InputButtonBinding> _buttonBindings = new List<InputButtonBinding>
        {
            new InputButtonBinding(InputButton.Jump, "Jump"),
            new  InputButtonBinding(InputButton.Sprint, "Sprint"),
        };

        /// <summary>获取自动创建 PlayerInput 时使用的动作资产。</summary>
        public InputActionAsset Actions => _actions;
        /// <summary>获取平面移动动作的直接引用。</summary>
        public InputActionReference MoveAction => _moveAction;
        /// <summary>获取旧版动作地图名称回退值。</summary>
        internal string LegacyActionMapName => _legacyActionMapName;
        /// <summary>获取旧版移动动作名称回退值。</summary>
        internal string LegacyMoveActionName => _legacyMoveActionName;
        /// <summary>获取通用按钮与 Input Action 的配置映射表。</summary>
        public IReadOnlyList<InputButtonBinding> ButtonBindings => _buttonBindings;

        /// <summary>创建输入监听能力运行时。</summary>
        /// <returns>使用当前动作配置的输入监听运行时。</returns>
        public override AbilityRuntime CreateRuntime()
        {
            return new InputListenerAbilityRuntime(this);
        }
    }
}
