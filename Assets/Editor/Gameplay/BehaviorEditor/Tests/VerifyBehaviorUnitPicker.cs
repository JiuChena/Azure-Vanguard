using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using BehaviorEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

/// <summary>验证单位行为收集、配对、切换与原有导出路径的独立编辑器脚本。</summary>
public static class VerifyBehaviorUnitPicker
{
    // 仅访问项目编辑器程序集内的非公开成员，兼容 UnityCLI 临时编译程序集。
    private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    #region Verification

    /// <summary>执行断言；临时资产和对象在 finally 中清理，不使用用户行为资产。</summary>
    /// <returns>逐项 PASS/FAIL 与汇总计数。</returns>
    public static string Run()
    {
        // 保存编辑器选择，创建完全隔离的临时资源集合。
        var log = new StringBuilder();
        int passed = 0;
        int failed = 0;
        var temporary = new List<Object>();
        Object[] selection = Selection.objects;
        string folder = "Assets/Editor/BehaviorPickerVerify_" + Guid.NewGuid().ToString("N");
        ScriptableObject window = null;
        Scene previewScene = default;
        Type collection = typeof(EditorWindow).Assembly.GetType("BehaviorEditor.BehaviorUnitAssetCollection");
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            if (collection == null) collection = assembly.GetType("BehaviorEditor.BehaviorUnitAssetCollection");
        if (collection == null) return "FAIL: 新收集器尚未编译到项目编辑器程序集。";
        Type windowType = collection.Assembly.GetType("BehaviorEditor.BehaviorEditorWindow");
        Action<bool, string> check = (condition, label) =>
        {
            // 使用独立期望值记录每项结果。
            if (condition) passed++;
            else failed++;
            log.AppendLine((condition ? "PASS " : "FAIL ") + label);
        };
        try
        {
            // 合成同名、大小写差异、缺失、重复引用及冲突场景。
            TimelineAsset idle = ScriptableObject.CreateInstance<TimelineAsset>();
            idle.name = "Idle";
            BehaviorClip idleClip = ScriptableObject.CreateInstance<BehaviorClip>();
            idleClip.name = "Idle";
            TimelineAsset move = ScriptableObject.CreateInstance<TimelineAsset>();
            move.name = "Move";
            BehaviorClip orphan = ScriptableObject.CreateInstance<BehaviorClip>();
            orphan.name = "Orphan";
            BehaviorClip lower = ScriptableObject.CreateInstance<BehaviorClip>();
            lower.name = "idle";
            TimelineAsset duplicate = ScriptableObject.CreateInstance<TimelineAsset>();
            duplicate.name = "Idle";
            BehaviorClip duplicateClip = ScriptableObject.CreateInstance<BehaviorClip>();
            duplicateClip.name = "Idle";
            temporary.AddRange(new Object[] { idle, idleClip, move, orphan, lower, duplicate, duplicateClip });
            MethodInfo build = collection.GetMethod("BuildPairs", Members);
            IList pairs = (IList)build.Invoke(null, new object[] { new Object[] { idle, idleClip, idle, null, move, orphan, lower } });
            check(pairs.Count == 4, "重复引用去重、空引用忽略、大小写不合并");
            object idlePair = pairs[0];
            check((string)Get(idlePair, "Name") == "Idle" && (string)Get(pairs[3], "Name") == "idle", "配对按精确名称稳定排序");
            check(((IList)Get(idlePair, "Timelines")).Count == 1 && ((IList)Get(idlePair, "Clips")).Count == 1, "同名 Timeline 与 SO 自动配对");
            check(((IList)Get(pairs[1], "Clips")).Count == 0, "仅 Timeline 的缺失 SO 组保留");
            check(((IList)Get(pairs[2], "Timelines")).Count == 0, "仅 SO 的缺失 Timeline 组保留");
            check(((IList)build.Invoke(null, new object[] { null })).Count == 0, "空引用集合安全处理");
            IList conflicts = (IList)build.Invoke(null, new object[] { new Object[] { idle, duplicate, idleClip } });
            object conflictPair = conflicts[0];
            check((bool)conflictPair.GetType().GetProperty("HasConflict", Members).GetValue(conflictPair), "不同 Timeline 同名时标记冲突");
            conflicts = (IList)build.Invoke(null, new object[] { new Object[] { idle, idleClip, duplicateClip } });
            check((bool)conflicts[0].GetType().GetProperty("HasConflict", Members).GetValue(conflicts[0]), "不同 SO 同名时标记冲突");

            // 通过含自循环的配置 SO 验证序列化数组遍历与循环保护。
            BehaviorPickerReferenceFixture fixture = ScriptableObject.CreateInstance<BehaviorPickerReferenceFixture>();
            fixture.references = new Object[] { idle, idleClip, fixture, idle };
            temporary.Add(fixture);
            MethodInfo collect = collection.GetMethod("Collect", Members);
            IList collected = (IList)collect.Invoke(null, new object[] { fixture, null });
            check(collected.Count == 2 && collected.Contains(idle) && collected.Contains(idleClip), "配置数组引用收集、重复去重、自循环终止");
            var config = ScriptableObject.CreateInstance<Framework.Gameplay.Abilities.UnitConfig>();
            temporary.Add(config);
            config.BehaviorClips.Add(idleClip);
            check(((IList)collect.Invoke(null, new object[] { config, null })).Contains(idleClip), "项目 UnitConfig 行为列表 SO 引用可收集");

            // 临时单位的非激活子节点也应被收集，其他单位不在遍历范围。
            previewScene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("BehaviorPickerVerifyRoot") { hideFlags = HideFlags.HideInHierarchy };
            SceneManager.MoveGameObjectToScene(root, previewScene);
            temporary.Add(root);
            var child = new GameObject("InactiveDirector") { hideFlags = HideFlags.HideInHierarchy };
            child.transform.SetParent(root.transform);
            child.SetActive(false);
            PlayableDirector director = child.AddComponent<PlayableDirector>();
            director.playableAsset = move;
            collected = (IList)collect.Invoke(null, new object[] { root, null });
            check(collected.Count == 1 && collected.Contains(move), "非激活子层级的 Director Timeline 可收集");
            check(((IList)collect.Invoke(null, new object[] { director, null })).Contains(move), "目标为 Component 时收集其单位层级");
            check(director.playableAsset == move && root.GetComponent<PlayableDirector>() == null, "收集不创建组件、不改变 Director 绑定");
            check(((IList)collect.Invoke(null, new object[] { null, null })).Count == 0, "没有单位和目录时返回空集合");
            check(((IList)collect.Invoke(null, new object[] { null, "Assets" })).Count == 0, "拒绝扫描 Assets 根目录");

            // 临时单位目录验证两种资产过滤及子目录递归，绝不使用真实单位资产。
            AssetDatabase.CreateFolder("Assets/Editor", folder.Substring("Assets/Editor/".Length));
            AssetDatabase.CreateFolder(folder, "Timelines");
            AssetDatabase.CreateFolder(folder, "BehaviorClips");
            AssetDatabase.CreateAsset(idle, folder + "/Timelines/Idle.playable");
            AssetDatabase.CreateAsset(idleClip, folder + "/BehaviorClips/Idle.asset");
            collected = (IList)collect.Invoke(null, new object[] { null, folder });
            check(collected.Count == 2 && collected.Contains(idle) && collected.Contains(idleClip), "显式单位目录递归收集 Timeline 和 SO");
            collected = (IList)collect.Invoke(null, new object[] { fixture, folder });
            check(collected.Count == 2, "配置与目录发现同一资产时只保留一次");

            // 快捷切换只填入原有表单，未开始编辑也未写入资产。
            window = ScriptableObject.CreateInstance(windowType);
            Set(window, "behaviorTargetUnit", root);
            MethodInfo select = windowType.GetMethod("SelectUnitBehaviorPair", Members);
            select.Invoke(window, new[] { idlePair });
            check((Object)Get(window, "sourceTimeline") == idle && (Object)Get(window, "targetBehaviorClip") == idleClip, "快捷选择填入原 Source / Target 字段");
            check((Object)Get(window, "previewReferenceRoot") == root, "场景单位自动填入 Reference Root");
            check(root.GetComponent<PlayableDirector>() == null && Get(window, "authoringSessionContext") == null, "快捷选择不开始会话、不创建预览组件");
            select.Invoke(window, new[] { conflictPair });
            check((Object)Get(window, "sourceTimeline") == idle, "重名冲突拒绝快捷选择");
            select.Invoke(window, new[] { pairs[2] });
            check((Object)Get(window, "targetBehaviorClip") == idleClip, "仅 SO 组拒绝快捷选择，防止复用旧 Timeline");
            select.Invoke(window, new[] { pairs[1] });
            check((Object)Get(window, "sourceTimeline") == move && Get(window, "targetBehaviorClip") == null &&
                (string)Get(window, "outputAssetName") == "Move", "缺少 SO 时清空旧目标并填入同名输出名称");
            Type sessionType = collection.Assembly.GetType("BehaviorEditor.BehaviorAuthoringSessionContext");
            Set(window, "authoringSessionContext", Activator.CreateInstance(sessionType, Members, null, new object[] { move, director, root }, null));
            select.Invoke(window, new[] { idlePair });
            check((Object)Get(window, "sourceTimeline") == move && Get(window, "targetBehaviorClip") == null, "活动会话拒绝切换 Timeline 和 SO");
            Set(window, "authoringSessionContext", null);

            // 旧手动导出到明确 SO，以及目标为空时创建资产，两条路径保持可用。
            Set(window, "sourceTimeline", idle);
            Set(window, "targetBehaviorClip", idleClip);
            Set(window, "previewReferenceRoot", null);
            windowType.GetMethod("ExportToBehaviorClip", Members).Invoke(window, null);
            check((Object)Get(window, "targetBehaviorClip") == idleClip && idleClip.playbackSettings != null, "原手动指定 SO 的导出路径通过");
            Set(window, "targetBehaviorClip", null);
            Set(window, "outputFolder", folder + "/BehaviorClips");
            Set(window, "outputAssetName", "CreatedByManualExport");
            windowType.GetMethod("ExportToBehaviorClip", Members).Invoke(window, null);
            check(AssetDatabase.LoadAssetAtPath<BehaviorClip>(folder + "/BehaviorClips/CreatedByManualExport.asset") != null, "原未指定 SO 的创建导出路径通过");
        }
        catch (Exception exception)
        {
            // 测试异常作为失败返回，不伪造通过。
            failed++;
            log.AppendLine("FAIL exception: " + (exception.InnerException ?? exception));
        }
        finally
        {
            // 清理仅本次生成的窗口、临时对象与唯一目录，恢复用户选择。
            if (window != null)
            {
                Set(window, "sourceTimeline", null);
                Set(window, "authoringSessionContext", null);
                Object.DestroyImmediate(window);
            }
            for (int i = temporary.Count - 1; i >= 0; i--)
                if (temporary[i] != null && !EditorUtility.IsPersistent(temporary[i])) Object.DestroyImmediate(temporary[i]);
            if (previewScene.IsValid()) EditorSceneManager.ClosePreviewScene(previewScene);
            if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
            Selection.objects = selection;
        }
        return log.AppendLine($"RESULT: {passed} PASS / {failed} FAIL").ToString();
    }

    #endregion

    #region Reflection Helpers

    /// <summary>读取测试目标的非公开字段。</summary>
    /// <param name="target">被测实例。</param>
    /// <param name="field">已知字段名。</param>
    /// <returns>字段值，可能为 null。</returns>
    private static object Get(object target, string field)
    {
        // 只在编辑器测试中访问实现状态。
        return target.GetType().GetField(field, Members).GetValue(target);
    }

    /// <summary>设置测试目标的非公开字段。</summary>
    /// <param name="target">被测实例。</param>
    /// <param name="field">已知字段名。</param>
    /// <param name="value">测试值，允许 null。</param>
    private static void Set(object target, string field, object value)
    {
        // 使用隔离窗口，不改用户打开的导出窗口。
        target.GetType().GetField(field, Members).SetValue(target, value);
    }

    #endregion
}
