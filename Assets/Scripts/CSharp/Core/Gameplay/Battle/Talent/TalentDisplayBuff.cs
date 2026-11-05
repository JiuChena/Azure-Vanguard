using Framework.Gameplay.Abilities.Buff;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 天赋展示用 buff（2026-11-04）：只用于 UnitStatPanel Effect 页显示条目，
    /// 不加减属性、不参与战斗结算（战斗数值由 TalentAura/Effect 自己负责）。
    /// </summary>
    public sealed class TalentDisplayBuff : IBuff
    {
        private readonly int buffId;

        public TalentDisplayBuff(int buffId)
        {
            this.buffId = buffId;
        }

        public int BuffId => buffId;

        public void RegisterCallback(BuffRuntimeData host)
        {
        }

        public void UnregisterCallback()
        {
        }

        public void OnUpdate(float deltaTime)
        {
        }

        public bool DoneCallback(float deltaTime)
        {
            return false;
        }
    }
}
