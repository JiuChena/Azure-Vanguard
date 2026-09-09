using Framework.Gameplay.Abilities.Input;
using UnityEngine;

namespace Framework.Gameplay.Abilities.BetterHSM.Test
{
    /// <summary>保存 CH0221 BetterHSM 测试状态共同读取的数据。</summary>
    public sealed class CH0221TestStateContext
    {
        // 当前 CH0221 实体所属的 GAS 上下文。
        private readonly AbilityOwnerContext _ownerContext;

        /// <summary>
        /// 创建 CH0221 测试状态上下文。
        /// </summary>
        /// <param name="ownerContext">当前 CH0221 实体的 GAS 上下文；不允许为 null。</param>
        /// <exception cref="System.ArgumentNullException">GAS 上下文为空时抛出。</exception>
        public CH0221TestStateContext(AbilityOwnerContext ownerContext)
        {
            // 保存由 GAS 传入的当前实体唯一上下文。
            if (ownerContext == null) throw new System.ArgumentNullException(nameof(ownerContext));

            _ownerContext = ownerContext;
        }

        /// <summary>
        /// 获取当前 CH0221 实体所属的 GAS 上下文。
        /// </summary>
        public AbilityOwnerContext OwnerContext => _ownerContext;

        /// <summary>
        /// 判断当前帧是否存在有效的平面移动输入。
        /// </summary>
        /// <returns>输入能力已提供非零移动输入时返回 true；输入能力尚未挂载或尚未初始化时返回 false。</returns>
        public bool HasMoveInput()
        {
            // 测试状态图只读取已有输入 RuntimeData，不要求额外组件或业务能力。
            if (!_ownerContext.TryGet(
                    AbilityRuntimeDataType.Input,
                    out InputRuntimeData inputRuntimeData))
            {
                return false;
            }

            return inputRuntimeData.Move.sqrMagnitude > 0.0001f;
        }
    }
}
