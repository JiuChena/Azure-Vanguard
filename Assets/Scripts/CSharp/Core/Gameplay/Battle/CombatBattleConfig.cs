using System;
using System.Collections.Generic;
using UnityEngine;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 战斗场景配置载体：由场景入口把 Inspector 拖拽的引用与参数一次性交给 CombatService。
    /// 只承载数据，不含逻辑；场景入口保留同名序列化字段用于 Gizmos 与回读。
    /// </summary>
    public sealed class CombatBattleConfig
    {
        // 关卡配置（守点血量/点数经济/胜负策略/StageId）。
        public LevelConfig LevelConfig;

        // 守点 Transform（索敌锚点/守点判定/子弹锁头）。
        public Transform GuardPoint;

        // 单位视图父节点；null = 不挂父级。
        public Transform ViewRoot;

        // 可放置区域角点 A（与 B 对角组成三维包围盒）。
        public Transform DeployAreaPointA;

        // 可放置区域角点 B。
        public Transform DeployAreaPointB;

        // 放置球检测半径（障碍命中 / 已部署角色过近共用）。
        public float DeployCheckRadius = 1f;

        // 不可放置层（障碍物放此层，球检测命中即拒绝放置）。
        public LayerMask UnputableLayers;

        // 可放置地面层白名单（判定点半径内必须命中这些层）。
        public LayerMask PlaceableGroundLayers;

        // 幽灵预览透明度系数。
        public float GhostAlpha = 0.5f;

        // 不可放置时幽灵主色调。
        public Color GhostInvalidTint = new Color(1f, 0.35f, 0.35f);

        // 敌人创建间隔（秒）：同批到点敌人的依次创建节奏。
        public float SpawnInterval = 1.5f;

        // 出怪点绑定：WaveTable.enemyCreatePointId → 场景位置。
        public List<SpawnPointBinding> SpawnPoints = new List<SpawnPointBinding>();

        // 敌人行进路线：WaveTable.routeId → 有序路点。
        public List<EnemyRoute> Routes = new List<EnemyRoute>();
    }

    /// <summary>出怪点绑定：波次表 pointId → 场景 Transform。</summary>
    [Serializable]
    public sealed class SpawnPointBinding
    {
        [Tooltip("出怪点 id（WaveTable.enemyCreatePointId）")]
        public string pointId;

        [Tooltip("出怪点位置（敌人出生 Transform）")]
        public Transform point;
    }

    /// <summary>敌人行进路线：波次行经 routeId 引用，路点为世界坐标有序序列。</summary>
    [Serializable]
    public sealed class EnemyRoute
    {
        [Tooltip("路线 id（WaveTable.routeId 引用）")]
        public string routeId;

        [Tooltip("是否在 Scene 视图绘制本路线（路点小球+相邻连线）")]
        public bool showGizmos = true;

        [Tooltip("路点序列（世界坐标，有序；走完转守点）")]
        public List<Vector3> points = new List<Vector3>();
    }
}
