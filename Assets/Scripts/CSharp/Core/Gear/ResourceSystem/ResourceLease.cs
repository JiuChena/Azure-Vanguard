using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Core.Gear
{
    /// <summary>
    /// 资源引用租约基类，持有对资源的独立引用计数。释放时通知 AddressableManager 递减引用。
    /// </summary>
    public abstract class ResourceLease : IDisposable
    {
        // 所属 AddressableManager 实例
        private readonly AddressableManager owner;

        // 唯一 Lease ID
        private readonly int leaseId;

        // 关联的资源 key
        private readonly string resourceKey;

        internal ResourceLease(AddressableManager owner, int leaseId, string resourceKey, ResourceScope scope)
        {
            this.owner = owner;
            this.leaseId = leaseId;
            this.resourceKey = resourceKey;
            this.Scope = scope;
        }

        // 所属作用域
        public ResourceScope Scope { get; private set; }

        // 是否已释放
        public bool IsReleased { get; private set; }

        /// <summary>
        /// 释放租约。先从作用域移除自身，再通知管理器递减引用计数。
        /// </summary>
        public void Dispose()
        {
            if (IsReleased) return;

            IsReleased = true;
            Scope?.Unregister(this);
            Scope = null;
            owner.ReleaseLease(leaseId, resourceKey);
        }

        /// <summary>
        /// 静默释放（不触发引用计数变更），仅用于关机批量清理。
        /// </summary>
        internal void ForceDisposeSilently()
        {
            if (IsReleased) return;

            IsReleased = true;
            Scope?.Unregister(this);
            Scope = null;
        }
    }
}
