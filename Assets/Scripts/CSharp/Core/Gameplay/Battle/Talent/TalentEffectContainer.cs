using System;
using System.Collections.Generic;
using Framework.Gameplay.Abilities.Buff;
using UnityEngine;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 战斗级全场 Effect 容器（2026-11-04 用户拍板）：注册效果 → 新单位登场按 CanAcquire 获取；
    /// 来源倒地统一释放、复活重新提供；伤害事件派发给攻击者已获取的效果。
    /// 纯 C#，随 BattleController 生命周期；Dispose 清空光环与展示 buff。
    /// </summary>
    public sealed class TalentEffectContainer : IDisposable
    {
        private readonly BattleController controller;
        private readonly List<IBattleGlobalEffect> effects = new List<IBattleGlobalEffect>();
        private readonly Dictionary<string, HashSet<BattleUnitState>> acquired =
            new Dictionary<string, HashSet<BattleUnitState>>();
        private readonly Dictionary<BattleUnitState, BuffRuntimeData> views =
            new Dictionary<BattleUnitState, BuffRuntimeData>();
        private bool disposed;

        public TalentEffectContainer(BattleController controller)
        {
            this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
            this.controller.UnitSpawned += OnUnitSpawned;
            this.controller.UnitRevived += OnUnitRevived;
            this.controller.UnitDowned += OnUnitDowned;
            this.controller.EnemyRemoved += OnUnitRemoved;
            this.controller.DamageDealt += OnDamageDealt;
        }

        /// <summary>所属战场。</summary>
        public BattleController Controller => controller;

        /// <summary>注册一个全场效果；注册后立即向场上所有单位按 CanAcquire 提供。</summary>
        public void Register(IBattleGlobalEffect effect)
        {
            if (disposed || effect == null || string.IsNullOrEmpty(effect.Id)) return;
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i].Id == effect.Id) return;
            }

            effects.Add(effect);
            acquired[effect.Id] = new HashSet<BattleUnitState>();
            IReadOnlyList<BattleUnitState> units = controller.Units;
            for (int i = 0; i < units.Count; i++) Offer(effect, units[i]);
        }

        /// <summary>注销效果并从所有已获取单位释放。</summary>
        public void Unregister(string effectId)
        {
            if (string.IsNullOrEmpty(effectId)) return;
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                IBattleGlobalEffect effect = effects[i];
                if (effect.Id != effectId) continue;
                ReleaseAll(effect);
                effects.RemoveAt(i);
                acquired.Remove(effectId);
                if (!disposed) TalentAura.RemoveAllForSource(effectId);
                return;
            }
        }

        /// <summary>视图/GAS 就绪：登记 BuffRuntimeData，并给该单位补挂已获取效果的展示 buff。</summary>
        public void AttachView(BattleUnitState unit, BuffRuntimeData buffRuntimeData)
        {
            if (disposed || unit == null || buffRuntimeData == null) return;
            views[unit] = buffRuntimeData;
            for (int i = 0; i < effects.Count; i++)
            {
                IBattleGlobalEffect effect = effects[i];
                if (!AcquiredBy(effect, unit)) continue;
                ApplyDisplay(effect, unit);
            }
        }

        /// <summary>视图销毁：摘除展示登记。</summary>
        public void DetachView(BattleUnitState unit)
        {
            if (unit != null) views.Remove(unit);
        }

        /// <summary>伤害事件派发给攻击者已获取的效果。</summary>
        private void OnDamageDealt(BattleDamageEvent e)
        {
            if (disposed || e.Attacker == null) return;
            for (int i = 0; i < effects.Count; i++)
            {
                IBattleGlobalEffect effect = effects[i];
                if (!AcquiredBy(effect, e.Attacker)) continue;
                try
                {
                    effect.OnDamageDealt(e);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[Talent] 效果 {effect.Id} 处理伤害事件异常：{exception}");
                }
            }
        }

        private void OnUnitSpawned(BattleUnitState unit)
        {
            if (disposed || unit == null) return;
            // 角色登场：只负责把已注册效果按 CanAcquire 提供给该单位；
            // 天赋效果由角色进场的 Talent BE clip → EffectSO → 容器注册（2026-11-04 用户流程）
            OfferAllTo(unit);
        }

        private void OnUnitRevived(BattleUnitState unit)
        {
            if (disposed || unit == null) return;
            OfferAllTo(unit);
            // 来源复活：向全场重新提供它的效果
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i].Source == unit) OfferAll(effects[i]);
            }
        }

        private void OnUnitDowned(BattleUnitState unit)
        {
            if (disposed || unit == null) return;
            ReleaseAllForUnit(unit);
            // 来源倒地：它注册的效果从全场释放（保留注册，复活后可重新获取）
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i].Source == unit) ReleaseAll(effects[i]);
            }
        }

        private void OnUnitRemoved(BattleUnitState unit)
        {
            if (disposed || unit == null) return;
            ReleaseAllForUnit(unit);
            views.Remove(unit);
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i].Source == unit) ReleaseAll(effects[i]);
            }
        }

        private void OfferAllTo(BattleUnitState unit)
        {
            for (int i = 0; i < effects.Count; i++) Offer(effects[i], unit);
        }

        private void OfferAll(IBattleGlobalEffect effect)
        {
            IReadOnlyList<BattleUnitState> units = controller.Units;
            for (int i = 0; i < units.Count; i++) Offer(effect, units[i]);
        }

        private void Offer(IBattleGlobalEffect effect, BattleUnitState unit)
        {
            if (unit == null || !acquired.TryGetValue(effect.Id, out HashSet<BattleUnitState> holders)) return;
            if (holders.Contains(unit)) return;

            var context = new TalentAcquireContext(
                unit, unit.Camp, effect.Source, controller, IsSourceAlive(effect.Source), CollectIds(unit));
            bool canAcquire;
            try
            {
                canAcquire = effect.CanAcquire(context);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Talent] 效果 {effect.Id} 获取判定异常：{exception}");
                return;
            }
            if (!canAcquire) return;

            holders.Add(unit);
            effect.OnAcquire(unit);
            ApplyDisplay(effect, unit);
        }

        private void Release(IBattleGlobalEffect effect, BattleUnitState unit)
        {
            if (!acquired.TryGetValue(effect.Id, out HashSet<BattleUnitState> holders)) return;
            if (!holders.Remove(unit)) return;
            effect.OnRelease(unit);
            RemoveDisplay(effect, unit);
        }

        private void ReleaseAll(IBattleGlobalEffect effect)
        {
            if (!acquired.TryGetValue(effect.Id, out HashSet<BattleUnitState> holders)) return;
            var snapshot = new List<BattleUnitState>(holders);
            for (int i = 0; i < snapshot.Count; i++) Release(effect, snapshot[i]);
        }

        private void ReleaseAllForUnit(BattleUnitState unit)
        {
            for (int i = 0; i < effects.Count; i++) Release(effects[i], unit);
        }

        private void ApplyDisplay(IBattleGlobalEffect effect, BattleUnitState unit)
        {
            if (!views.TryGetValue(unit, out BuffRuntimeData buffs) || buffs == null) return;
            if (effect.DisplayBuffId > 0 && !buffs.TryGetBuffByBuffId(effect.DisplayBuffId, out _))
            {
                buffs.ApplyBuff(new TalentDisplayBuff(effect.DisplayBuffId));
            }
            // 面板数值：光环加成挂到单位 StatRuntimeData（战斗结算仍走 TalentAura，避免双算）
            if (effect is ITalentDisplayStats display) display.ApplyDisplayStats(buffs.StatData);
        }

        private void RemoveDisplay(IBattleGlobalEffect effect, BattleUnitState unit)
        {
            if (!views.TryGetValue(unit, out BuffRuntimeData buffs) || buffs == null) return;
            if (effect.DisplayBuffId > 0 && buffs.TryGetBuffByBuffId(effect.DisplayBuffId, out IBuff buff))
            {
                buffs.RemoveBuff(buff);
            }
            if (effect is ITalentDisplayStats display) display.RemoveDisplayStats(buffs.StatData);
        }

        private bool AcquiredBy(IBattleGlobalEffect effect, BattleUnitState unit)
        {
            return acquired.TryGetValue(effect.Id, out HashSet<BattleUnitState> holders) && holders.Contains(unit);
        }

        private HashSet<string> CollectIds(BattleUnitState unit)
        {
            var ids = new HashSet<string>();
            for (int i = 0; i < effects.Count; i++)
            {
                if (AcquiredBy(effects[i], unit)) ids.Add(effects[i].Id);
            }
            return ids;
        }

        private static bool IsSourceAlive(BattleUnitState source)
        {
            return source != null && source.IsAlive && source.Vital == UnitVitalState.Alive;
        }

        /// <summary>战斗结束清理：释放全部效果、清光环与展示登记。</summary>
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            controller.UnitSpawned -= OnUnitSpawned;
            controller.UnitRevived -= OnUnitRevived;
            controller.UnitDowned -= OnUnitDowned;
            controller.EnemyRemoved -= OnUnitRemoved;
            controller.DamageDealt -= OnDamageDealt;

            for (int i = 0; i < effects.Count; i++) ReleaseAll(effects[i]);
            effects.Clear();
            acquired.Clear();
            views.Clear();
            TalentAura.Clear();
        }
    }
}
