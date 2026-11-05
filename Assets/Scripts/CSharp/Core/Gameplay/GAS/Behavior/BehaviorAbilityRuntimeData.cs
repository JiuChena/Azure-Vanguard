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
        // 宿主 GAS 能力上下文（技能方案第 0 步：执行链注入，效果段直接访问能力）。
        private readonly AbilityOwnerContext _ownerContext;
        // 当前单位对象，用于日志关联。
        private readonly GameObject _owner;

        /// <summary>
        /// 创建当前实体 BE 行为的公开运行时数据。
        /// </summary>
        /// <param name="executor">当前实体已经创建的播放执行器；不允许为 null。</param>
        /// <param name="unitConfig">当前单位静态配置；允许为 null，此时播放请求按缺失处理。</param>
        /// <param name="owner">当前单位对象，用于日志关联；允许为 null。</param>
        /// <param name="ownerContext">宿主 GAS 能力上下文；允许为 null（非 GAS 宿主缺省）。</param>
        /// <exception cref="ArgumentNullException">播放执行器为空时抛出。</exception>
        public BehaviorAbilityRuntimeData(BehaviorExecutor executor, UnitConfig unitConfig, GameObject owner,
            AbilityOwnerContext ownerContext = null)
        {
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            _unitConfig = unitConfig;
            _owner = owner;
            _ownerContext = ownerContext;
        }

        /// <summary>
        /// 按叶子状态键播放对应 BE 行为；未配置单位表或键未命中时记录错误并忽略本次请求。
        /// </summary>
        /// <param name="behaviorKey">叶子状态类中声明的 BehaviorKey 常量。</param>
        /// <remarks>项目硬约束（2026-11-05）：仅供 BHSM 状态经 CombatStateContext.PlayBehavior 调用；
        /// 外部系统禁止直接播放角色战斗 BehaviorClip（一次性 clip 也必须建 HSM 状态 + 完成度门禁）。</remarks>
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
            if (!_unitConfig.TryGetBehaviorClip(behaviorKey, out BehaviorClip clip) || clip == null)
            {
                Debug.LogError($"[Behavior] 行为键 '{behaviorKey}' 未在 UnitConfig '{_unitConfig.UnitId}' 中配置同名 BehaviorClip 资产。", _owner);
                return;
            }

            _executor.Play(clip, _ownerContext);
        }

        /// <summary>停止当前实体的 BE 行为播放。</summary>
        public void Stop()
        {
            _executor.Stop();
        }

        /// <summary>当前行为是否播放中（状态完成度 gate 用；未播放/未配置时 false）。</summary>
        public bool IsPlaying => _executor.IsPlaying;

        /// <summary>当前行为归一化进度 0~1（Loop 段回绕；轮完成检测用；未播放时 0）。</summary>
        public float NormalizedTime => _executor.NormalizedTime;

        /// <summary>当前行为配置时长（秒；未播放时 0——表现缺失兜底判据）。</summary>
        public float Duration => _executor.CurrentPlaybackSettings != null
            ? _executor.CurrentPlaybackSettings.duration
            : 0f;

        /// <summary>保持播放状态，不在 GAS 的通用数据重置阶段打断表现。</summary>
        public void Reset()
        {
        }
    }
}
