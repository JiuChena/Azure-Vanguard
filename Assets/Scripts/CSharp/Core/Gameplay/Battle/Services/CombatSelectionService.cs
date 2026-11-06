namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 战斗选中态子服务：一场战斗创建一份 BattleSelection，Reset 时释放。
    /// 选中态比面板长寿（面板关闭后可重开），拾取逻辑在 CombatSelectionPicker。
    /// </summary>
    public sealed class CombatSelectionService : ICombatSubservice
    {
        /// <summary>当前战斗选中态；无战斗时为 null。</summary>
        public BattleSelection Current => BattleSelection.Current;

        /// <summary>创建本场选中态（已存在则复用）。</summary>
        public void Begin()
        {
            if (BattleSelection.Current == null) new BattleSelection();
        }

        /// <summary>释放选中态（无选中时安全）。</summary>
        public void Reset()
        {
            BattleSelection.Current?.Dispose();
        }
    }
}
