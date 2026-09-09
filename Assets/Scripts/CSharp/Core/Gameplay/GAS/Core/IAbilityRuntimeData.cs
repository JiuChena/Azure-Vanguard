namespace Framework.Gameplay.Abilities
{
    /// <summary>定义可注册到能力拥有者上下文的运行时数据契约。</summary>
    public interface IAbilityRuntimeData
    {
        /// <summary>清空数据对象持有的运行时状态。</summary>
        void Reset();
    }
}
