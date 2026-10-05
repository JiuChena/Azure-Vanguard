using UnityEngine;

namespace Framework.Gameplay.Item
{
    /// <summary>
    /// 物品功能统一入口：由 ItemFeatureCatalog 按 tags 标签创建，UI 侧构建功能按钮后调用 Execute。
    /// </summary>
    public interface IItemFeature
    {
        /// <summary>执行当前功能；实际行为在对应系统接入后实现。</summary>
        void Execute();
    }

    /// <summary>使用物品功能：网络层接入后发 UseItemRequest 由服务端结算扣量与效果。</summary>
    public sealed class UseItemFeature : IItemFeature
    {
        /// <summary>执行使用物品；当前为占位实现。</summary>
        public void Execute()
        {
            // TODO 网络层接入后改为发送 UseItemRequest，等待服务端响应后再更新本地缓存
            Debug.Log("TODO 使用物品：等待 UseItemRequest 请求通道接入。");
        }
    }

    /// <summary>出售物品功能：网络层接入后发 SellItemRequest，服务端校验 sellPrice 并结算货币。</summary>
    public sealed class SellItemFeature : IItemFeature
    {
        /// <summary>执行出售物品；当前为占位实现。</summary>
        public void Execute()
        {
            // TODO 网络层接入后改为发送 SellItemRequest，出售价以服务端 ItemTable 的 sellPrice 为准
            Debug.Log("TODO 出售物品：等待 SellItemRequest 请求通道接入。");
        }
    }

    /// <summary>批量使用物品功能：在使用物品的基础上携带数量参数一次性结算。</summary>
    public sealed class BatchUseItemFeature : IItemFeature
    {
        /// <summary>执行批量使用；当前为占位实现。</summary>
        public void Execute()
        {
            // TODO 网络层接入后改为发送 UseItemRequest(count=N)，单次请求内批量结算
            Debug.Log("TODO 批量使用物品：等待 UseItemRequest 请求通道接入并支持数量参数。");
        }
    }
}
