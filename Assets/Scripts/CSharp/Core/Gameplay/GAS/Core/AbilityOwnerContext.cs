using System;
using System.Collections.Generic;
using UnityEngine;

namespace Framework.Gameplay.Abilities
{
    /// <summary>提供能力访问所属单位和共享运行时数据的上下文。</summary>
    public sealed class AbilityOwnerContext
    {
        // 运行时数据表：语义枚举键 → 对应能力公开的运行时数据。
        private readonly Dictionary<AbilityRuntimeDataType, IAbilityRuntimeData> _runtimeData
            = new Dictionary<AbilityRuntimeDataType, IAbilityRuntimeData>();
        // 能力所属单位对象。
        public GameObject Owner { get; }
        // 能力所属单位 Transform。
        public Transform Transform { get; }

        /// <summary>创建单位能力拥有者上下文。</summary>
        /// <param name="owner">能力所属单位对象。</param>
        public AbilityOwnerContext(GameObject owner)
        {
            Owner = owner;
            Transform = owner != null ? owner.transform : null;
        }

        /// <summary>注册一个能力公开的运行时数据对象。</summary>
        /// <param name="type">数据在上下文中的语义键。</param>
        /// <param name="data">实现运行时数据接口的数据对象；不允许为 null。</param>
        public void Register<T>(AbilityRuntimeDataType type, T data)
            where T : class, IAbilityRuntimeData
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (_runtimeData.TryGetValue(type, out IAbilityRuntimeData existing))
            {
                if (ReferenceEquals(existing, data)) return;
                throw new InvalidOperationException($"Ability runtime data key {type} is already registered.");
            }

            _runtimeData.Add(type, data);
        }

        /// <summary>注销指定语义键对应的运行时数据对象。</summary>
        /// <param name="type">需要注销的数据语义键。</param>
        /// <returns>成功移除数据时返回 true。</returns>
        public bool Unregister(AbilityRuntimeDataType type)
        {
            return _runtimeData.Remove(type);
        }

        /// <summary>仅当指定实例仍是当前注册对象时注销运行时数据。</summary>
        /// <param name="type">需要注销的数据语义键。</param>
        /// <param name="expectedData">期望被注销的数据实例。</param>
        /// <returns>实例匹配并成功移除时返回 true。</returns>
        public bool Unregister<T>(AbilityRuntimeDataType type, T expectedData)
            where T : class, IAbilityRuntimeData
        {
            if (!_runtimeData.TryGetValue(type, out IAbilityRuntimeData current)) return false;
            if (!ReferenceEquals(current, expectedData)) return false;
            return _runtimeData.Remove(type);
        }

        /// <summary>安全获取指定语义键下的强类型运行时数据。</summary>
        /// <param name="type">需要访问的数据语义键。</param>
        /// <param name="data">找到且类型匹配时返回数据实例。</param>
        /// <returns>找到并成功转换时返回 true。</returns>
        public bool TryGet<T>(AbilityRuntimeDataType type, out T data)
            where T : class, IAbilityRuntimeData
        {
            if (_runtimeData.TryGetValue(type, out IAbilityRuntimeData value) && value is T typed)
            {
                data = typed;
                return true;
            }

            data = null;
            return false;
        }

        /// <summary>强制获取指定语义键下的强类型运行时数据。</summary>
        /// <param name="type">需要访问的数据语义键。</param>
        /// <returns>找到且类型匹配的数据实例。</returns>
        /// <exception cref="InvalidOperationException">数据不存在或注册类型不匹配时抛出。</exception>
        public T Get<T>(AbilityRuntimeDataType type)
            where T : class, IAbilityRuntimeData
        {
            if (TryGet(type, out T data)) return data;
            if (!_runtimeData.TryGetValue(type, out IAbilityRuntimeData value))
                throw new InvalidOperationException($"Ability runtime data key {type} is not registered.");
            throw new InvalidOperationException(
                $"Ability runtime data key {type} contains {value.GetType().Name}, not {typeof(T).Name}.");
        }

        /// <summary>重置指定语义键对应的运行时数据对象。</summary>
        /// <param name="type">需要重置的数据语义键。</param>
        /// <returns>找到数据并执行重置时返回 true。</returns>
        public bool Reset(AbilityRuntimeDataType type)
        {
            if (!_runtimeData.TryGetValue(type, out IAbilityRuntimeData data)) return false;
            data.Reset();
            return true;
        }

        /// <summary>重置上下文中全部已注册运行时数据的状态。</summary>
        public void ResetAll()
        {
            foreach (IAbilityRuntimeData data in _runtimeData.Values) data.Reset();
        }
    }
}
