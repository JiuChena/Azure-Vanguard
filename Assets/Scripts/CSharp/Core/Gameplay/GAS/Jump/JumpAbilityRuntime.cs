using Framework.Gameplay.Abilities.Configuration;
using Framework.Gameplay.Abilities.Input;
using UnityEngine;

namespace Framework.Gameplay.Abilities
{
    /// <summary>执行 Jump 能力的单位独占运行时逻辑。</summary>
    public sealed class JumpAbilityRuntime : AbilityRuntime
    {
        // 当前能力的静态配置。
        private readonly JumpAbilitySO _configuration;
        // 当前能力向其他能力公开的运行时数据。
        private JumpAbilityRuntimeData _runtimeData;
        // 当前能力运行时数据在拥有者上下文中的注册键。
        private const AbilityRuntimeDataType RuntimeDataType = AbilityRuntimeDataType.Jump;

        public CharacterController cc;

        /// <summary>创建 Jump 能力运行时并保存配置引用。</summary>
        /// <param name="configuration">Jump 能力配置资产。</param>
        public JumpAbilityRuntime(JumpAbilitySO configuration)
        {
            _configuration = configuration;
        }

        /// <summary>绑定能力拥有者上下文并初始化运行时依赖。</summary>
        /// <param name="ownerContext">当前单位的能力拥有者上下文。</param>
        public override void AbilityInit(AbilityOwnerContext ownerContext)
        {
            base.AbilityInit(ownerContext);
            if (ownerContext == null || ownerContext.Owner == null) return;

            // 创建并注册当前能力向其他能力公开的运行时数据。
            _runtimeData = new JumpAbilityRuntimeData();
            OwnerContext.Register(RuntimeDataType, _runtimeData);
            
            cc = ownerContext.Owner.GetComponent<CharacterController>();
        }

        /// <summary>清空能力启用前的运行时状态。</summary>
        public override void AbilityOnEnable()
        {
            _runtimeData?.Reset();
        }

        /// <summary>执行能力启动阶段。</summary>
        public override void AbilityStart()
        {
        }

        /// <summary>执行能力普通帧逻辑。</summary>
        /// <param name="deltaTime">当前帧时长，单位：秒。</param>
        public override void AbilityUpdate(float deltaTime)
        {
            if (Physics.Raycast(OwnerContext.Owner.transform.position, Vector3.down, 1.1f, 1 << LayerMask.NameToLayer("Default")))
            {
                _runtimeData.isGrounded = true;
                _runtimeData.ySpeed = 0;
            }
            else _runtimeData.isGrounded = false;
            
            if (OwnerContext.Get<InputRuntimeData>(AbilityRuntimeDataType.Input).ConsumePressed(InputButton.Jump) && _runtimeData.isGrounded)
            {
                _runtimeData.ySpeed = _configuration.JumpSpeed;
            }
            
            cc.Move(_runtimeData.ySpeed * deltaTime * Vector3.up);
            if(!_runtimeData.isGrounded) _runtimeData.ySpeed -= (_runtimeData.ySpeed >= 0 ? 9.81f : 19.62f) * deltaTime;
        }

        /// <summary>执行能力固定帧逻辑。</summary>
        /// <param name="fixedDeltaTime">当前固定帧时长，单位：秒。</param>
        public override void AbilityFixedUpdate(float fixedDeltaTime)
        {
        }

        /// <summary>执行能力延迟帧逻辑。</summary>
        /// <param name="deltaTime">当前帧时长，单位：秒。</param>
        public override void AbilityLateUpdate(float deltaTime)
        {
        }

        /// <summary>清理能力禁用时的运行时状态。</summary>
        public override void AbilityOnDisable()
        {
            _runtimeData?.Reset();
        }

        /// <summary>释放能力持有的运行时依赖。</summary>
        public override void AbilityDispose()
        {
            AbilityOnDisable();
            OwnerContext?.Unregister(RuntimeDataType, _runtimeData);
            _runtimeData = null;
            base.AbilityDispose();
        }
    }
}
