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
    /// 资源调试信息快照。
    /// </summary>
    public readonly struct ResourceDebugInfo
    {
        // 资源 key（FullTypeName::key 格式）
        public readonly string Key;

        // 已加载的资源实例
        public readonly Object Asset;

        // 当前引用计数
        public readonly int ReferenceCount;

        // 异步操作状态
        public readonly AsyncOperationStatus Status;

        // 加载是否失败
        public readonly bool LoadFailed;

        public ResourceDebugInfo(string key, Object asset, int referenceCount, AsyncOperationStatus status, bool loadFailed)
        {
            Key = key;
            Asset = asset;
            ReferenceCount = referenceCount;
            Status = status;
            LoadFailed = loadFailed;
        }
    }
}
