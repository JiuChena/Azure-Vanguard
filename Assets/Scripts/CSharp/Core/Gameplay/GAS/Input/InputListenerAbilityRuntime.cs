using System.Collections.Generic;
using Framework.Gameplay.Abilities.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Framework.Gameplay.Abilities.Input
{
    /// <summary>每帧读取 PlayerInput 并写入单位输入黑板。</summary>
    public sealed class InputListenerAbilityRuntime : AbilityRuntime
    {
        // 输入动作静态配置表。
        private readonly InputListenerAbilitySO _configuration;
        // 当前单位的输入来源组件。
        private PlayerInput _playerInput;
        // 当前单位独占的输入运行时数据。
        private InputRuntimeData _inputRuntimeData;
        // 缓存的平面移动动作。
        private InputAction _moveAction;
        // 与通用按钮标识一一对应的缓存动作。
        private InputAction[] _buttonActions;
        // 与缓存动作一一对应的通用按钮标识。
        private InputButton[] _buttons;

        /// <summary>创建输入监听运行时并保存配置表引用。</summary>
        /// <param name="configuration">输入动作配置表；允许为 null 并使用默认动作名称。</param>
        public InputListenerAbilityRuntime(InputListenerAbilitySO configuration)
        {
            _configuration = configuration;
        }

        /// <summary>创建输入运行时数据并缓存 PlayerInput 动作引用。</summary>
        /// <param name="ownerContext">当前单位的能力拥有者上下文。</param>
        public override void AbilityInit(AbilityOwnerContext ownerContext)
        {
            base.AbilityInit(ownerContext);
            if (ownerContext == null || ownerContext.Owner == null) return;

            // 创建并注册输入能力向其他能力公开的运行时数据。
            _inputRuntimeData = new InputRuntimeData();
            OwnerContext.Register(AbilityRuntimeDataType.Input, _inputRuntimeData);

            // 获取或创建输入来源组件，并在缺失动作资产时由配置表补齐。
            _playerInput = EnsurePlayerInput(ownerContext.Owner);

            // 初始化阶段缓存动作引用，运行帧只读取缓存。
            CacheActions(_playerInput);
        }

        /// <summary>读取当前帧输入并更新输入黑板。</summary>
        /// <param name="deltaTime">当前帧时长，单位：秒；输入采集不依赖该值。</param>
        public override void AbilityUpdate(float deltaTime)
        {
            if (_inputRuntimeData == null) return;

            // 从缓存动作读取当前帧值，缺失动作按中性输入处理。
            Vector2 move = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
            _inputRuntimeData.WriteMove(move);

            // 按绑定顺序写入按钮持续状态，输入上下文统一记录可消费边沿。
            if (_buttonActions == null || _buttons == null) return;
            for (int index = 0; index < _buttonActions.Length; index++)
            {
                InputAction buttonAction = _buttonActions[index];
                _inputRuntimeData.WriteButtonState(_buttons[index], buttonAction != null && buttonAction.IsPressed());
            }
        }

        /// <summary>清空输入状态，避免能力禁用后残留按键边沿。</summary>
        public override void AbilityOnDisable()
        {
            _inputRuntimeData?.Reset();
        }

        /// <summary>重新启用输入来源并清空上一轮输入状态。</summary>
        public override void AbilityOnEnable()
        {
            _inputRuntimeData?.Reset();
            if (_playerInput == null) return;
            _playerInput.ActivateInput();
            CacheActions(_playerInput);
        }

        /// <summary>释放输入动作和黑板引用。</summary>
        public override void AbilityDispose()
        {
            AbilityOnDisable();
            OwnerContext?.Unregister(AbilityRuntimeDataType.Input, _inputRuntimeData);
            _moveAction = null;
            _buttonActions = null;
            _buttons = null;
            _playerInput = null;
            _inputRuntimeData = null;
            base.AbilityDispose();
        }

        /// <summary>从 PlayerInput 的动作资产缓存输入动作。</summary>
        private void CacheActions(PlayerInput playerInput)
        {
            if (playerInput == null || playerInput.actions == null)
            {
                _moveAction = null;
                _buttonActions = null;
                _buttons = null;
                return;
            }

            // 旧配置仍按动作地图名称作为直接引用缺失时的回退。
            string actionMapName = _configuration != null
                ? _configuration.LegacyActionMapName
                : "Player";
            InputActionMap actionMap = string.IsNullOrWhiteSpace(actionMapName)
                ? null
                : playerInput.actions.FindActionMap(actionMapName, false);
            _moveAction = ResolveAction(
                _configuration != null ? _configuration.MoveAction : null,
                playerInput.actions,
                actionMap,
                _configuration != null ? _configuration.LegacyMoveActionName : "Move");
            CacheButtonActions(playerInput.actions, actionMap);
        }

        /// <summary>按配置顺序缓存通用按钮对应的输入动作。</summary>
        /// <param name="actions">当前 PlayerInput 使用的完整动作资产；为空时清空缓存。</param>
        /// <param name="actionMap">优先查找动作的地图；为空时直接查找完整资产。</param>
        private void CacheButtonActions(InputActionAsset actions, InputActionMap actionMap)
        {
            IReadOnlyList<InputButtonBinding> bindings = _configuration != null ? _configuration.ButtonBindings : null;
            if (bindings == null)
            {
                // 未提供配置表时保持 Jump 默认动作名称的回退行为。
                _buttons = new[] { InputButton.Jump };
                _buttonActions = new[]
                {
                    FindAction(actions, actionMap, "Jump")
                };
                return;
            }

            int bindingCount = bindings.Count;
            _buttonActions = new InputAction[bindingCount];
            _buttons = new InputButton[bindingCount];
            ulong configuredButtons = 0UL;

            // 初始化阶段验证并缓存每个配置项，运行帧不再执行动作查找或集合分配。
            for (int index = 0; index < bindingCount; index++)
            {
                InputButtonBinding binding = bindings[index];
                if (binding == null) continue;
                if (!InputRuntimeData.TryGetButtonMask(binding.Button, out ulong buttonMask)) continue;
                if ((configuredButtons & buttonMask) != 0UL) continue;

                configuredButtons |= buttonMask;
                _buttons[index] = binding.Button;
                _buttonActions[index] = ResolveAction(
                    binding.Action,
                    actions,
                    actionMap,
                    binding.LegacyActionName);
            }
        }

        /// <summary>从直接引用或旧版名称解析并返回当前 PlayerInput 使用的动作。</summary>
        /// <param name="actionReference">Input Actions 资产中的直接动作引用；为空时使用旧版名称。</param>
        /// <param name="actions">当前 PlayerInput 使用的动作资产；为空时返回 null。</param>
        /// <param name="fallbackMap">旧版动作地图回退对象；为空时查找整个动作资产。</param>
        /// <param name="fallbackActionName">旧版动作名称；为空时返回 null。</param>
        /// <returns>当前 PlayerInput 对应的缓存动作；无法解析时返回 null。</returns>
        private InputAction ResolveAction(
            InputActionReference actionReference,
            InputActionAsset actions,
            InputActionMap fallbackMap,
            string fallbackActionName)
        {
            if (actionReference != null && actionReference.action != null)
            {
                InputAction referencedAction = actionReference.action;
                string mapName = referencedAction.actionMap != null
                    ? referencedAction.actionMap.name
                    : string.Empty;
                InputActionMap referencedMap = string.IsNullOrWhiteSpace(mapName)
                    ? null
                    : actions.FindActionMap(mapName, false);
                return FindAction(
                    actions,
                    referencedMap,
                    referencedAction.name);
            }

            return FindAction(actions, fallbackMap, fallbackActionName);
        }

        /// <summary>确保单位拥有可供本能力读取的 PlayerInput 组件。</summary>
        /// <param name="owner">能力所属单位对象；为 null 时返回 null。</param>
        /// <returns>已有或由配置资产创建完成的 PlayerInput；缺少动作资产时返回 null。</returns>
        private PlayerInput EnsurePlayerInput(GameObject owner)
        {
            if (owner == null) return null;

            // 已有组件优先保留业务层配置，仅在它缺少动作资产时应用能力配置。
            PlayerInput playerInput = owner.GetComponent<PlayerInput>();
            if (!Application.isPlaying) return playerInput;
            if (playerInput == null)
            {
                if (_configuration == null || _configuration.Actions == null) return null;
                playerInput = owner.AddComponent<PlayerInput>();
            }

            // 新建或未配置的 PlayerInput 由输入 SO 提供动作资产和默认动作地图。
            if (playerInput.actions == null && _configuration != null)
                playerInput.actions = _configuration.Actions;
            if (playerInput.actions == null) return null;

            string actionMapName = GetConfiguredActionMapName();
            if (!string.IsNullOrWhiteSpace(actionMapName)) playerInput.defaultActionMap = actionMapName;
            if (!playerInput.enabled) playerInput.enabled = true;
            playerInput.ActivateInput();
            return playerInput;
        }

        /// <summary>获取直接动作引用所属的动作地图，缺少引用时回退到旧配置。</summary>
        /// <returns>PlayerInput 应启用的动作地图名称；没有可用配置时返回空字符串。</returns>
        private string GetConfiguredActionMapName()
        {
            if (_configuration == null) return string.Empty;

            InputAction moveAction = _configuration.MoveAction != null
                ? _configuration.MoveAction.action
                : null;
            if (moveAction != null && moveAction.actionMap != null)
                return moveAction.actionMap.name;

            IReadOnlyList<InputButtonBinding> bindings = _configuration.ButtonBindings;
            if (bindings != null)
            {
                for (int index = 0; index < bindings.Count; index++)
                {
                    InputButtonBinding binding = bindings[index];
                    InputAction buttonAction = binding != null && binding.Action != null
                        ? binding.Action.action
                        : null;
                    if (buttonAction != null && buttonAction.actionMap != null)
                        return buttonAction.actionMap.name;
                }
            }

            return _configuration.LegacyActionMapName;
        }

        /// <summary>从指定动作地图或动作资产查找动作。</summary>
        /// <param name="actions">当前 PlayerInput 使用的完整动作资产；为空时返回 null。</param>
        /// <param name="actionMap">优先查找的动作地图；为空时直接查找动作资产。</param>
        /// <param name="actionName">动作名称；为空时返回 null。</param>
        /// <returns>找到的输入动作；未找到时返回 null。</returns>
        private InputAction FindAction(
            InputActionAsset actions,
            InputActionMap actionMap,
            string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName)) return null;
            if (actionMap != null) return actionMap.FindAction(actionName, false);
            return actions != null ? actions.FindAction(actionName, false) : null;
        }
    }
}
