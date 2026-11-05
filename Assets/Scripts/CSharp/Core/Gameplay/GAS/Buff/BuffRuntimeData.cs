using System;
using System.Collections.Generic;
using Framework.Gameplay.Abilities.Stat;
using Framework.Gameplay.Battle;

namespace Framework.Gameplay.Abilities.Buff
{
    /// <summary>
    /// Buff 运行时数据（buff 能力注册进上下文的键数据）：管控单位身上的全部 buff——
    /// 注册（ApplyBuff）、帧轮询（Tick）、注销（RemoveBuff/到期自动注销）、访问
    /// （Buffs/BuffCount/HasBuff/TryGetBuffByBuffId/CountByBuffId）。
    /// 宿主持有能力上下文/单位模拟状态/属性数据三引用（buff 实现经 StatData 加属性加成，
    /// SourceId 约定 = "buff_{BuffId}"，同 id 叠层共用，RemoveBySource 整组撤回）。
    ///
    /// Tick 三段时序（对齐"帧中断检查先于帧轮询"语义）：
    ///   阶段一【帧中断·先于帧轮询】只读正向遍历 _buffs，调 DoneCallback(dt)，true 收进本地 expired（零删改）
    ///   阶段二【统一注销】遍历 expired 快照：先 UnregisterCallback()（撤 StatModifier）再移出两列表
    ///   阶段三【帧轮询】对 _buffs 一次性快照遍历，调用前 _buffs.Contains 校验命中才 OnUpdate(dt)
    ///     ——保证本帧到期绝不再收本帧轮询；OnUpdate 内可安全 RemoveBuff（已移除者不再收本帧）。
    /// 事件广播（BuffApplied/BuffRemoved）随"施放来源链"下一轮接入，本轮不发。
    /// </summary>
    public sealed class BuffRuntimeData : IAbilityRuntimeData
    {
        // 能力拥有者上下文（宿主引用；独立构造测试时可为 null，本轮无消费方）
        private readonly AbilityOwnerContext _ownerContext;

        // 单位模拟状态（③注册表持有所有权；只读引用不持有）
        private readonly BattleUnitState _unit;

        // 属性数据（Stat 能力注册；buff 生效/失效在此加/撤加成，只读引用不持有）
        private readonly StatRuntimeData _statData;

        // 注册中的全部 buff（入列序 = 注册序，展示按此序排列）
        private readonly List<IBuff> _buffs = new List<IBuff>();

        // buffId → 同 id 叠层列表（访问/计数索引；叠层 append，注销时移除）
        private readonly Dictionary<int, List<IBuff>> _byBuffId = new Dictionary<int, List<IBuff>>();

        /// <summary>创建 buff 运行时数据。</summary>
        /// <param name="ownerContext">能力拥有者上下文；可为 null（独立测试构造）。</param>
        /// <param name="unit">单位模拟状态；不允许为 null。</param>
        /// <param name="statData">单位属性数据；不允许为 null。</param>
        /// <exception cref="ArgumentNullException">单位模拟状态或属性数据为空时抛出。</exception>
        public BuffRuntimeData(AbilityOwnerContext ownerContext, BattleUnitState unit, StatRuntimeData statData)
        {
            _ownerContext = ownerContext;
            _unit = unit ?? throw new ArgumentNullException(nameof(unit));
            _statData = statData ?? throw new ArgumentNullException(nameof(statData));
        }

        /// <summary>能力拥有者上下文（后续事件广播等接入用）。</summary>
        public AbilityOwnerContext OwnerContext => _ownerContext;

        /// <summary>单位模拟状态。</summary>
        public BattleUnitState Unit => _unit;

        /// <summary>单位属性数据（buff 加成的读写口）。</summary>
        public StatRuntimeData StatData => _statData;

        /// <summary>注册中的全部 buff（只读；顺序 = 注册序）。</summary>
        public IReadOnlyList<IBuff> Buffs => _buffs;

        /// <summary>注册中的 buff 总数（含叠层）。</summary>
        public int BuffCount => _buffs.Count;

        /// <summary>指定 buff 实例是否仍注册中。</summary>
        public bool HasBuff(IBuff buff)
        {
            return buff != null && _buffs.Contains(buff);
        }

        /// <summary>按 buffId 查首个注册实例（叠层首实例）。</summary>
        /// <param name="buffId">效果行主键（SkillEffectTable.effectId）。</param>
        /// <param name="buff">命中的首实例；未注册该 id 时为 null。</param>
        /// <returns>命中返回 true。</returns>
        public bool TryGetBuffByBuffId(int buffId, out IBuff buff)
        {
            if (_byBuffId.TryGetValue(buffId, out List<IBuff> list) && list.Count > 0)
            {
                buff = list[0];
                return true;
            }
            buff = null;
            return false;
        }

