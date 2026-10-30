using System.Collections.Generic;
using Core.Gear;
using UnityEngine;

namespace Framework.Gameplay.Player
{
    /// <summary>
    /// 全局编队管理器：六套编队（每套 8 槽）与当前选中编队索引的唯一入口与唯一存档者，变更即保存。
    /// 槽位以 unitId（如 CH0221）占位，空串 = 未编队；同一角色在同套编队内不可重复。
    /// 编队业务规则（名单来源、进战斗校验）在上层面板与战斗入口；本类只做数据读写与防御。
    /// </summary>
    public class FormationTeamManager
    {
        // 存档相对 Data/ 的子目录
        private const string SaveFolder = "PlayerData/Formation/";

        // 存档文件名（不含扩展名）
        private const string SaveFileName = "Teams";

        private static FormationTeamManager instance;
        public static FormationTeamManager Instance => instance ??= new FormationTeamManager();

        // 编队存档数据
        private FormationTeamData data;

        private FormationTeamManager()
        {
            LoadData();
        }

        #region 查询

        /// <summary>当前选中编队索引（0 起）。</summary>
        public int CurrentTeamIndex => data.currentTeamIndex;

        /// <summary>
        /// 取一套编队的 8 槽原始数据（槽位序；空串 = 未编队）。
        /// </summary>
        /// <param name="teamIndex">编队索引（0 起）。</param>
        /// <returns>槽位副本（8 元素），调用方可安全改写；索引非法返回全空槽。</returns>
        public string[] GetTeamSlots(int teamIndex)
        {
            if (!IsValidTeam(teamIndex)) return NewEmptySlots();
            return (string[])data.teams[teamIndex].Clone();
        }

        /// <summary>
        /// 取一套编队的角色列表（去空槽、按槽位序）。
        /// </summary>
        /// <param name="teamIndex">编队索引（0 起）。</param>
        /// <returns>角色 id 列表；索引非法返回空列表。</returns>
        public List<string> GetTeamUnitIds(int teamIndex)
        {
            List<string> unitIds = new List<string>();
            if (!IsValidTeam(teamIndex)) return unitIds;

            string[] slots = data.teams[teamIndex];
            for (int i = 0; i < slots.Length; i++)
            {
                if (!string.IsNullOrEmpty(slots[i])) unitIds.Add(slots[i]);
            }
            return unitIds;
        }

