using System;
using Core.Gear;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 战斗事件桥子服务：把 CombatService 的模拟事件桥接为 EventCenter 广播，
    /// 装配完成后补发点数/波次初值，供 HUD 订阅。
    /// </summary>
    public sealed class CombatEventService : ICombatSubservice
    {
        // 所属战斗服务。
        private CombatService service;

        // EventCenter 退订句柄（必须与订阅同一委托实例）。
        private Action<int> onPointsChanged;
        private Action<int> onWaveChanged;
        private Action<BattleOutcome> onOutcomeDecided;

        /// <summary>绑定战斗服务并桥接事件；装配后立即补发点数/波次初值。</summary>
        /// <param name="combatService">所属战斗服务；不允许为 null。</param>
        public void Bind(CombatService combatService)
        {
            if (combatService == null) throw new ArgumentNullException(nameof(combatService));

            Reset();
            service = combatService;

            onPointsChanged = points =>
                EventCenter.Instance.SetEventTrigger(EventNames.BattlePointChanged, points);
            onWaveChanged = wave =>
                EventCenter.Instance.SetEventTrigger(EventNames.BattleWaveChanged, wave);
            onOutcomeDecided = outcome =>
                EventCenter.Instance.SetEventTrigger(EventNames.BattleOutcomeDecided, outcome);

            service.PointsChanged += onPointsChanged;
            service.WaveChanged += onWaveChanged;
            service.OutcomeDecided += onOutcomeDecided;

            EventCenter.Instance.SetEventTrigger(EventNames.BattlePointChanged, service.Points);
            EventCenter.Instance.SetEventTrigger(EventNames.BattleWaveChanged, service.CurrentWaveNumber);
        }

        /// <summary>退订全部桥接事件。</summary>
        public void Reset()
        {
            if (service != null)
            {
                service.PointsChanged -= onPointsChanged;
                service.WaveChanged -= onWaveChanged;
                service.OutcomeDecided -= onOutcomeDecided;
            }
            service = null;
            onPointsChanged = null;
            onWaveChanged = null;
            onOutcomeDecided = null;
        }
    }
}