        /// <summary>按 buffId 计数（叠层数）；未注册该 id 返回 0。</summary>
        public int CountByBuffId(int buffId)
        {
            return _byBuffId.TryGetValue(buffId, out List<IBuff> list) ? list.Count : 0;
        }

        /// <summary>
        /// 注册一个 buff：先调 RegisterCallback(host)（生效副作用），成功后入列并登记 id 索引。
        /// null 与同实例重复注册忽略（同实例已在列不二次生效）。
        /// </summary>
        /// <param name="buff">待注册的 buff 实例。</param>
        public void ApplyBuff(IBuff buff)
        {
            if (buff == null || _buffs.Contains(buff)) return;

            buff.RegisterCallback(this);
            _buffs.Add(buff);
            if (!_byBuffId.TryGetValue(buff.BuffId, out List<IBuff> list))
            {
                list = new List<IBuff>();
                _byBuffId[buff.BuffId] = list;
            }
            list.Add(buff);
        }

        /// <summary>
        /// 注销一个 buff（幂等）：仅对仍注册中的实例调 UnregisterCallback（撤生效副作用）再移出。
        /// 未注册/已注销实例调用无操作——可被 OnUpdate/Tick 外任意上下文安全重复调用。
        /// </summary>
        /// <param name="buff">待注销的 buff 实例。</param>
        public void RemoveBuff(IBuff buff)
        {
            if (buff == null || !_buffs.Contains(buff)) return;
            buff.UnregisterCallback();
            RemoveInternal(buff);
        }

        /// <summary>
        /// 帧轮询（BuffAbilityRuntime 每帧调用）：三段时序见类头。
        /// 阶段二注销用快照遍历，阶段三 OnUpdate 用快照 + Contains 校验——任何删改只发生在
        /// 统一注销与 OnUpdate 内部，遍历中不直接改列表（防漏检/跳项）。
        /// </summary>
        /// <param name="deltaTime">本帧时长，单位：秒。</param>
        public void Tick(float deltaTime)
        {
            if (_buffs.Count == 0) return;

            // 阶段一：帧中断检查（先于帧轮询）——只读正向遍历，true 收进本地 expired，本段零删改
            List<IBuff> expired = null;
            for (int i = 0; i < _buffs.Count; i++)
            {
                IBuff buff = _buffs[i];
                if (buff.DoneCallback(deltaTime))
                {
                    if (expired == null) expired = new List<IBuff>();
                    expired.Add(buff);
                }
            }

            // 阶段二：统一注销（快照遍历安全删改）——先撤副作用再移出，本帧到期的绝不再收本帧轮询
            if (expired != null)
            {
                for (int i = 0; i < expired.Count; i++)
                {
                    IBuff buff = expired[i];
                    if (!_buffs.Contains(buff)) continue; // 防御：阶段一后不对外调用，正常情况下恒命中
                    buff.UnregisterCallback();
                    RemoveInternal(buff);
                }
            }

            // 阶段三：帧轮询——对 _buffs 做一次性快照，调用前校验仍在列（OnUpdate 内可安全 RemoveBuff）
            if (_buffs.Count == 0) return;
            List<IBuff> snapshot = new List<IBuff>(_buffs);
            for (int i = 0; i < snapshot.Count; i++)
            {
                IBuff buff = snapshot[i];
                if (!_buffs.Contains(buff)) continue;
                buff.OnUpdate(deltaTime);
            }
        }

        /// <summary>
        /// 清空全部 buff（OwnerContext.Reset(Buff) 语义）：逐条调 UnregisterCallback（撤全部生效副作用）
        /// 再清两列表。与 RemoveBuff 同约定——UnregisterCallback 内不得调用 host.RemoveBuff（注销由宿主驱动）。
        /// </summary>
        public void Reset()
        {
            for (int i = _buffs.Count - 1; i >= 0; i--) _buffs[i].UnregisterCallback();
            _buffs.Clear();
            _byBuffId.Clear();
        }

        // 从两列表移除一个已注销的 buff（调用方保证其不再被需要）
        private void RemoveInternal(IBuff buff)
        {
            _buffs.Remove(buff);
            if (_byBuffId.TryGetValue(buff.BuffId, out List<IBuff> list))
            {
                list.Remove(buff);
                if (list.Count == 0) _byBuffId.Remove(buff.BuffId);
            }
        }
    }
}
