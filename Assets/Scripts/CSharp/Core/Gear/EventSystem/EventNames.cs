namespace Core.Gear
{
    /// <summary>
    /// 事件名称常量表，所有事件字符串统一在此定义，消除裸字符串拼写错误。
    /// 按使用域分区：Core 为框架基础能力事件，Gameplay 为玩法功能事件。
    /// </summary>
    public static class EventNames
    {
        #region Core

        // 更新控制状态（基础输入/控制链）。
        public const string UpdateControlCH = nameof(UpdateControlCH);
        // 场景异步加载进度（0~1）。
        public const string LoadSceneProgress = nameof(LoadSceneProgress);

        #endregion

        #region Gameplay

        // 玩家死亡。
        public const string PlayerDeath = nameof(PlayerDeath);
        // 敌人死亡。
        public const string EnemyDeath = nameof(EnemyDeath);
        // 血量变化。
        public const string HPChanged = nameof(HPChanged);
        // 能量变化。
        public const string EnergyChanged = nameof(EnergyChanged);
        // 天赋冷却变化。
        public const string TalentCDChanged = nameof(TalentCDChanged);
        // 爆发冷却变化。
        public const string BurstCDChanged = nameof(BurstCDChanged);
        // 背包数据更新。
        public const string BagUpdated = nameof(BagUpdated);
        // 商店购买完成。
        public const string StorePurchased = nameof(StorePurchased);
        // 单位属性更新。
        public const string UnitStatsUpdated = nameof(UnitStatsUpdated);
        // 角色属性更新（兼容旧名，同 UnitStatsUpdated）。
        public const string CharacterStatsUpdated = UnitStatsUpdated;
        // 技能等级更新。
        public const string SkillLevelUpdated = nameof(SkillLevelUpdated);
        // 当前控制单位切换。
        public const string UnitSwitched = nameof(UnitSwitched);
        // 当前控制角色切换（兼容旧名，同 UnitSwitched）。
        public const string CharacterSwitched = UnitSwitched;
        // 单位死亡（携带单位 GameObject）。
        public const string UnitDeath = nameof(UnitDeath);
        // Buff 施加。
        public const string BuffApplied = nameof(BuffApplied);
        // Buff 移除。
        public const string BuffRemoved = nameof(BuffRemoved);
        // 任务接受（携带任务 ID）。
        public const string QuestAccepted = nameof(QuestAccepted);
        // 任务进度更新。
        public const string QuestProgressUpdated = nameof(QuestProgressUpdated);
        // 任务阶段推进（携带任务 ID）。
        public const string QuestStageAdvanced = nameof(QuestStageAdvanced);
        // 任务完成（携带任务 ID）。
        public const string QuestCompleted = nameof(QuestCompleted);
        // 对话开始。
        public const string DialogueStarted = nameof(DialogueStarted);
        // 对话结束。
        public const string DialogueEnded = nameof(DialogueEnded);
        // 进入区域（携带区域 ID）。
        public const string AreaEntered = nameof(AreaEntered);

        #endregion
    }

}