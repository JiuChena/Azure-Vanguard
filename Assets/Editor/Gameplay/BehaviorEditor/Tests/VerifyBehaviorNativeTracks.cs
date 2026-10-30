using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using BehaviorEditor;
using Core.Gear;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

/// <summary>隔离验证原生轨道导出、资产重载与执行器生命周期。</summary>
public static class VerifyBehaviorNativeTracks
{
    // 反射仅用于测试访问编辑器内部编译器和运行时图，不进入产品热路径。
    // 池播模型下音频断言走 AudioManager 活跃表反射，见 ActivePoolSources。
    private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    #region 验证
    /// <summary>使用独立预览场景和唯一临时资产验证，不修改真实单位资产。</summary>
    /// <returns>逐项结果和汇总。</returns>
    public static string Run()
    {
        var log = new StringBuilder();
        int passed = 0;
        int failed = 0;
        Action<bool, string> check = (condition, label) =>
        {
            if (condition) passed++; else failed++;
            log.AppendLine((condition ? "PASS " : "FAIL ") + label);
        };
        var temporary = new List<Object>();
        var executors = new List<IBehaviorTrackExecutor>();
        Scene preview = default;
        string folder = "Assets/Editor/NativeTracksVerify_" + Guid.NewGuid().ToString("N");
        BehaviorExecutor scheduler = null;
        try
        {
            // 构造独立的单位及两类原生轨道。
            preview = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("NativeTracksVerify");
            SceneManager.MoveGameObjectToScene(root, preview);
            var child = new GameObject("Effect");
            child.transform.SetParent(root.transform, false);
            var sound = new GameObject("Sound");
            sound.transform.SetParent(root.transform, false);
            AudioSource binding = sound.AddComponent<AudioSource>();
            binding.playOnAwake = false;
            binding.volume = 0.8f;
            AudioClip audioClip = null;
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" }))
            {
                audioClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
                if (audioClip != null && audioClip.length > 1f) break;
            }
            if (audioClip == null) throw new InvalidOperationException("测试需要项目已有音频资源。");
            binding.clip = audioClip;
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            temporary.Add(timeline);
            var activation = timeline.CreateTrack<ActivationTrack>(null, "Enable Effect");
            activation.postPlaybackState = ActivationTrack.PostPlaybackState.Revert;
            TimelineClip a = activation.CreateDefaultClip(); a.start = 1d; a.duration = 2d;
            TimelineClip b = activation.CreateDefaultClip(); b.start = 2d; b.duration = 2d;
            var audio = timeline.CreateTrack<AudioTrack>(null, "Sound Track");
            TimelineClip first = audio.CreateClip(audioClip);
            first.start = 0d; first.duration = 3d; first.clipIn = 0.25d; first.timeScale = 2d;
            first.easeInDuration = 0.5d; first.easeOutDuration = 0.5d;
            first.mixInCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            first.mixOutCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            ((AudioPlayableAsset)first.asset).loop = true;
            TimelineClip second = audio.CreateClip(audioClip);
            second.start = 1d; second.duration = 3d;
            ((AudioPlayableAsset)second.asset).loop = true;
            var extraAudio = timeline.CreateTrack<AudioTrack>(null, "Second Sound Track");
            TimelineClip extra = extraAudio.CreateClip(audioClip);
            extra.start = 0d; extra.duration = 4d;
            ((AudioPlayableAsset)extra.asset).loop = true;
            temporary.AddRange(new Object[] { activation, a.asset, b.asset, audio, first.asset, second.asset, extraAudio, extra.asset });
            SetFloat(audio, "m_TrackProperties.volume", 0.6f);
            SetFloat(audio, "m_TrackProperties.stereoPan", -0.2f);
            SetFloat(audio, "m_TrackProperties.spatialBlend", 0.4f);
            SetFloat(first.asset, "m_ClipProperties.volume", 0.5f);
            PlayableDirector director = root.AddComponent<PlayableDirector>();
            director.playableAsset = timeline;
            director.SetGenericBinding(activation, child);
            director.SetGenericBinding(audio, binding);
            director.SetGenericBinding(extraAudio, binding);

            // 经过真实编译器目录，而不是直接构造输出数据冒充导出测试。
            Assembly editorAssembly = null;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                if (assembly.GetType("BehaviorEditor.BehaviorExportContext") != null) editorAssembly = assembly;
            Type contextType = editorAssembly.GetType("BehaviorEditor.BehaviorExportContext");
            Type catalogType = editorAssembly.GetType("BehaviorEditor.BehaviorTrackCompilerCatalog");
            MethodInfo export = catalogType.GetMethod("TryExport", Members);
            Func<object> makeContext = () => Activator.CreateInstance(contextType,
                new object[] { timeline, director, root.transform, new BehaviorPlaybackSettings() });
            object context = makeContext();
            check((bool)export.Invoke(null, new[] { activation, context }), "目录自动识别 ActivationTrack");
            check((bool)export.Invoke(null, new[] { audio, context }), "目录自动识别 AudioTrack");
            export.Invoke(null, new[] { extraAudio, context });
            var clip = ScriptableObject.CreateInstance<BehaviorClip>();
            temporary.Add(clip);
            contextType.GetMethod("CommitTo", Members).Invoke(context, new object[] { clip });
            ActivationTrackData activationData = clip.GetTrackData<ActivationTrackData>();
            AudioTrackData audioData = clip.GetTrackData<AudioTrackData>();
            check(activationData.tracks[0].targetPath == "Effect" && activationData.tracks[0].intervals.Length == 2, "激活绑定路径和两个区间导出");
            check(activationData.tracks[0].postPlaybackState == ActivationTrack.PostPlaybackState.Revert, "原生结束策略导出");
            check(audioData.tracks[0].targetPath == "Sound" && audioData.tracks[0].segments.Length == 2, "音频绑定和重叠片段保留");
            check(audioData.tracks.Length == 2, "多个原生音频轨道分别导出");
            AudioTrackData.Segment segment = audioData.tracks[0].segments[0];
            check(segment.clip == audioClip && segment.clipIn == 0.25d && segment.timeScale == 2d && segment.loop, "音频资源、裁切、倍速和循环导出");
            check(Mathf.Approximately(audioData.tracks[0].volume, 0.6f) && Mathf.Approximately(segment.volume, 0.5f) &&
                  Mathf.Approximately(audioData.tracks[0].stereoPan, -0.2f) && Mathf.Approximately(audioData.tracks[0].spatialBlend, 0.4f), "隐藏轨道和片段混音参数导出");
            first.mixInCurve.postWrapMode = WrapMode.ClampForever;
            segment.mixInCurve.postWrapMode = WrapMode.ClampForever;
            check(((IList)contextType.GetProperty("Warnings").GetValue(context)).Count == 0, "有效原生轨道无导出警告");

            // 真正落盘后卸载重载，验证 SerializeReference 和音频资产引用。
            AssetDatabase.CreateFolder("Assets/Editor", folder.Substring("Assets/Editor/".Length));
            string assetPath = folder + "/Native.asset";
            AssetDatabase.CreateAsset(clip, assetPath);
            AssetDatabase.SaveAssetIfDirty(clip);
            temporary.Remove(clip);
            Resources.UnloadAsset(clip);
            clip = AssetDatabase.LoadAssetAtPath<BehaviorClip>(assetPath);
            activationData = clip.GetTrackData<ActivationTrackData>();
            audioData = clip.GetTrackData<AudioTrackData>();
            check(activationData != null && audioData != null && audioData.tracks[0].segments[0].clip == audioClip,
                "SO 卸载重载后两类多态数据和音频引用仍有效");
            check(audioData.tracks[0].segments[0].mixInCurve.Evaluate(0.5f) == 0.5f, "淡入曲线序列化保持");

            // 直接驱动真实激活执行器，验证重叠、边界、间隙和所有结束策略。
            var runtimeContext = new BehaviorExecutionContext(root, 1.5f);
            IBehaviorTrackExecutor activeExecutor = activationData.CreateExecutor(runtimeContext);
            executors.Add(activeExecutor);
            child.SetActive(true);
            activeExecutor.Begin(); check(!child.activeSelf, "激活轨道零时刻区间外关闭");
            activeExecutor.Tick(1f); check(child.activeSelf, "激活区间左闭边界");
            activeExecutor.Tick(3f); check(child.activeSelf, "首段结束时重叠片段仍保持激活");
            activeExecutor.Tick(4f); check(!child.activeSelf, "激活区间右开边界");
            activeExecutor.Stop(); check(child.activeSelf, "Revert 恢复初始 activeSelf");
            foreach (ActivationTrack.PostPlaybackState post in Enum.GetValues(typeof(ActivationTrack.PostPlaybackState)))
            {
                activationData.tracks[0].postPlaybackState = post;
                child.SetActive(false);
                activeExecutor.Begin(); activeExecutor.Tick(2f); activeExecutor.Stop(); activeExecutor.Stop();
                bool expected = post == ActivationTrack.PostPlaybackState.Active || post == ActivationTrack.PostPlaybackState.LeaveAsIs;
                check(child.activeSelf == expected, "结束策略和重复停止：" + post);
            }
            activationData.tracks[0].intervals = new[] { new Vector2(0f, 1f), new Vector2(2f, 3f) };
            activeExecutor.Begin(); activeExecutor.Tick(1.5f); check(!child.activeSelf, "不连续激活区间间隙关闭"); activeExecutor.Stop();
            activationData.tracks[0].intervals = Array.Empty<Vector2>();
            child.SetActive(true); activeExecutor.Begin(); check(!child.activeSelf, "空激活轨道关闭对象"); activeExecutor.Stop();

            // 真实音频执行器池播验证：窗口触发、倍速与裁切、空间参数、循环回收与一次性不追停。
            // EditMode 下 AudioManager.EnsureRoot 的 DontDestroyOnLoad 不可用：预置池根，绕过其创建路径。
            Transform poolRoot = new GameObject("AudioManagerVerifyRoot").transform;
            temporary.Add(poolRoot.gameObject);
            typeof(AudioManager).GetField("_root", Members).SetValue(AudioManager.Instance, poolRoot);
            Func<int> activeCount = () => ((IDictionary)typeof(AudioManager)
                .GetField("_activeAudios", Members).GetValue(AudioManager.Instance)).Count;
            int poolBase = activeCount();
            check(AudioDataManager.Instance.Data.soundVolume > 0.05f, "测试依赖全局音效音量非零");
            IBehaviorTrackExecutor audioExecutor = audioData.CreateExecutor(runtimeContext);
            executors.Add(audioExecutor);
            audioExecutor.Begin(); audioExecutor.Tick(1.25f);
            check(activeCount() == poolBase + 3, "窗口内三片段全部经池起播");
            AudioSource speedUp = null;
            int normalSpeed = 0;
            foreach (AudioSource source in ActivePoolSources())
            {
                if (Mathf.Abs(source.pitch - 3f) < 0.01f) speedUp = source;
                else if (Mathf.Abs(source.pitch - 1.5f) < 0.01f) normalSpeed++;
            }
            check(speedUp != null && normalSpeed == 2, "片段倍速乘全局行为速度并落到池源 pitch");
            check(speedUp != null && speedUp.time > 0.2f && speedUp.time < 0.6f, "Clip In 裁切起点生效");
            // 音频轨配了声像与空间混合，第二条音频轨未配走默认值：各轨池源应用各自参数。
            int spatialTrack = 0, spatialDefault = 0;
            foreach (AudioSource source in ActivePoolSources())
            {
                if (Mathf.Approximately(source.spatialBlend, 0.4f) && Mathf.Approximately(source.panStereo, -0.2f)) spatialTrack++;
                else if (Mathf.Approximately(source.spatialBlend, 0f) && Mathf.Approximately(source.panStereo, 0f)) spatialDefault++;
            }
            check(spatialTrack == 2 && spatialDefault == 1, "各轨池源应用各自声像与空间混合");
            float globalVolume = AudioDataManager.Instance.Data.soundVolume;
            var expectedRatios = new[] { 0.3f, 0.6f, 1f };
            var volumes = new List<float>();
            foreach (AudioSource source in ActivePoolSources()) volumes.Add(source.volume);
            volumes.Sort();
            bool volumeOk = volumes.Count == 3;
            for (int index = 0; volumeOk && index < 3; index++)
                volumeOk &= Mathf.Abs(volumes[index] - globalVolume * expectedRatios[index]) < 0.02f;
            check(volumeOk, "池源音量=全局音效音量×轨道×片段");
            audioExecutor.Tick(4f);
            check(activeCount() == poolBase, "循环片段窗口退出即全部回收");
            audioData.tracks[0].segments[0].loop = false;
            audioExecutor.Tick(1f);
            check(activeCount() == poolBase + 3, "窗口重入重新触发池播");
            audioExecutor.Tick(4f);
            check(activeCount() == poolBase + 1, "一次性片段退窗不追停，循环片段回收");
            audioExecutor.Stop(); audioExecutor.Stop();
            check(activeCount() == poolBase + 1, "停止仅回收循环片段且重复停止安全");

            // 缺失绑定、同对象冲突和旧数据移除走同一导出入口。
            director.ClearGenericBinding(audio);
            object missing = makeContext();
            export.Invoke(null, new[] { audio, missing });
            check(((IList)contextType.GetProperty("Warnings").GetValue(missing)).Count == 1, "未绑定音频给出警告");
            contextType.GetMethod("CommitTo", Members).Invoke(missing, new object[] { clip });
            AudioTrackData unboundAudio = clip.GetTrackData<AudioTrackData>();
            check(unboundAudio != null && unboundAudio.tracks.Length == 1 && unboundAudio.tracks[0].targetPath == string.Empty
                  && clip.GetTrackData<ActivationTrackData>() == null, "未绑定音频回退单位根导出，失效激活轨移除");
            var duplicate = timeline.CreateTrack<ActivationTrack>(null, "Duplicate");
            director.SetGenericBinding(duplicate, child);
            object conflict = makeContext();
            export.Invoke(null, new[] { activation, conflict }); export.Invoke(null, new[] { duplicate, conflict });
            check(((IList)contextType.GetProperty("Warnings").GetValue(conflict)).Count == 1, "相同对象多个激活轨道明确提示冲突");
            var outside = new GameObject("OutsideUnit");
            SceneManager.MoveGameObjectToScene(outside, preview);
            director.SetGenericBinding(activation, outside);
            object outsideContext = makeContext(); export.Invoke(null, new[] { activation, outsideContext });
            check(((IList)contextType.GetProperty("Warnings").GetValue(outsideContext)).Count == 1, "单位外绑定拒绝导出并警告");
            var sameName = new GameObject("Effect"); sameName.transform.SetParent(root.transform, false);
            director.SetGenericBinding(activation, child);
            object ambiguous = makeContext(); export.Invoke(null, new[] { activation, ambiguous });
            check(((IList)contextType.GetProperty("Warnings").GetValue(ambiguous)).Count == 1, "同名兄弟造成歧义时拒绝导出");
            Object.DestroyImmediate(sameName);
            director.SetGenericBinding(activation, sound);
            Object.DestroyImmediate(sound);
            activationData.tracks[0].targetPath = "Missing";
            child.SetActive(true); activeExecutor.Begin(); activeExecutor.Tick(2f); activeExecutor.Stop();
            check(root.activeSelf && child.activeSelf, "运行时缺失路径不回退到根对象");

            // 总调度器接入与循环重建，不需要业务侧额外手动执行轨道。
            activationData.tracks[0].targetPath = "Effect";
            activationData.tracks[0].intervals = new[] { new Vector2(0f, 0.5f) };
            activationData.tracks[0].postPlaybackState = ActivationTrack.PostPlaybackState.Revert;
            clip.SetTrackData(activationData);
            clip.playbackSettings.duration = 1f; clip.playbackSettings.wrapMode = WrapMode.Loop;
            scheduler = new BehaviorExecutor(root);
            scheduler.Play(clip); scheduler.Tick(0.75f); check(!child.activeSelf, "总执行器驱动原生激活轨道");
            scheduler.Tick(0.25f); check(child.activeSelf, "行为循环重建并求值零时刻");
            scheduler.Stop();
        }
        catch (Exception exception)
        {
            failed++;
            log.AppendLine("FAIL exception: " + exception);
        }
        finally
        {
            // 先释放播放资源与池化音频，再清理唯一测试目录与预览场景。
            scheduler?.Stop();
            AudioManager.Instance.ReleaseAll();
            foreach (IBehaviorTrackExecutor executor in executors) executor.Stop();
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            foreach (Object item in temporary) if (item != null) Object.DestroyImmediate(item);
            if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
        }
        log.AppendLine($"TOTAL {passed} PASS / {failed} FAIL");
        return log.ToString();
    }
    #endregion

    #region 测试辅助
    /// <summary>读取 AudioManager 当前活跃的池化源快照。</summary>
    private static List<AudioSource> ActivePoolSources()
    {
        var sources = new List<AudioSource>();
        IDictionary actives = (IDictionary)typeof(AudioManager)
            .GetField("_activeAudios", Members).GetValue(AudioManager.Instance);
        foreach (object key in actives.Keys) sources.Add((AudioSource)key);
        return sources;
    }

    /// <summary>读取被测实例的内部状态。</summary>
    /// <param name="instance">被测对象。</param>
    /// <param name="field">内部字段名。</param>
    /// <returns>字段当前值。</returns>
    private static object Get(object instance, string field)
    {
        return instance.GetType().GetField(field, Members).GetValue(instance);
    }

    /// <summary>配置与 Timeline Inspector 同路径的序列化参数。</summary>
    /// <param name="owner">作者资产。</param>
    /// <param name="path">属性完整路径。</param>
    /// <param name="value">测试值。</param>
    private static void SetFloat(Object owner, string path, float value)
    {
        using var serialized = new SerializedObject(owner);
        serialized.FindProperty(path).floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    #endregion
}
