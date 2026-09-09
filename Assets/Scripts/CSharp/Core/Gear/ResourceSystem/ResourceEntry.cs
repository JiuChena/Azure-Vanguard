using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace Core.Gear
{
    /// <summary>
    /// 内部资源条目，封装 Addressable Handle 与引用计数。
    /// </summary>
    internal sealed class ResourceEntry
    {
        // 完整 resourceKey
        public string Key { get; }

        // Addressables 异步操作 Handle
        public AsyncOperationHandle Handle { get; }

        // 加载 Task，用于 await 等待完成
        public Task Task { get; }

        // 资源实例（Handle 有效时）
        public Object Asset => Handle.IsValid() ? Handle.Result as Object : null;

        // 加载是否已完成
        public bool IsLoaded => Handle.IsValid() && Handle.Status == AsyncOperationStatus.Succeeded;

        // 加载是否失败
        public bool LoadFailed => Handle.IsValid() && Handle.Status == AsyncOperationStatus.Failed;

        // 当前引用计数
        public int ReferenceCount;

        public ResourceEntry(string key, AsyncOperationHandle handle)
        {
            Key = key;
            Handle = handle;
            Task = handle.Task;
            ReferenceCount = 0;
        }

        /// <summary>
        /// 释放底层 Addressable Handle。
        /// </summary>
        public void ReleaseHandle()
        {
            if (Handle.IsValid()) Addressables.Release(Handle);
        }
    }
}
