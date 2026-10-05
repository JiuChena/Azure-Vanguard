using System;
using BehaviorEditor;
using UnityEngine;

namespace Framework.Gameplay.Abilities
{
    /// <summary>保存 BE 行为能力向其他能力公开的运行时数据：按键查询 UnitConfig 播放与停止入口。</summary>
    public sealed class BehaviorAbilityRuntimeData : IAbilityRuntimeData
    {
        // 当前实体 BE 能力创建并持有的播放执行器。
        private readonly BehaviorExecutor _executor;
        // 当前单位静态配置；未在 GAS 组件配置时为 null。
        private readonly UnitConfig _unitConfig;
        // 当前单位对象，用于日志关联。
        private readonly GameObject _owner;

        /// <summary>
        /// 创建当前实体 BE 行为的公开运行时数据。
        /// </summary>
        /// <param name="executor">当前实体已经创建的播放执行器；不允许为 null。</param>
        /// <param name="unitConfig">当前单位静态配置；允许为 null，此时播放请求按缺失处理。</param>
        /// <param name="owner">当前单位对象，用于日志关联；允许为 null。</param>
        /// <exception cref="ArgumentNullException">播放执行器为空时抛出。</exception>
        public BehaviorAbilityRuntimeData(BehaviorExecutor executor, UnitConfig unitConfig, GameObject owner)
        {
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            _unitConfig = unitConfig;
            _owner = owner;
        }

        /// <summary>
        /// 按叶子状态键播放对应 BE 行为；未配置单位表或键未命中时记录错误并忽略本次请求。
        /// </summary>
        /// <param name="behaviorKey">叶子状态类中声明的 BehaviorKey 常量。</param>
        public void Play(string behaviorKey)
        {
            if (string.IsNullOrEmpty(behaviorKey))
            {
                Debug.LogWarning("[Behavior] 播放请求的行为键为空，已忽略。", _owner);
                return;
            }
            if (_unitConfig == null)
            {
                Debug.LogError("[Behavior] 单位未配置 UnitConfig，无法查询行为资产。", _owner);
                return;
            }
            if (!_unitConfig.BehaviorClips.TryGetValue(behaviorKey, out BehaviorClip clip) || clip == null)
            {
                Debug.LogError($"[Behavior] 行为键 '{behaviorKey}' 未在 UnitConfig '{_unitConfig.UnitId}' 中配置有效资产。", _owner);
                return;
            }

            _executor.Play(clip);
        }

        /// <summary>停止当前实体的 BE 行为播放。</summary>
        public void Stop()
        {
            _executor.Stop();
        }

        /// <summary>保持播放状态，不在 GAS 的通用数据重置阶段打断表现。</summary>
        public void Reset()
        {
        }
    }
}
