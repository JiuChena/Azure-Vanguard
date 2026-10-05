using System;
using System.Collections.Generic;
using UnityEngine;

namespace Framework.Gameplay.Item
{
    /// <summary>
    /// 物品功能标签常量：ItemTable.tags 列与代码侧共同对齐的字符串约定（同 BehaviorKey 惯例，精确匹配）。
    /// </summary>
    public static class ItemFeatureTags
    {
        /// <summary>可使用：详情页出现使用按钮。</summary>
        public const string Usable = "usable";

        /// <summary>可出售：详情页出现出售按钮，售价取 sellPrice 列。</summary>
        public const string Sell = "sell";

        /// <summary>可批量使用：在使用按钮基础上出现批量入口。</summary>
        public const string Batch = "batch";
    }

    /// <summary>
    /// 物品功能注册表：按 ItemTable.tags 标签分发工厂方法（惯例同 BetterHSMAbilitySchemeCatalog）。
    /// 未命中标签记录错误并返回 false，由调用方跳过，不中断整批功能构建。
    /// </summary>
    public static class ItemFeatureCatalog
    {
        // 标签 → 工厂；新增功能标签在此注册，同时给 ItemFeatureTags 补常量
        private static readonly Dictionary<string, Func<IItemFeature>> Factories = new()
        {
            [ItemFeatureTags.Usable] = () => new UseItemFeature(),
            [ItemFeatureTags.Sell] = () => new SellItemFeature(),
            [ItemFeatureTags.Batch] = () => new BatchUseItemFeature(),
        };

        /// <summary>
        /// 按标签创建物品功能实例。
        /// </summary>
        /// <param name="tag">功能标签，来自 ItemRow.TagList，精确匹配。</param>
        /// <param name="feature">命中的功能实例；未命中时为 null。</param>
        /// <returns>命中返回 true；未命中记录错误并返回 false。</returns>
        public static bool TryCreate(string tag, out IItemFeature feature)
        {
            if (Factories.TryGetValue(tag, out Func<IItemFeature> factory))
            {
                feature = factory();
                return true;
            }

            Debug.LogError($"[ItemFeature] 未知物品功能标签 '{tag}'，请检查 ItemTable.tags 与 ItemFeatureCatalog 注册表是否一致。");
            feature = null;
            return false;
        }
    }
}
