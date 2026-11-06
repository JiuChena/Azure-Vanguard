using System;
using System.Collections.Generic;
using Framework.Gameplay.Abilities.Buff;
using UnityEngine;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 战斗级全局 Effect 子服务：按角色/敌人两个阵营桶注册效果，新单位登场按阵营 + CanAcquire 获取；
    /// 来源倒地统一释放、复活重新提供；伤害事件派发给攻击者已获取的效果。
    /// 光环数值（充能效率/伤害加成）由本服务实例表承载，随战斗 Reset 清空，不再使用全局静态表。
    /// </summary>
    public sealed class CombatEffectService : ICombatSubservice
    {
        // 光环加成条目（单位 id → 来源列表）。
        private sealed class AuraEntry
        {
            public string SourceId;
            public float EnergyRecharge;
            public float DamageBonus;
        }

        // 所属战斗服务（单位查询 / 事件订阅）。
        private CombatService service;

        // 角色侧全局效果。
        private readonly List<IBattleGlobalEffect> characterEffects = new List<IBattleGlobalEffect>();

        // 敌人侧全局效果。
        private readonly List<IBattleGlobalEffect> enemyEffects = new List<IBattleGlobalEffect>();

        // 效果 id → 注册阵营（Offer 前先按阵营过滤）。
        private readonly Dictionary<string, BattleCamp> effectCamps = new Dictionary<string, BattleCamp>();

        // 效果 id → 已获取该效果的单位集合。
        private readonly Dictionary<string, HashSet<BattleUnitState>> acquired =
            new Dictionary<string, HashSet<BattleUnitState>>();

        // 单位 → BuffRuntimeData（展示 buff 挂载点）。
        private readonly Dictionary<BattleUnitState, BuffRuntimeData> views =
            new Dictionary<BattleUnitState, BuffRuntimeData>();

        // 光环加成表：单位 id → 来源条目列表。
        private readonly Dictionary<string, List<AuraEntry>> auras = new Dictionary<string, List<AuraEntry>>();

        // 避免重复退订的标记。
        private bool bound;

        /// <summary>所属战斗服务（效果内部取随机源/单位列表用）。</summary>
        public CombatService Service => service;

        /// <summary>角色侧全局效果（只读）。</summary>
        public IReadOnlyList<IBattleGlobalEffect> CharacterEffects => characterEffects;

        /// <summary>敌人侧全局效果（只读）。</summary>
        public IReadOnlyList<IBattleGlobalEffect> EnemyEffects => enemyEffects;

        /// <summary>绑定战斗服务并订阅单位/伤害事件（重复调用先退订清空）。</summary>
        /// <param name="combatService">所属战斗服务；不允许为 null。</param>
        public void Begin(CombatService combatService)
        {
            if (combatService == null) throw new ArgumentNullException(nameof(combatService));

            Reset();
            service = combatService;
            service.UnitSpawned += OnUnitSpawned;
            service.UnitRevived += OnUnitRevived;
            service.UnitDowned += OnUnitDowned;
            service.EnemyRemoved += OnUnitRemoved;
            service.DamageDealt += OnDamageDealt;
            bound = true;
        }

        /// <summary>退订事件并释放全部效果、光环与展示登记。</summary>
        public void Reset()
        {
            if (bound && service != null)
            {
                service.UnitSpawned -= OnUnitSpawned;
                service.UnitRevived -= OnUnitRevived;
                service.UnitDowned -= OnUnitDowned;
                service.EnemyRemoved -= OnUnitRemoved;
                service.DamageDealt -= OnDamageDealt;
            }
            bound = false;

            ReleaseAllEffects();
            characterEffects.Clear();
            enemyEffects.Clear();
            effectCamps.Clear();
            acquired.Clear();
            views.Clear();
            auras.Clear();
            service = null;
        }

        /// <summary>注册一个角色侧全局效果；注册后立即向场上角色按 CanAcquire 提供。</summary>
        /// <param name="effect">效果实例；Id 为空或重复时忽略。</param>
        public void RegisterCharacterEffect(IBattleGlobalEffect effect)
        {
            Register(effect, BattleCamp.Character);
        }

        /// <summary>注册一个敌人侧全局效果；注册后立即向场上敌人按 CanAcquire 提供。</summary>
        /// <param name="effect">效果实例；Id 为空或重复时忽略。</param>
        public void RegisterEnemyEffect(IBattleGlobalEffect effect)
        {
            Register(effect, BattleCamp.Enemy);
        }

        /// <summary>注销效果并从所有已获取单位释放。</summary>
        /// <param name="effectId">效果 id。</param>
        public void Unregister(string effectId)
        {
            if (string.IsNullOrEmpty(effectId)) return;

            IBattleGlobalEffect effect = FindEffect(effectId);
            if (effect == null) return;

            ReleaseAll(effect);
            characterEffects.Remove(effect);
            enemyEffects.Remove(effect);
            effectCamps.Remove(effectId);
            acquired.Remove(effectId);
            RemoveAllAurasForSource(effectId);
        }

        /// <summary>视图/GAS 就绪：登记 BuffRuntimeData，并补挂该单位已获取效果的展示 buff。</summary>
        /// <param name="unit">单位。</param>
        /// <param name="buffRuntimeData">该单位 GAS 的 Buff 运行时数据。</param>
        public void AttachView(BattleUnitState unit, BuffRuntimeData buffRuntimeData)
        {
            if (unit == null || buffRuntimeData == null) return;
            views[unit] = buffRuntimeData;
            for (int i = 0; i < characterEffects.Count; i++) ApplyDisplayIfAcquired(characterEffects[i], unit);
            for (int i = 0; i < enemyEffects.Count; i++) ApplyDisplayIfAcquired(enemyEffects[i], unit);
        }

        /// <summary>视图销毁：摘除展示登记。</summary>
        /// <param name="unit">单位。</param>
        public void DetachView(BattleUnitState unit)
        {
            if (unit != null) views.Remove(unit);
        }

        /// <summary>单位当前充能效率加成合计（0 = 无）。</summary>
        /// <param name="unitId">单位 id。</param>
        /// <returns>加成合计。</returns>
        public float GetEnergyRecharge(string unitId)
        {
            return SumAura(unitId, true);
        }

        /// <summary>单位当前伤害加成合计（0 = 无）。</summary>
        /// <param name="unitId">单位 id。</param>
        /// <returns>加成合计。</returns>
        public float GetDamageBonus(string unitId)
        {
            return SumAura(unitId, false);
        }

        /// <summary>给单位登记一条来源光环（同来源覆盖，不叠加）。</summary>
        /// <param name="unitId">单位 id。</param>
        /// <param name="sourceId">效果来源 id。</param>
        /// <param name="energyRecharge">充能效率加成。</param>
        /// <param name="damageBonus">伤害加成。</param>
        public void AddAura(string unitId, string sourceId, float energyRecharge, float damageBonus)
        {
            if (string.IsNullOrEmpty(unitId) || string.IsNullOrEmpty(sourceId)) return;
            if (!auras.TryGetValue(unitId, out List<AuraEntry> list))
            {
                list = new List<AuraEntry>();
                auras[unitId] = list;
            }
            list.RemoveAll(entry => entry.SourceId == sourceId);
            list.Add(new AuraEntry
            {
                SourceId = sourceId,
                EnergyRecharge = energyRecharge,
                DamageBonus = damageBonus,
            });
        }

        /// <summary>撤掉单位某来源的光环。</summary>
        /// <param name="unitId">单位 id。</param>
        /// <param name="sourceId">效果来源 id。</param>
        public void RemoveAura(string unitId, string sourceId)
        {
            if (string.IsNullOrEmpty(unitId) || !auras.TryGetValue(unitId, out List<AuraEntry> list)) return;
            list.RemoveAll(entry => entry.SourceId == sourceId);
            if (list.Count == 0) auras.Remove(unitId);
        }

        // 注册入口：按阵营入桶，建获取集合并立即提供。
        private void Register(IBattleGlobalEffect effect, BattleCamp camp)
        {
            if (effect == null || string.IsNullOrEmpty(effect.Id) || FindEffect(effect.Id) != null) return;

            if (camp == BattleCamp.Enemy) enemyEffects.Add(effect);
            else characterEffects.Add(effect);
            effectCamps[effect.Id] = camp;
            acquired[effect.Id] = new HashSet<BattleUnitState>();
            OfferAll(effect);
        }

        // 按 id 在两个桶中查效果。
        private IBattleGlobalEffect FindEffect(string effectId)
        {
            for (int i = 0; i < characterEffects.Count; i++)
            {
                if (characterEffects[i].Id == effectId) return characterEffects[i];
            }
            for (int i = 0; i < enemyEffects.Count; i++)
            {
                if (enemyEffects[i].Id == effectId) return enemyEffects[i];
            }
            return null;
        }

        // 伤害事件派发给攻击者已获取的效果。
        private void OnDamageDealt(BattleDamageEvent e)
        {
            if (e.Attacker == null) return;
            DispatchDamage(characterEffects, e);
            DispatchDamage(enemyEffects, e);
        }

        private void DispatchDamage(List<IBattleGlobalEffect> effects, BattleDamageEvent e)
        {
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
                    Debug.LogError($"[CombatEffect] 效果 {effect.Id} 处理伤害事件异常：{exception}");
                }
            }
        }

        private void OnUnitSpawned(BattleUnitState unit)
        {
            if (unit == null) return;
            OfferAllTo(unit);
        }

        private void OnUnitRevived(BattleUnitState unit)
        {
            if (unit == null) return;
            OfferAllTo(unit);
            // 来源复活：向全场重新提供它的效果
            OfferAllFromSource(characterEffects, unit);
            OfferAllFromSource(enemyEffects, unit);
        }

        private void OnUnitDowned(BattleUnitState unit)
        {
            if (unit == null) return;
            ReleaseAllForUnit(unit);
            // 来源倒地：它注册的效果从全场释放（保留注册，复活后可重新获取）
            ReleaseAllFromSource(characterEffects, unit);
            ReleaseAllFromSource(enemyEffects, unit);
        }

        private void OnUnitRemoved(BattleUnitState unit)
        {
            if (unit == null) return;
            ReleaseAllForUnit(unit);
            views.Remove(unit);
            ReleaseAllFromSource(characterEffects, unit);
            ReleaseAllFromSource(enemyEffects, unit);
        }

        private void OfferAllTo(BattleUnitState unit)
        {
            for (int i = 0; i < characterEffects.Count; i++) Offer(characterEffects[i], unit);
            for (int i = 0; i < enemyEffects.Count; i++) Offer(enemyEffects[i], unit);
        }

        private void OfferAllFromSource(List<IBattleGlobalEffect> effects, BattleUnitState source)
        {
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i].Source == source) OfferAll(effects[i]);
            }
        }

        private void OfferAll(IBattleGlobalEffect effect)
        {
            if (service == null || !service.IsBattleActive) return;
            IReadOnlyList<BattleUnitState> units = service.Units;
            for (int i = 0; i < units.Count; i++) Offer(effect, units[i]);
        }

        private void Offer(IBattleGlobalEffect effect, BattleUnitState unit)
        {
            if (unit == null || !acquired.TryGetValue(effect.Id, out HashSet<BattleUnitState> holders)) return;
            if (holders.Contains(unit)) return;

            // 先按注册阵营过滤，再问效果自身的 CanAcquire（双保险）
            if (!effectCamps.TryGetValue(effect.Id, out BattleCamp camp) || unit.Camp != camp) return;

            var context = new TalentAcquireContext(
                unit, unit.Camp, effect.Source, service, IsSourceAlive(effect.Source), CollectIds(unit));
            bool canAcquire;
            try
            {
                canAcquire = effect.CanAcquire(context);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[CombatEffect] 效果 {effect.Id} 获取判定异常：{exception}");
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
            try
            {
                effect.OnRelease(unit);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[CombatEffect] 效果 {effect.Id} 释放异常：{exception}");
            }
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
            for (int i = 0; i < characterEffects.Count; i++) Release(characterEffects[i], unit);
            for (int i = 0; i < enemyEffects.Count; i++) Release(enemyEffects[i], unit);
        }

        private void ReleaseAllFromSource(List<IBattleGlobalEffect> effects, BattleUnitState source)
        {
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i].Source == source) ReleaseAll(effects[i]);
            }
        }

        private void ReleaseAllEffects()
        {
            for (int i = 0; i < characterEffects.Count; i++) ReleaseAll(characterEffects[i]);
            for (int i = 0; i < enemyEffects.Count; i++) ReleaseAll(enemyEffects[i]);
        }

        private void ApplyDisplayIfAcquired(IBattleGlobalEffect effect, BattleUnitState unit)
        {
            if (AcquiredBy(effect, unit)) ApplyDisplay(effect, unit);
        }

        private void ApplyDisplay(IBattleGlobalEffect effect, BattleUnitState unit)
        {
            if (!views.TryGetValue(unit, out BuffRuntimeData buffs) || buffs == null) return;
            if (effect.DisplayBuffId > 0 && !buffs.TryGetBuffByBuffId(effect.DisplayBuffId, out _))
            {
                buffs.ApplyBuff(new TalentDisplayBuff(effect.DisplayBuffId));
            }
            // 面板数值：光环加成挂到单位 StatRuntimeData（战斗结算仍走 aura 表，避免双算）
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
            for (int i = 0; i < characterEffects.Count; i++)
            {
                if (AcquiredBy(characterEffects[i], unit)) ids.Add(characterEffects[i].Id);
            }
            for (int i = 0; i < enemyEffects.Count; i++)
            {
                if (AcquiredBy(enemyEffects[i], unit)) ids.Add(enemyEffects[i].Id);
            }
            return ids;
        }

        private static bool IsSourceAlive(BattleUnitState source)
        {
            return source != null && source.IsAlive && source.Vital == UnitVitalState.Alive;
        }

        private float SumAura(string unitId, bool energyRecharge)
        {
            if (string.IsNullOrEmpty(unitId) || !auras.TryGetValue(unitId, out List<AuraEntry> list)) return 0f;
            float sum = 0f;
            for (int i = 0; i < list.Count; i++)
            {
                sum += energyRecharge ? list[i].EnergyRecharge : list[i].DamageBonus;
            }
            return sum;
        }

        // 撤掉某来源在所有单位上的光环（效果注销时用）。
        private void RemoveAllAurasForSource(string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId)) return;
            var emptyUnits = new List<string>();
            foreach (KeyValuePair<string, List<AuraEntry>> pair in auras)
            {
                pair.Value.RemoveAll(entry => entry.SourceId == sourceId);
                if (pair.Value.Count == 0) emptyUnits.Add(pair.Key);
            }
            for (int i = 0; i < emptyUnits.Count; i++) auras.Remove(emptyUnits[i]);
        }
    }
}
