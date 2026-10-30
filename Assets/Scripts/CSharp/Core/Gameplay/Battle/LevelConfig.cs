using UnityEngine;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 关卡配置（SO）：一场战斗的静态参数——守点/点数经济/波次序列。
    /// 资产放 Assets/Configurations/Battle/，入 Addressables 组 Configuration-Battle（地址=levelId）。
    /// 静态参数（守点血量/点数经济/胜负策略）在本 SO 配置；波次序列唯一走 StageTable
    /// （stageId 查表 waveIds，有序）。
    /// 胜负策略经 evaluatorAsset（GuardPointEvaluatorAsset 引用）关卡级注入，切片 2 随胜负系统补字段。
    /// </summary>
    [CreateAssetMenu(menuName = "AzureVanguard/Battle/LevelConfig", fileName = "LV_New")]
    public sealed class LevelConfig : ScriptableObject
    {
        [Header("Guard Point")]
        [Tooltip("守点最大生命（每关自由配置；2026-10-27 口径：不提倡超过 99——敌人每只按 EnemyTable.guardPointDamage 扣点血，守点血小才有破点压力）")]
        [SerializeField] private int guardPointMaxHp = 20;

        [Header("Point Economy")]
        [Tooltip("初始点数（开局立得）")]
        [SerializeField] private int initialPoints = 20;

        [Tooltip("点数供给间隔（秒）")]
        [SerializeField] private float pointSupplyInterval = 2f;

        [Tooltip("每次供给点数")]
        [SerializeField] private int pointSupplyAmount = 1;

        [Tooltip("点数上限（时间供给封顶，服务器可推算任意时刻上限）")]
        [SerializeField] private int pointCap = 50;

        [Header("Waves")]
        [Tooltip("关卡 id（StageTable 主键；波次序列查表 waveIds，有序）")]
        [SerializeField] private string stageId = "LV_0001";

        [Header("Outcome")]
        [Tooltip("胜负策略资产（普通关=守点默认实现；挑战关引用继承变体）")]
        [SerializeField] private GuardPointEvaluatorAsset evaluatorAsset;

        // 守点最大生命
        public int GuardPointMaxHp => guardPointMaxHp;

        // 初始点数
        public int InitialPoints => initialPoints;

        // 点数供给间隔（秒）
        public float PointSupplyInterval => pointSupplyInterval;

        // 每次供给点数
        public int PointSupplyAmount => pointSupplyAmount;

        // 点数上限
        public int PointCap => pointCap;

        // 关卡 id（StageTable 主键，波次序列查表）
        public string StageId => stageId;

        // 胜负策略资产（关卡级注入；null 时装配层兜底 new GuardPointEvaluator）
        public GuardPointEvaluatorAsset EvaluatorAsset => evaluatorAsset;
    }
}
