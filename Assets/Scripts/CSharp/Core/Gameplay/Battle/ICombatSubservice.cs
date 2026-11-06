namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 战斗子服务生命周期契约：战斗 Reset 时统一清空自身状态。
    /// Begin 的签名因服务而异，不在此强行统一。
    /// </summary>
    public interface ICombatSubservice
    {
        /// <summary>清空本服务持有的战斗状态；必须可重复调用且对未初始化状态安全。</summary>
        void Reset();
    }
}
