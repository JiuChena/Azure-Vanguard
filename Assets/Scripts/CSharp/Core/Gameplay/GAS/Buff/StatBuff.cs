using Framework.Gameplay.Abilities.Stat;

namespace Framework.Gameplay.Abilities.Buff
{
    /// <summary>参数化属性加成 Buff（技能效果函数 RandomBuff 用，2026-10-28）：
    /// 按 buffId 加一个或多个 StatModifier（Flat 多属性，如双暴拐 = CritRate+CritDmg），
    /// duration 到期经 DoneCallback 由宿主统一注销（UnregisterCallback 按源整组撤加成）。
    /// buffId = 效果行 id（SkillEffectTable），数值/属性/时长由效果函数读表得出，代码零硬编码。</summary>
    public sealed class StatBuff : IBuff
    {
        private readonly int _buffId;
        private readonly (StatType Type, float Value)[] _attributes;
        private readonly float _duration;
        private BuffRuntimeData _host;
        private float _elapsed;

        /// <inheritdoc/>
        public int BuffId => _buffId;

        /// <summary>剩余秒数（-1 = 永久）。面板展示用。</summary>
        public float RemainingTime => _duration > 0f ? System.Math.Max(0f, _duration - _elapsed) : -1f;

        /// <summary>总时长秒数（0 = 永久）。</summary>
        public float Duration => _duration;

        /// <summary>加成属性与数值（面板展示用）。</summary>
        public (StatType Type, float Value)[] Attributes => _attributes;

        /// <summary>创建参数化属性加成 buff（可多属性，如双暴拐 = 暴率+暴伤）。</summary>
        /// <param name="buffId">效果行主键（SkillEffectTable.effectId；展示元数据/叠层共用）。</param>
        /// <param name="duration">持续秒数（0 = 永久，需外部注销）。</param>
        /// <param name="attributes">属性加成（StatType + Flat 值，逐个 AddModifier）。</param>
        public StatBuff(int buffId, float duration, params (StatType Type, float Value)[] attributes)
        {
            _buffId = buffId;
            _attributes = attributes ?? new (StatType, float)[0];
            _duration = duration;
        }

        /// <inheritdoc/>
        public void RegisterCallback(BuffRuntimeData host)
        {
            _host = host;
            _elapsed = 0f;
            for (int i = 0; i < _attributes.Length; i++)
            {
                host.StatData.AddModifier(
                    new StatModifier($"buff_{_buffId}", _attributes[i].Type, StatModifier.ModifierType.Flat,
                        _attributes[i].Value));
            }
        }

        /// <inheritdoc/>
        public void UnregisterCallback()
        {
            _host?.StatData.RemoveBySource($"buff_{_buffId}");
            _host = null;
        }

        /// <inheritdoc/>
        public void OnUpdate(float dt)
        {
        }

        /// <inheritdoc/>
        public bool DoneCallback(float dt)
        {
            if (_duration <= 0f) return false;
            _elapsed += dt;
            return _elapsed >= _duration;
        }
    }
}
