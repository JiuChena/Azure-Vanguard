namespace Framework.Gameplay.Abilities.Buff
{
    /// <summary>
    /// Buff 能力契约：所有 buff 类必须实现本接口，逻辑自洽由实现类负责（每个 buff
    /// 自带生效/失效/逐帧/到期判定），宿主 <see cref="BuffRuntimeData"/> 只做注册、帧轮询与注销驱动。
    /// 生命周期时序（由宿主 <see cref="BuffRuntimeData.Tick"/> 精确驱动，本接口实现不得私自改列表）：
    ///   ApplyBuff → RegisterCallback(host)  （入列前调用：注册期副作用=加属性/记状态）
    ///   每帧 Tick  → DoneCallback(dt) 先于 OnUpdate(dt)（帧中断检查，先判断是否该结束）
    ///   到期       → 系统统一调 UnregisterCallback()（撤销注册期副作用）再移出列表
    /// </summary>
    public interface IBuff
    {
        /// <summary>
        /// Buff 标识（效果行主键 SkillEffectTable.effectId，全局唯一）：注册时宿主据此登记
        /// <c>_byBuffId</c> 索引，展示侧按它查 SkillEffectTable 效果行（名称/描述/图标）；叠层同 id 共用。
        /// </summary>
        int BuffId { get; }

        /// <summary>
        /// 注册回调：Buff 生效时被宿主调用一次（入列前）。在此完成生效副作用——
        /// 推荐经 <paramref name="host"/> 的 <see cref="BuffRuntimeData.StatData"/> 加属性加成，
        /// 来源标识统一 <c>buff_{BuffId}</c>（同 id 叠层共用，<see cref="StatRuntimeData.RemoveBySource"/> 整组撤回）。
        /// 禁止在本方法内调用 host.RemoveBuff（注销由宿主统一驱动，会递归）。
        /// </summary>
        /// <param name="host">持有本 buff 的宿主（BuffRuntimeData）。</param>
        void RegisterCallback(BuffRuntimeData host);

        /// <summary>
        /// 注销回调：Buff 到期/被移除时由宿主调用一次。在此撤销注册期副作用
        /// （RemoveBySource("buff_{BuffId}") 撤净该来源全部加成）。幂等：可重复调用安全。
        /// </summary>
        void UnregisterCallback();

        /// <summary>
        /// 逐帧轮询回调：Buff 存续期间宿主每帧调用（dt = 本帧时长）。只做该 buff 的
        /// 内部逻辑（计时器/数值衰减等表现侧字段），不得私自改宿主 Buff 列表——
        /// 需提前结束请经宿主 RemoveBuff（宿主的快照校验会正确处理本帧语义）。
        /// </summary>
        /// <param name="deltaTime">本帧时长，单位：秒。</param>
        void OnUpdate(float deltaTime);

        /// <summary>
        /// 帧中断判断：每帧在帧轮询之前被宿主调用一次。返回 true = 有效期到点，宿主
        /// 将在同一帧统一调用 <see cref="UnregisterCallback"/> 并移出列表——
        /// 本帧绝不再收 <see cref="OnUpdate"/>。返回 false = 继续存续。
        /// </summary>
        /// <param name="deltaTime">本帧时长，单位：秒。</param>
        /// <returns>true 表示本帧到期，应注销；false 表示继续。</returns>
        bool DoneCallback(float deltaTime);
    }
}
