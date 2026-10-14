using UnityEngine;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 关卡配置（SO）：一场战斗的静态参数——守点/点数经济/波次序列。
    /// 资产放 Assets/Configurations/Battle/，入 Addressables 组 Configuration-Battle（地址=levelId）。
    /// 胜负策略经 evaluatorAsset（GuardPointEvaluatorAsset 引用）关卡级注入，切片 2 随胜负系统补字段。
    /// </summary>
    [CreateAssetMenu(menuName = "AzureVanguard/Battle/LevelConfig", fileName = "LV_New")]
    public sealed class LevelConfig : ScriptableObject
    {
        [Header("Guard Point")]
        [Tooltip("守点最大生命")]
        [SerializeField] private int guardPointMaxHp = 1000;

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
        [Tooltip("波次推进顺序（WaveTable 主键，有序）")]
        [SerializeField] private string[] waveIds = { "W0001", "W0002" };

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

        // 波次推进顺序（只读；返回内部数组引用前先判空，调用方不得改写）
        public string[] WaveIds => waveIds;

        // 胜负策略资产（关卡级注入；null 时装配层兜底 new GuardPointEvaluator）
        public GuardPointEvaluatorAsset EvaluatorAsset => evaluatorAsset;
    }
}
