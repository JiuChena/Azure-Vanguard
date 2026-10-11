using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Core.Gear
{
    /// <summary>
    /// UI 图标运行时库：按地址名加载单图 Sprite（png 的 Sprite 是子资源，走「名[名]」子地址直载）。
    /// 常驻不释放（与 UnitConfigLibrary 同口径）：图标小且反复使用，Lease 挂 Persistent 作用域。
    /// 缓存命中直接短路；并发加载经 pending 表去重；加载失败的名字不入缓存，下次自动重试并返回 null（调用方保留默认图）。
    /// 角色头像地址约定：CHAvatar_&lt;变体&gt;_&lt;unitId&gt;（如 CHAvatar_Small_CH0221），变体分组见 Addressables 组 Sprite-CharacterAvatar-*。
    /// </summary>
    public static class IconLibrary
    {
        //头像变体（地址段）：小头像（列表/培养左列）
        public const string VariantSmall = "Small";

        //头像变体：收藏立绘
        public const string VariantCollection = "Collection";

        //头像变体：好感立绘
        public const string VariantFavor = "Favor";

        //头像变体：玩家名片头像
        public const string VariantPlayerAvatar = "PlayerAvatar";

        // spriteName → 已加载 Sprite（失败不缓存）
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        // spriteName → 进行中的加载任务（并发去重）
        private static readonly Dictionary<string, Task<Sprite>> pending = new Dictionary<string, Task<Sprite>>();

        /// <summary>
        /// 组角色头像地址键：CHAvatar_&lt;变体&gt;_&lt;unitId&gt;。
        /// </summary>
        /// <param name="variant">头像变体（<see cref="VariantSmall"/> 等）。</param>
        /// <param name="unitId">单位 ID（如 CH0221）。</param>
        /// <returns>地址键（如 CHAvatar_Small_CH0221）。</returns>
        public static string CharacterAvatarKey(string variant, string unitId)
        {
            return $"CHAvatar_{variant}_{unitId}";
        }

        /// <summary>
        /// 获取角色头像（地址键 = <see cref="CharacterAvatarKey"/>），并发去重与缓存口径同 <see cref="GetAsync"/>。
        /// </summary>
        public static Task<Sprite> GetCharacterAvatarAsync(string variant, string unitId)
        {
            return GetAsync(CharacterAvatarKey(variant, unitId));
        }

        /// <summary>
        /// 获取图标 Sprite；缓存命中直接返回，否则从 Addressables 加载。
        /// </summary>
        /// <param name="spriteName">地址名（= png 文件名 = Sprite 子资源名，如 CHAvatar_Small_CH0221）。</param>
        /// <returns>Sprite；名字非法或未登记时返回 null（调用方保留默认图）。</returns>
        public static async Task<Sprite> GetAsync(string spriteName)
        {
            if (string.IsNullOrWhiteSpace(spriteName))
            {
                Debug.LogError("[IconLibrary] 获取失败：spriteName 为空");
                return null;
            }

            if (cache.TryGetValue(spriteName, out Sprite hit)) return hit;
            if (pending.TryGetValue(spriteName, out Task<Sprite> inflight)) return await inflight;

            Task<Sprite> task = LoadAsync(spriteName);
            pending[spriteName] = task;
            Sprite sprite = await task;
            pending.Remove(spriteName);

            if (sprite != null) cache[spriteName] = sprite;
            return sprite;
        }

        /// <summary>
        /// 仅查缓存，不触发加载；同帧重复请求等同步场景用。
        /// </summary>
        /// <param name="spriteName">地址名。</param>
        /// <param name="sprite">已缓存的 Sprite；未命中为 null。</param>
        /// <returns>命中返回 true。</returns>
        public static bool TryGetCached(string spriteName, out Sprite sprite)
        {
            return cache.TryGetValue(spriteName, out sprite) && sprite != null;
        }

        /// <summary>加载单个图标：失败记日志返回 null（不抛出，等待方一律安全拿到 null）。</summary>
        private static async Task<Sprite> LoadAsync(string spriteName)
        {
            try
            {
                ResourceLease<Sprite> lease = await AddressableManager.Instance
                    .AcquirePersistentAssetAsync<Sprite>($"{spriteName}[{spriteName}]");
                return lease?.Asset;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[IconLibrary] 图标加载失败：{spriteName}（{e.Message}）");
                return null;
            }
        }
    }
}
