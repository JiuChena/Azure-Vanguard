using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 世界侧部署子服务：编队卡拖放的幽灵预览、区域/障碍/角色距离校验、
    /// 提交部署与已放置名单；屏幕侧拖拽由 HUD 面板负责。
    /// </summary>
    public sealed class CombatDeployService : ICombatSubservice
    {
        // 障碍球检测复用缓冲（避免拖动逐帧分配）。
        private static readonly Collider[] DeployOverlapBuffer = new Collider[32];

        // 所属战斗服务（单位查询 / SubmitDeploy）。
        private CombatService service;

        // 视图桥（幽灵预览复用战斗视图 prefab 加载）。
        private CombatViewService views;

        // 场景配置（区域/层/幽灵参数）。
        private CombatBattleConfig config;

        // 已放置角色（不可重放；生命周期 = 一场战斗）。
        private readonly HashSet<string> deployedUnitIds = new HashSet<string>();

        // 当前拖动角色 id（null = 未拖动）。
        private string draggingUnitId;

        // 幽灵预览实例。
        private GameObject dragPreview;

        // 拖动序号（作废挂起的异步首载）。
        private int dragSequence;

        // 放置判定调试态（拖动中逐帧刷新）：最近判定点/是否全过/Gizmos 着色用。
        private Vector3 debugCheckPoint;
        private bool debugCheckDeployable;
        private string debugCheckReason = "未拖动";

        // 幽灵材质实例缓存（Apply 填充 / Destroy 清空）：不可放置着色按「基色×色调」回写。
        private readonly List<Material> ghostMaterials = new List<Material>();
        private readonly List<Color> ghostBaseColors = new List<Color>();

        /// <summary>当前拖动角色 id（null = 未拖动；Gizmos 用）。</summary>
        public string DraggingUnitId => draggingUnitId;

        /// <summary>最近一次放置判定点（Gizmos 用）。</summary>
        public Vector3 DebugCheckPoint => debugCheckPoint;

        /// <summary>最近一次放置校验是否通过（Gizmos 用）。</summary>
        public bool DebugCheckDeployable => debugCheckDeployable;

        /// <summary>绑定战斗服务、视图桥与场景配置（重复调用先清空旧状态）。</summary>
        /// <param name="combatService">所属战斗服务；不允许为 null。</param>
        /// <param name="viewService">视图桥；不允许为 null。</param>
        /// <param name="battleConfig">场景配置；不允许为 null。</param>
        public void Begin(CombatService combatService, CombatViewService viewService, CombatBattleConfig battleConfig)
        {
            if (combatService == null) throw new System.ArgumentNullException(nameof(combatService));
            if (viewService == null) throw new System.ArgumentNullException(nameof(viewService));
            if (battleConfig == null) throw new System.ArgumentNullException(nameof(battleConfig));

            Reset();
            service = combatService;
            views = viewService;
            config = battleConfig;
        }

        /// <summary>取消拖动、销毁幽灵预览、清空已放置名单与调试态。</summary>
        public void Reset()
        {
            CleanupDrag();
            deployedUnitIds.Clear();
            debugCheckPoint = Vector3.zero;
            debugCheckDeployable = false;
            debugCheckReason = "未拖动";
            service = null;
            views = null;
            config = null;
        }

        /// <summary>该角色是否已放置过（放置过的卡片不可再次拖出）。</summary>
        /// <param name="unitId">角色 id。</param>
        /// <returns>已放置返回 true。</returns>
        public bool IsUnitDeployed(string unitId)
        {
            return !string.IsNullOrEmpty(unitId) && deployedUnitIds.Contains(unitId);
        }

        /// <summary>
        /// 开始拖动编队卡：按 id 拉起战斗视图同款幽灵预览（异步首载，失败仅无预览不挡放置）。
        /// 重复调用（已在拖动中）忽略；拖动已结束则丢弃迟到的预览。
        /// </summary>
        /// <param name="unitId">角色 id。</param>
        public async void BeginDeployDrag(string unitId)
        {
            if (service == null || !service.IsBattleActive || string.IsNullOrEmpty(unitId) || draggingUnitId != null)
                return;

            draggingUnitId = unitId;
            debugCheckReason = "等待地面映射";
            int seq = ++dragSequence;
            CombatBattleConfig activeConfig = config;

            GameObject prefab = await views.LoadViewPrefabAsync(unitId);
            if (seq != dragSequence || prefab == null || draggingUnitId == null || activeConfig == null) return;

            dragPreview = UnityEngine.Object.Instantiate(prefab, prefab.transform.position,
                Quaternion.identity, activeConfig.ViewRoot);
            dragPreview.name = $"DeployPreview_{unitId}";
            StripGhostBehaviours(dragPreview);
            ApplyGhostMaterials(dragPreview);
        }

        /// <summary>
        /// 拖动中更新幽灵预览位置并刷新判定调试态；判定结果变化时落一条日志（逐帧去重）。
        /// </summary>
        /// <param name="worldPoint">HUD 射线落到战场后的世界点。</param>
        public void UpdateDeployDrag(Vector3 worldPoint)
        {
            if (dragPreview != null) dragPreview.transform.position = worldPoint;

            string previousReason = debugCheckReason;
            EvaluateDeploy(worldPoint, out debugCheckReason);
            if (debugCheckReason != previousReason)
            {
                Debug.Log($"[CombatDeploy] 放置判定：{debugCheckReason}（point={worldPoint}）");
            }
            if (dragPreview != null) SetGhostInvalid(!debugCheckDeployable);
        }

        /// <summary>
        /// 结束拖动并尝试放置：校验通过才 SubmitDeploy；失败/被拒返回 false，卡片保留可再拖。
        /// </summary>
        /// <param name="worldPoint">落点世界坐标。</param>
        /// <returns>成功提交部署返回 true。</returns>
        public bool EndDeployDrag(Vector3 worldPoint)
        {
            string unitId = draggingUnitId;
            bool deployable = EvaluateDeploy(worldPoint, out string reason);
            CleanupDrag();
            if (unitId == null || service == null) return false;

            if (!deployable)
            {
                Debug.Log($"[CombatDeploy] '{unitId}' 放置校验未通过：{reason}（point={worldPoint}）");
                return false;
            }
            if (!service.SubmitDeploy(unitId, worldPoint))
            {
                Debug.LogWarning($"[CombatDeploy] '{unitId}' 部署被拒（点数不足/名额/白名单），卡片保留。");
                return false;
            }
            deployedUnitIds.Add(unitId);
            return true;
        }

        /// <summary>取消拖动（玩家把卡片放回）：只清理预览，不提交放置。</summary>
        public void CancelDeployDrag()
        {
            CleanupDrag();
        }

        /// <summary>执行一次放置校验（不改变拖动状态）。</summary>
        /// <param name="point">待判定世界点。</param>
        /// <returns>可放置返回 true。</returns>
        public bool IsDeployable(Vector3 point)
        {
            return EvaluateDeploy(point, out _);
        }

        // 拖动状态清理：作废挂起的首载 + 销毁幽灵预览与材质实例。
        private void CleanupDrag()
        {
            draggingUnitId = null;
            dragSequence++;
            if (dragPreview != null)
            {
                DestroyGhostMaterials(dragPreview);
                UnityEngine.Object.Destroy(dragPreview);
                dragPreview = null;
            }
        }

        // 放置校验全量执行：四关逐关短路，原因与判定点落调试态。
        private bool EvaluateDeploy(Vector3 point, out string reason)
        {
            debugCheckPoint = point;
            debugCheckDeployable = false;

            if (service == null || !service.IsBattleActive || config == null
                || config.DeployAreaPointA == null || config.DeployAreaPointB == null)
            {
                reason = service == null || !service.IsBattleActive
                    ? "战斗未就绪（控制器未装配）"
                    : "部署区域角点未配置（deployAreaPointA/B）";
                return false;
            }

            // ① 区域盒（分量取两角点最小/最大，含边界）
            Vector3 min = Vector3.Min(config.DeployAreaPointA.position, config.DeployAreaPointB.position);
            Vector3 max = Vector3.Max(config.DeployAreaPointA.position, config.DeployAreaPointB.position);
            if (point.x < min.x || point.x > max.x
                || point.y < min.y || point.y > max.y
                || point.z < min.z || point.z > max.z)
            {
                reason = $"区域外（盒 {min} ~ {max}，点 {point}）";
                return false;
            }

            // ② 可放置地面层白名单：判定点半径内必须命中可放置层（mask 空 = 跳过本关）
            if (config.PlaceableGroundLayers.value != 0
                && Physics.OverlapSphereNonAlloc(
                    point, config.DeployCheckRadius, DeployOverlapBuffer,
                    config.PlaceableGroundLayers.value) == 0)
            {
                reason = $"半径 {config.DeployCheckRadius} 内未命中可放置地面层（点不在有效放置面上）";
                return false;
            }

            // ③ 不可放置层障碍球检测：半径内命中即拒
            int hitCount = Physics.OverlapSphereNonAlloc(
                point, config.DeployCheckRadius, DeployOverlapBuffer, config.UnputableLayers.value);
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = DeployOverlapBuffer[i];
                if (hit != null)
                {
                    reason = $"半径 {config.DeployCheckRadius} 内命中不可放置层物体：{hit.name}";
                    return false;
                }
            }

            // ④ 与已部署角色过近（同半径）；敌人不查（设计允许放进敌人堆）
            float radiusSq = config.DeployCheckRadius * config.DeployCheckRadius;
            IReadOnlyList<BattleUnitState> units = service.Units;
            for (int i = 0; i < units.Count; i++)
            {
                BattleUnitState unit = units[i];
                if (unit.Camp != BattleCamp.Character || !unit.IsAlive) continue;
                if ((unit.Position - point).sqrMagnitude <= radiusSq)
                {
                    reason = $"离角色 {unit.UnitId} 过近（≤{config.DeployCheckRadius}）";
                    return false;
                }
            }

            reason = "可放置";
            debugCheckDeployable = true;
            return true;
        }

        // 幽灵预览去逻辑化（纯视觉外壳）：销毁全部用户脚本 + NavMeshAgent/Obstacle + Rigidbody。
        private void StripGhostBehaviours(GameObject root)
        {
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                behaviour.enabled = false;
                UnityEngine.Object.Destroy(behaviour);
            }
            foreach (NavMeshAgent agent in root.GetComponentsInChildren<NavMeshAgent>(true))
            {
                agent.enabled = false;
                UnityEngine.Object.Destroy(agent);
            }
            foreach (NavMeshObstacle obstacle in root.GetComponentsInChildren<NavMeshObstacle>(true))
            {
                obstacle.enabled = false;
                UnityEngine.Object.Destroy(obstacle);
            }
            foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>(true))
            {
                UnityEngine.Object.Destroy(body);
            }
        }

        // 幽灵化预览材质：实例化材质、开 _GHOST_ON + 透明混合，关投影并禁用 Collider。
        private void ApplyGhostMaterials(GameObject root)
        {
            ghostMaterials.Clear();
            ghostBaseColors.Clear();
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.materials;
                bool ghosted = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material mat = materials[i];
                    if (mat == null) continue;
                    // 无 _Color 的材质（如粒子）不可主色着色，跳过缓存防 get_color 报错
                    if (mat.HasProperty("_Color"))
                    {
                        ghostMaterials.Add(mat);
                        ghostBaseColors.Add(mat.color);
                    }
                    mat.EnableKeyword("_GHOST_ON");
                    mat.SetFloat("_GhostAlpha", config.GhostAlpha);
                    mat.SetFloat("_BlendSrc", 5f);
                    mat.SetFloat("_BlendDst", 10f);
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    ghosted = true;
                }
                if (ghosted) renderer.materials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        // 不可放置着色：幽灵材质主色 × 红色调（恢复可放置写回基色）。
        private void SetGhostInvalid(bool invalid)
        {
            for (int i = 0; i < ghostMaterials.Count; i++)
            {
                Material mat = ghostMaterials[i];
                if (mat == null) continue;
                mat.color = invalid ? ghostBaseColors[i] * config.GhostInvalidTint : ghostBaseColors[i];
            }
        }

        // 销毁幽灵预览：.materials getter 产生的材质实例是孤本，须显式 Destroy 防泄漏。
        private static void DestroyGhostMaterials(GameObject root)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material mat in renderer.materials)
                {
                    if (mat != null) UnityEngine.Object.Destroy(mat);
                }
            }
        }
    }
}
