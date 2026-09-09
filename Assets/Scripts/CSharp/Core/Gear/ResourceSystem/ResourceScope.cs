using System;
using System.Collections.Generic;

namespace Core.Gear
{
    /// <summary>
    /// 资源生命周期作用域。Dispose 时级联释放名下所有 Lease，用于将一组资源的生命周期绑定在一起。
    /// </summary>
    public class ResourceScope : IDisposable
    {
        // 常驻作用域，永不自动释放
        public static ResourceScope Persistent { get; } = new ResourceScope("Persistent", false);

        // 名下注册的 Lease 列表
        private readonly List<ResourceLease> leases = new List<ResourceLease>(4);

        // 是否启用自动释放（Persistent 为 false）
        private readonly bool autoDisposeEnabled;

        // 作用域名称，用于调试和日志
        public string Name { get; }

        // 是否已释放
        public bool IsDisposed { get; private set; }

        /// <param name="name">作用域名称</param>
        /// <param name="autoDisposeEnabled">是否启用 Dispose 级联释放</param>
        public ResourceScope(string name = null, bool autoDisposeEnabled = true)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "UnnamedScope" : name;
            this.autoDisposeEnabled = autoDisposeEnabled;
        }

        /// <summary>
        /// 将 Lease 注册到本作用域。已释放的作用域拒绝注册。
        /// </summary>
        internal void Register(ResourceLease lease)
        {
            if (lease == null || IsDisposed) return;
            leases.Add(lease);
        }

        /// <summary>
        /// 将 Lease 从本作用域移除。
        /// </summary>
        internal void Unregister(ResourceLease lease)
        {
            if (lease == null || leases.Count == 0) return;
            leases.Remove(lease);
        }

        /// <summary>
        /// 释放作用域，级联释放名下所有 Lease。
        /// </summary>
        public void Dispose()
        {
            if (IsDisposed || !autoDisposeEnabled) return;

            IsDisposed = true;

            // 反向遍历释放所有 Lease
            for (int i = leases.Count - 1; i >= 0; i--) leases[i]?.Dispose();

            leases.Clear();
        }
    }
}