        /// <summary>
        /// 查角色在某套编队中的槽位。
        /// </summary>
        /// <param name="teamIndex">编队索引（0 起）。</param>
        /// <param name="unitId">角色 id。</param>
        /// <returns>槽位下标（0 起）；未编入或索引非法返回 -1。</returns>
        public int IndexOfUnit(int teamIndex, string unitId)
        {
            if (!IsValidTeam(teamIndex) || string.IsNullOrEmpty(unitId)) return -1;

            string[] slots = data.teams[teamIndex];
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == unitId) return i;
            }
            return -1;
        }

        #endregion

        #region 变更

        /// <summary>
        /// 设置当前选中编队索引。
        /// </summary>
        /// <param name="teamIndex">编队索引（0 起）。</param>
        /// <returns>成功返回 true；索引非法返回 false 且无副作用。</returns>
        public bool SetCurrentTeamIndex(int teamIndex)
        {
            if (!IsValidTeam(teamIndex))
            {
                Debug.LogError($"[FormationTeamManager] 编队索引非法：{teamIndex}");
                return false;
            }

            if (data.currentTeamIndex == teamIndex) return true;
            data.currentTeamIndex = teamIndex;
            SaveData();
            return true;
        }

        /// <summary>
        /// 整队覆写（编辑面板提交）：按传入顺序填充槽位，不足 8 槽补空；同套内重复角色拒绝。
        /// </summary>
        /// <param name="teamIndex">编队索引（0 起）。</param>
        /// <param name="unitIds">编队角色（按槽位序，可空/null 表示清空该套）。</param>
        /// <returns>成功返回 true；索引非法、超槽或重复返回 false 且无副作用。</returns>
        public bool SetTeam(int teamIndex, IReadOnlyList<string> unitIds)
        {
            if (!IsValidTeam(teamIndex))
            {
                Debug.LogError($"[FormationTeamManager] 编队索引非法：{teamIndex}");
                return false;
            }
            if (unitIds != null && unitIds.Count > FormationTeamData.TeamSlotCount)
            {
                Debug.LogError($"[FormationTeamManager] 编队角色数超槽位上限：{unitIds.Count} > {FormationTeamData.TeamSlotCount}");
                return false;
            }

            //重复校验：同一角色在同套编队内不可重复
            HashSet<string> seen = new HashSet<string>();
            if (unitIds != null)
            {
                for (int i = 0; i < unitIds.Count; i++)
                {
                    string unitId = unitIds[i];
                    if (string.IsNullOrEmpty(unitId)) continue;
                    if (!seen.Add(unitId))
                    {
                        Debug.LogError($"[FormationTeamManager] 编队重复角色：unitId={unitId}");
                        return false;
                    }
                }
            }

            string[] slots = NewEmptySlots();
            if (unitIds != null)
            {
                for (int i = 0; i < unitIds.Count; i++)
                {
                    slots[i] = unitIds[i] ?? string.Empty;
                }
            }
            data.teams[teamIndex] = slots;
            SaveData();
            return true;
        }

        /// <summary>
        /// 交换一套编队内的两个槽位（拖拽换位）。
        /// </summary>
        /// <param name="teamIndex">编队索引（0 起）。</param>
        /// <param name="slotA">槽位下标 A。</param>
        /// <param name="slotB">槽位下标 B。</param>
        /// <returns>成功返回 true；索引非法返回 false 且无副作用。</returns>
        public bool TrySwapSlots(int teamIndex, int slotA, int slotB)
        {
            if (!IsValidTeam(teamIndex) || !IsValidSlot(slotA) || !IsValidSlot(slotB) || slotA == slotB)
            {
                Debug.LogError($"[FormationTeamManager] 槽位交换参数非法：team={teamIndex}, {slotA} <-> {slotB}");
                return false;
            }

            (data.teams[teamIndex][slotA], data.teams[teamIndex][slotB]) =
                (data.teams[teamIndex][slotB], data.teams[teamIndex][slotA]);
            SaveData();
            return true;
        }

        #endregion

        #region 持久化

        /// <summary>
        /// 将当前编队数据序列化到本地文件。
        /// </summary>
        public void SaveData()
        {
            BinaryDataManager.Instance.Save(SaveFolder, SaveFileName, data);
        }

        /// <summary>
        /// 从本地文件加载编队数据，文件不存在或反序列化失败时使用空编队。
        /// </summary>
        public void LoadData()
        {
            data = BinaryDataManager.Instance.Load<FormationTeamData>(SaveFolder, SaveFileName) ?? new FormationTeamData();

            // 防御外部或旧档产生的脏数据
            Normalize();
        }

        #endregion

        #region Private

        /// <summary>编队索引是否有效。</summary>
        private static bool IsValidTeam(int teamIndex)
        {
            return teamIndex >= 0 && teamIndex < FormationTeamData.TeamCount;
        }

        /// <summary>槽位下标是否有效。</summary>
        private static bool IsValidSlot(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < FormationTeamData.TeamSlotCount;
        }

        /// <summary>全空槽位（8 元素空串）。</summary>
        private static string[] NewEmptySlots()
        {
            string[] slots = new string[FormationTeamData.TeamSlotCount];
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = string.Empty;
            }
            return slots;
        }

        /// <summary>
        /// 加载后清理：编队套数/槽位数对齐契约，剔除非法与重复条目（同套重复保留首个）。
        /// </summary>
        private void Normalize()
        {
            data.currentTeamIndex = Mathf.Clamp(data.currentTeamIndex, 0, FormationTeamData.TeamCount - 1);

            //套数补齐或裁剪
            while (data.teams.Count < FormationTeamData.TeamCount) data.teams.Add(NewEmptySlots());
            if (data.teams.Count > FormationTeamData.TeamCount) data.teams.RemoveRange(FormationTeamData.TeamCount, data.teams.Count - FormationTeamData.TeamCount);

            for (int i = 0; i < data.teams.Count; i++)
            {
                string[] slots = data.teams[i];

                //槽位数补齐或裁剪；null 槽位统一空串
                if (slots == null)
                {
                    slots = NewEmptySlots();
                }
                else if (slots.Length != FormationTeamData.TeamSlotCount)
                {
                    string[] normalized = NewEmptySlots();
                    for (int j = 0; j < normalized.Length && j < slots.Length; j++)
                    {
                        normalized[j] = slots[j] ?? string.Empty;
                    }
                    slots = normalized;
                }
                else
                {
                    for (int j = 0; j < slots.Length; j++)
                    {
                        slots[j] ??= string.Empty;
                    }
                }

                //同套重复角色保留首个，其余置空
                HashSet<string> seen = new HashSet<string>();
                for (int j = 0; j < slots.Length; j++)
                {
                    if (string.IsNullOrEmpty(slots[j]) || seen.Add(slots[j])) continue;
                    slots[j] = string.Empty;
                }

                data.teams[i] = slots;
            }
        }

        #endregion
    }
}
