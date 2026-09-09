#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BehaviorEditor
{
    /// <summary>
    /// Behavior 正式 AnimatorController 创建工具。
    /// 提供低约束的正式创建流程：在任意目录创建或刷新一个自包含 Controller，并可直接分配到 Animator。
    /// </summary>
    internal sealed class BehaviorAnimatorControllerSetupWindow : EditorWindow
    {
        private string outputFolder = "Assets/Animations/Behavior";
        private string controllerName = "BehaviorController";
        private int layerCount = BehaviorAnimatorControllerConvention.DefaultLayerCount;
        private int slotsPerLayer = BehaviorAnimatorControllerConvention.DefaultSlotsPerLayer;

        [MenuItem("Framework/Behavior Editor/Animator Controller Setup")]
        private static void Open()
        {
            BehaviorAnimatorControllerSetupWindow window =
                GetWindow<BehaviorAnimatorControllerSetupWindow>("Behavior Controller");
            window.minSize = new Vector2(520f, 520f);
        }

        /// <summary>
        /// 资源右键菜单入口：在当前选中文件夹下创建自包含的 Behavior AnimatorController。
        /// </summary>
        [MenuItem("Assets/Create/Framework/BehaviorEditor/Authoring/Animator Controller", priority = 305)]
        private static void CreateControllerFromAssetsMenu()
        {
            // 解析目标目录并生成唯一资产路径。
            string targetFolder = ResolveSelectedFolderPath();
            string uniqueControllerPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{targetFolder}/BehaviorController.controller");
            string controllerName = Path.GetFileNameWithoutExtension(uniqueControllerPath);
            string outputFolder = Path.GetDirectoryName(uniqueControllerPath)?.Replace("\\", "/") ?? targetFolder;

            // 创建或刷新 Controller 并选中。
            AnimatorController controller = BehaviorAnimatorControllerAssetUtility.CreateOrUpdateController(
                outputFolder,
                controllerName,
                BehaviorAnimatorControllerConvention.DefaultLayerCount,
                BehaviorAnimatorControllerConvention.DefaultSlotsPerLayer);

            Selection.activeObject = controller;
            EditorGUIUtility.PingObject(controller);
            Debug.Log($"已创建 Behavior AnimatorController：{AssetDatabase.GetAssetPath(controller)}", controller);
        }

        /// <summary>
        /// 绘制窗口主界面：说明与创建表单。
        /// </summary>
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Behavior AnimatorController", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "正式方案下，Behavior 的前 N 个 Animator Layer 作为保留槽位层使用。" +
                "这个工具只负责生成或刷新槽位外壳与占位动画；真正的行为内容仍然由项目侧行为配置与 BehaviorClip 决定。" +
                "它不会删除无关层，也不会触碰不匹配 Behavior 命名规则的其他状态。",
                MessageType.Info);

            GUILayout.Space(8f);
            DrawCreationSection();
        }

        /// <summary>
        /// 绘制创建参数表单：目录、名称、层数与每层槽位数。
        /// </summary>
        private void DrawCreationSection()
        {
            EditorGUILayout.LabelField("Create Or Refresh", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "会在目标目录下生成一个自包含 Controller 和它自己的占位动画文件夹。" +
                "你可以按角色、按类别、按实验版本自由创建多个，不区分“通用”或“专属”。",
                MessageType.None);

            outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
            controllerName = EditorGUILayout.TextField("Controller Name", controllerName);
            layerCount = Mathf.Max(1, EditorGUILayout.IntField("Layer Count", layerCount));
            slotsPerLayer = Mathf.Clamp(EditorGUILayout.IntField("Slots Per Layer", slotsPerLayer), 1, 32);

            if (GUILayout.Button("Create Or Refresh Controller", GUILayout.Height(32f)))
            {
                AnimatorController controller = BehaviorAnimatorControllerAssetUtility.CreateOrUpdateController(
                    outputFolder,
                    controllerName,
                    layerCount,
                    slotsPerLayer);
                Selection.activeObject = controller;
                EditorGUIUtility.PingObject(controller);
            }
        }

        /// <summary>
        /// 解析当前选中资源所在文件夹；选中无效时回退到 Assets 根目录。
        /// </summary>
        /// <returns>目标文件夹路径。</returns>
        private static string ResolveSelectedFolderPath()
        {
            UnityEngine.Object activeObject = Selection.activeObject;
            if (activeObject == null)
                return "Assets";

            string assetPath = AssetDatabase.GetAssetPath(activeObject);
            if (string.IsNullOrWhiteSpace(assetPath))
                return "Assets";

            // 选中本身是文件夹时直接使用。
            if (AssetDatabase.IsValidFolder(assetPath))
                return assetPath;

            // 选中是资产时取其所在目录。
            string folderPath = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
            return string.IsNullOrWhiteSpace(folderPath) ? "Assets" : folderPath;
        }
    }

    /// <summary>
    /// Behavior AnimatorController 的资产创建工具：生成自包含 Controller、占位动画与槽位状态。
    /// </summary>
    internal static class BehaviorAnimatorControllerAssetUtility
    {
        /// <summary>
        /// 创建或刷新指定目录下的自包含 Behavior AnimatorController。
        /// </summary>
        /// <param name="outputFolder">输出目录。</param>
        /// <param name="controllerName">Controller 名称。</param>
        /// <param name="layerCount">保留层数。</param>
        /// <param name="slotsPerLayer">每层槽位数。</param>
        /// <returns>创建或刷新后的 AnimatorController。</returns>
        public static AnimatorController CreateOrUpdateController(
            string outputFolder,
            string controllerName,
            int layerCount,
            int slotsPerLayer)
        {
            outputFolder = EnsureFolder(string.IsNullOrWhiteSpace(outputFolder)
                ? BehaviorAnimatorControllerConvention.DefaultSharedControllerFolder
                : outputFolder);
            controllerName = string.IsNullOrWhiteSpace(controllerName)
                ? BehaviorAnimatorControllerConvention.DefaultSharedControllerName
                : controllerName.Trim();
            layerCount = Mathf.Max(1, layerCount);
            slotsPerLayer = Mathf.Clamp(slotsPerLayer, 1, 32);

            string controllerPath = $"{outputFolder}/{controllerName}.controller";
            string placeholderFolder = EnsureFolder($"{outputFolder}/{controllerName}_Placeholders");

            // 已存在则复用，否则新建。
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

            // 为每个槽位生成占位动画片段。
            AnimationClip[,] placeholders = new AnimationClip[layerCount, slotsPerLayer];
            for (int layer = 0; layer < layerCount; layer++)
            {
                for (int slot = 0; slot < slotsPerLayer; slot++)
                    placeholders[layer, slot] = CreatePlaceholderClip(placeholderFolder, layer, slot);
            }

            EnsureBehaviorLayers(controller, placeholders, layerCount, slotsPerLayer);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"Behavior AnimatorController 已准备完成。\n" +
                $"Controller: {controllerPath}\n" +
                $"Placeholder Folder: {placeholderFolder}",
                controller);

            return controller;
        }

        /// <summary>
        /// 确保 Controller 拥有指定数量的 Behavior 保留层与槽位状态。
        /// </summary>
        /// <param name="controller">目标 AnimatorController。</param>
        /// <param name="placeholders">槽位占位动画二维数组。</param>
        /// <param name="layerCount">需要的层数。</param>
        /// <param name="slotsPerLayer">每层槽位数。</param>
        private static void EnsureBehaviorLayers(
            AnimatorController controller,
            AnimationClip[,] placeholders,
            int layerCount,
            int slotsPerLayer)
        {
            List<AnimatorControllerLayer> layers = new List<AnimatorControllerLayer>(controller.layers);
            if (layers.Count == 0)
                layers.Add(CreateControllerLayer(controller, 0));

            // 逐层复用已有层或新建层，并同步槽位状态。
            for (int layerIndex = 0; layerIndex < layerCount; layerIndex++)
            {
                AnimatorControllerLayer layer;
                if (layerIndex < layers.Count)
                {
                    layer = layers[layerIndex];
                    if (layer.stateMachine == null)
                    {
                        layer.stateMachine = new AnimatorStateMachine
                        {
                            name = layerIndex == 0 ? "Base Layer StateMachine" : $"BehaviorLayer{layerIndex}StateMachine"
                        };
                        AssetDatabase.AddObjectToAsset(layer.stateMachine, controller);
                    }
                }
                else
                {
                    layer = CreateControllerLayer(controller, layerIndex);
                    layers.Add(layer);
                }

                if (string.IsNullOrWhiteSpace(layer.name))
                    layer.name = layerIndex == 0 ? "Base Layer" : $"Layer {layerIndex}";
                SyncBehaviorStates(layer.stateMachine, placeholders, layerIndex, slotsPerLayer);
                layers[layerIndex] = layer;
            }

            controller.layers = layers.ToArray();
        }

        /// <summary>
        /// 创建指定索引的 AnimatorControllerLayer 及其状态机。
        /// </summary>
        /// <param name="controller">目标 AnimatorController。</param>
        /// <param name="layerIndex">层索引。</param>
        /// <returns>新建的层。</returns>
        private static AnimatorControllerLayer CreateControllerLayer(AnimatorController controller, int layerIndex)
        {
            AnimatorStateMachine stateMachine = new AnimatorStateMachine
            {
                name = layerIndex == 0 ? "Base Layer StateMachine" : $"BehaviorLayer{layerIndex}StateMachine"
            };
            AssetDatabase.AddObjectToAsset(stateMachine, controller);

            return new AnimatorControllerLayer
            {
                name = layerIndex == 0 ? "Base Layer" : $"Layer {layerIndex}",
                defaultWeight = 1f,
                stateMachine = stateMachine,
                blendingMode = AnimatorLayerBlendingMode.Override
            };
        }

        /// <summary>
        /// 同步层内 Behavior 槽位状态：清理越界/重复状态，补齐缺失槽位并绑定占位动画。
        /// </summary>
        /// <param name="stateMachine">目标状态机。</param>
        /// <param name="placeholders">槽位占位动画二维数组。</param>
        /// <param name="layerIndex">层索引。</param>
        /// <param name="slotsPerLayer">每层槽位数。</param>
        private static void SyncBehaviorStates(
            AnimatorStateMachine stateMachine,
            AnimationClip[,] placeholders,
            int layerIndex,
            int slotsPerLayer)
        {
            // 收集符合命名规则的既有槽位状态，剔除越界与重复项。
            Dictionary<int, AnimatorState> existingStates = new Dictionary<int, AnimatorState>();
            ChildAnimatorState[] childStates = stateMachine.states;

            for (int i = childStates.Length - 1; i >= 0; i--)
            {
                AnimatorState state = childStates[i].state;
                if (state == null)
                    continue;

                if (!TryParseBehaviorSlotStateName(state.name, layerIndex, out int slotIndex))
                    continue;

                if (slotIndex < 0 || slotIndex >= slotsPerLayer || existingStates.ContainsKey(slotIndex))
                {
                    stateMachine.RemoveState(state);
                    continue;
                }

                existingStates.Add(slotIndex, state);
            }

            // 补齐缺失槽位并绑定占位动画，槽位 0 设为默认状态。
            for (int slotIndex = 0; slotIndex < slotsPerLayer; slotIndex++)
            {
                if (!existingStates.TryGetValue(slotIndex, out AnimatorState state) || state == null)
                {
                    state = stateMachine.AddState(
                        BehaviorAnimatorControllerConvention.GetStateName(layerIndex, slotIndex));
                    existingStates[slotIndex] = state;
                }

                state.name = BehaviorAnimatorControllerConvention.GetStateName(layerIndex, slotIndex);
                state.motion = placeholders[layerIndex, slotIndex];
                if (slotIndex == 0)
                    stateMachine.defaultState = state;
            }
        }

        /// <summary>
        /// 从状态名解析 Behavior 槽位索引。
        /// </summary>
        /// <param name="stateName">状态名。</param>
        /// <param name="layerIndex">期望的层索引。</param>
        /// <param name="slotIndex">输出的槽位索引。</param>
        /// <returns>状态名符合命名规则时返回 true。</returns>
        private static bool TryParseBehaviorSlotStateName(string stateName, int layerIndex, out int slotIndex)
        {
            slotIndex = -1;
            if (string.IsNullOrWhiteSpace(stateName))
                return false;

            string prefix = $"L{layerIndex}_Segment_";
            if (!stateName.StartsWith(prefix, StringComparison.Ordinal))
                return false;

            string suffix = stateName.Substring(prefix.Length);
            return int.TryParse(suffix, out slotIndex);
        }

        /// <summary>
        /// 创建或复用指定槽位的占位动画片段资产。
        /// </summary>
        /// <param name="placeholderFolder">占位动画目录。</param>
        /// <param name="layerIndex">层索引。</param>
        /// <param name="slotIndex">槽位索引。</param>
        /// <returns>占位动画片段。</returns>
        private static AnimationClip CreatePlaceholderClip(string placeholderFolder, int layerIndex, int slotIndex)
        {
            string placeholderName = BehaviorAnimatorControllerConvention.GetPlaceholderClipName(layerIndex, slotIndex);
            string clipPath = $"{placeholderFolder}/{placeholderName}.anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip != null)
                return clip;

            // 不存在时创建空占位片段。
            clip = new AnimationClip
            {
                name = placeholderName
            };
            AssetDatabase.CreateAsset(clip, clipPath);
            return clip;
        }

        /// <summary>
        /// 确保文件夹存在，缺失时逐级创建。
        /// </summary>
        /// <param name="folderPath">目标文件夹路径。</param>
        /// <returns>规范化后的文件夹路径。</returns>
        private static string EnsureFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                folderPath = "Assets";

            folderPath = folderPath.Replace("\\", "/").TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folderPath))
                return folderPath;

            // 逐级创建缺失的文件夹。
            string[] parts = folderPath.Split('/');
            string currentPath = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string nextPath = $"{currentPath}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(nextPath))
                    AssetDatabase.CreateFolder(currentPath, parts[i]);

                currentPath = nextPath;
            }

            return folderPath;
        }
    }
}
#endif
