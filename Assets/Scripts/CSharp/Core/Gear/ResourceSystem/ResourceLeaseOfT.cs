using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Core.Gear
{
    /// <summary>
    /// 强类型资源租约，持有泛型资源实例引用。
    /// </summary>
    public sealed class ResourceLease<T> : ResourceLease where T : Object
    {
        // 已加载的资源实例
        public T Asset { get; }

        internal ResourceLease(AddressableManager owner, int leaseId, string resourceKey, T asset, ResourceScope scope = null)
            : base(owner, leaseId, resourceKey, scope)
        {
            Asset = asset;
        }
    }
}
