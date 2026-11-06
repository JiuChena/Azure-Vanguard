using System;
using UnityEngine;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 战斗指令子服务：部署/移动/复活三类指令的校验、扣费、流水记账与事件外发。
    /// 指令流水是 ⑤④→③ 的唯一通道，被拒指令同样记账（seq 不断号）。
    /// </summary>
    public sealed class CombatCommandService : ICombatSubservice
    {
        // 复活费用占位公式：base × mult^已复活次数（📐 挂账 #29，数值待拍板）。
        private const int ReviveBaseCost = 20;
        private const int ReviveCostMultiplier = 2;

        // 所属战斗服务。
        private CombatService service;

        /// <summary>指令流水账本。</summary>
        public BattleCommandLog CommandLog { get; } = new BattleCommandLog();

        /// <summary>上场名额 = 编队白名单大小（倒地不空名额）。</summary>
        public int MaxDeployed => service?.DataPack != null ? service.DataPack.DeployCosts.Count : 0;

        /// <summary>绑定战斗服务（重复调用先清空流水）。</summary>
        /// <param name="combatService">所属战斗服务；不允许为 null。</param>
        public void Begin(CombatService combatService)
        {
            Reset();
            service = combatService;
        }

        /// <summary>清空指令流水。</summary>
        public void Reset()
        {
            CommandLog.Clear();
            service = null;
        }

        /// <summary>部署角色：点数 + 编队白名单 + 名额 + 重复上场四重校验，通过则扣费注册。</summary>
        /// <param name="unitId">角色 id。</param>
        /// <param name="position">部署点。</param>
        /// <returns>部署成功返回 true。</returns>
        public bool SubmitDeploy(string unitId, Vector3 position)
        {
            if (service == null || !service.IsBattleActive || service.DataPack == null) return false;
            BattleDataPack data = service.DataPack;

            if (!data.DeployCosts.TryGetValue(unitId, out int cost))
                return RecordReject(BattleCommandType.Deploy, unitId, position, "未在编队白名单");

            if (service.Roster.DeployedCharacterCount >= MaxDeployed)
                return RecordReject(BattleCommandType.Deploy, unitId, position, "上场名额已满");

            if (service.Roster.FindCharacter(unitId) != null)
                return RecordReject(BattleCommandType.Deploy, unitId, position, "该角色已上场");

            if (!service.Resources.TrySpend(cost))
                return RecordReject(BattleCommandType.Deploy, unitId, position, "点数不足");

            var state = new BattleUnitState(unitId, BattleCamp.Character, data.Stats[unitId], position);
            service.Roster.AddCharacter(state);

            // 技能 CD 入场注册：E 初始满 CD（部署即开始冷却，首次放等冷却完）
            if (state.Stats.SkillEId > 0
                && BattleDataModule.TryGetSkillCooldown(state.Stats.SkillEId, out float eCooldown))
            {
                service.Resources.Cooldowns.StartCooldown(state, state.Stats.SkillEId, eCooldown);
            }

            CommandLog.Append(BattleCommandType.Deploy, unitId, position, BattleCommandResult.Applied, null, cost);
            service.RaiseUnitSpawned(state);
            service.RefreshSnapshot();
            return true;
        }

        /// <summary>点选移动：只写目的地意图（免费，寻路归表现层执行）。</summary>
        /// <param name="unitId">角色 id。</param>
        /// <param name="position">目标点。</param>
        /// <returns>指令接受返回 true。</returns>
        public bool SubmitMove(string unitId, Vector3 position)
        {
            if (service == null || !service.IsBattleActive) return false;
            BattleUnitState unit = service.Roster.FindCharacter(unitId);
            if (unit == null || unit.IsDowned)
                return RecordReject(BattleCommandType.Move, unitId, position, "单位不存在或已倒地");

            unit.MoveDestination = position;
            CommandLog.Append(BattleCommandType.Move, unitId, position, BattleCommandResult.Applied, null, 0);
            service.RefreshSnapshot();
            return true;
        }

        /// <summary>复活倒地角色：按递增公式扣费，满血复活。</summary>
        /// <param name="unitId">角色 id。</param>
        /// <returns>复活成功返回 true。</returns>
        public bool SubmitRevive(string unitId)
        {
            if (service == null || !service.IsBattleActive) return false;
            BattleUnitState unit = service.Roster.FindCharacter(unitId);
            if (unit == null || !unit.IsDowned)
                return RecordReject(BattleCommandType.Revive, unitId, Vector3.zero, "单位不在倒地状态");

            int cost = GetReviveCost(unit);
            if (!service.Resources.TrySpend(cost))
                return RecordReject(BattleCommandType.Revive, unitId, Vector3.zero, "点数不足");

            unit.Hp = unit.Stats.MaxHp;
            unit.Vital = UnitVitalState.Alive;
            unit.ReviveCount++;
            CommandLog.Append(BattleCommandType.Revive, unitId, Vector3.zero, BattleCommandResult.Applied, null, cost);
            service.RaiseUnitRevived(unit);
            service.RefreshSnapshot();
            return true;
        }

        /// <summary>查询部署费用（HUD 槽位费用展示用）。</summary>
        /// <param name="unitId">角色 id。</param>
        /// <param name="cost">部署费用。</param>
        /// <returns>在编队白名单内返回 true。</returns>
        public bool TryGetDeployCost(string unitId, out int cost)
        {
            cost = 0;
            return service?.DataPack != null && service.DataPack.DeployCosts.TryGetValue(unitId, out cost);
        }

        /// <summary>复活费用：base × mult^已复活次数（递增）。</summary>
        /// <param name="unit">倒地角色。</param>
        /// <returns>费用。</returns>
        public int GetReviveCost(BattleUnitState unit)
        {
            if (unit == null) return 0;
            return ReviveBaseCost * (int)Math.Pow(ReviveCostMultiplier, unit.ReviveCount);
        }

        // 记一条被拒指令（seq 占号不断号）并返回 false。
        private bool RecordReject(BattleCommandType type, string unitId, Vector3 position, string reason)
        {
            CommandLog.Append(type, unitId, position, BattleCommandResult.Rejected, reason, 0);
            service?.RefreshSnapshot();
            return false;
        }
    }
}
