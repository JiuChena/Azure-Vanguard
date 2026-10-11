#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;

namespace UI.Editor
{
    /// <summary>
    /// TMP 字体批量替换窗口（Tools/TMP Font Replacer）。
    /// 指定目标物体（场景物体或预制体资产均可）与目标 TMP_FontAsset，递归收集其下全部 TMP_Text
    /// 组件（含未激活节点与嵌套预制体；TMP_Dropdown/TMP_InputField 的内嵌文本同为 TMP_Text，自动覆盖），
    /// 扫描预览分类后一键替换。预制体资产经 LoadPrefabContents 写入落盘并回读校验；
    /// 场景物体走 Undo 记录，Ctrl+Z 可整批撤销。注意：替换会把文本共享材质重置为新字体的默认材质
    /// （TMP 的 font 赋值行为），原字体专属材质预设不保留。
    /// </summary>
    internal sealed class TmpFontReplacerWindow : EditorWindow
    {
        /// <summary>单个 TMP_Text 的判定类别。</summary>
        private enum FontAction
        {
            Replace,     // 需替换（当前字体与目标不同）
            Same,        // 已是目标字体
            MissingFont, // 当前未设置字体
        }

        /// <summary>单个 TMP_Text 的扫描记录。</summary>
        private sealed class Entry
        {
            public TMP_Text Text;       // 场景目标为实时组件；预制体资产扫描时为资产组件（只读展示）
            public string Name;         // 组件所在节点名
            public string Path;         // 相对目标物体的节点路径
            public string CurrentFont;  // 当前字体名（未设置为 "(无)"）
            public FontAction Action;
        }

        // 目标物体：场景物体或预制体资产均可。
        private GameObject _target;
        // 目标字体：其下全部 TMP_Text 将换成该字体。
        private TMP_FontAsset _font;
        // 最近一次扫描/替换后的结果列表（目标或字体变更后需重新扫描）。
        private List<Entry> _entries;
        // 结果列表滚动位置。
        private Vector2 _scroll;
        // 汇总行文案。
        private string _summary = "指定目标物体与字体后点击「扫描」查看替换范围。";

        /// <summary>打开 TMP 字体替换窗口。</summary>
        [MenuItem("Tools/TMP Font Replacer")]
        private static void Open()
        {
            TmpFontReplacerWindow window = GetWindow<TmpFontReplacerWindow>("TMP Font Replacer");
            window.minSize = new Vector2(480f, 380f);
        }

        /// <summary>绘制窗口主界面：输入、操作按钮与结果列表。</summary>
        private void OnGUI()
        {
            EditorGUILayout.LabelField("TMP Font Replacer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "递归收集目标物体下全部 TMP_Text（含未激活与嵌套预制体），一键换成指定字体。" +
                "替换会把文本共享材质重置为新字体默认材质；预制体资产直接落盘，操作前建议先提交版本控制。",
                MessageType.Info);

            _target = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("目标物体", "场景物体或预制体资产，其下全部 TMP_Text 均在替换范围"), _target, typeof(GameObject), true);
            _font = (TMP_FontAsset)EditorGUILayout.ObjectField(
                new GUIContent("目标字体", "替换用的 TMP_FontAsset"), _font, typeof(TMP_FontAsset), false);
            if (GUILayout.Button("目标字体设为项目默认字体", EditorStyles.miniButton))
                _font = TMP_Settings.defaultFontAsset;

            EditorGUILayout.Space(8f);
            using (new EditorGUI.DisabledScope(_target == null || _font == null))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("扫描（不修改）", GUILayout.Height(28f)))
                        Scan();
                    if (GUILayout.Button("扫描并替换", GUILayout.Height(28f)))
                        Replace();
                }
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(_summary, EditorStyles.wordWrappedMiniLabel);

            if (_entries == null || _entries.Count == 0)
                return;

            EditorGUILayout.Space(4f);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (Entry entry in _entries)
                DrawEntry(entry);
            EditorGUILayout.EndScrollView();
        }

        /// <summary>绘制单条扫描结果：按类别着色的节点行 + 路径行。</summary>
        private static void DrawEntry(Entry entry)
        {
            if (entry.Text == null)
                return;   // 目标为场景物体且场景已变时组件可能已销毁

            Color cache = GUI.color;
            switch (entry.Action)
            {
                case FontAction.Replace:
                    GUI.color = new Color(0.55f, 1f, 0.55f);
                    break;
                case FontAction.MissingFont:
                    GUI.color = new Color(1f, 0.8f, 0.5f);
                    break;
                default:
                    GUI.color = new Color(0.75f, 0.75f, 0.75f);
                    break;
            }

            EditorGUILayout.LabelField($"[{ActionLabel(entry.Action)}] {entry.Name}（当前：{entry.CurrentFont}）");
            GUI.color = cache;
            EditorGUILayout.LabelField($"    {entry.Path}", EditorStyles.miniLabel);
        }

        /// <summary>类别的中文标签。</summary>
        private static string ActionLabel(FontAction action)
        {
            switch (action)
            {
                case FontAction.Replace: return "待替换";
                case FontAction.Same: return "已是目标字体";
                case FontAction.MissingFont: return "未设置字体";
                default: return action.ToString();
            }
        }

        /// <summary>
        /// 扫描目标物体下全部 TMP_Text 并分类写入 _entries 与 _summary，不修改任何数据。
        /// </summary>
        private void Scan()
        {
            if (_target == null || _font == null)
            {
                _summary = "请先指定目标物体与目标字体。";
                return;
            }

            BuildEntries(Collect(_target));
            _summary = $"扫描到 {_entries.Count} 个 TMP_Text："
                + $"待替换 {Count(FontAction.Replace)}｜已是目标字体 {Count(FontAction.Same)}｜未设置字体 {Count(FontAction.MissingFont)}";
        }

        /// <summary>
        /// 替换入口：预制体资产走 LoadPrefabContents 落盘流程，场景物体走 Undo 直改。
        /// 完成后重新扫描刷新列表与汇总。
        /// </summary>
        private void Replace()
        {
            if (_target == null || _font == null)
            {
                _summary = "请先指定目标物体与目标字体。";
                return;
            }

            if (EditorUtility.IsPersistent(_target))
                ReplacePrefabAsset();
            else
                ReplaceSceneObjects();
        }

        /// <summary>
        /// 预制体资产替换：载入预制体内容 → 换字体 → 落盘 → 回读校验（残留必须为 0）。
        /// </summary>
        private void ReplacePrefabAsset()
        {
            string path = AssetDatabase.GetAssetPath(_target);
            int replaced = 0;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (text.font == _font)
                        continue;
                    text.font = _font;
                    EditorUtility.SetDirty(text);
                    replaced++;
                }
                if (replaced > 0)
                    PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            // 回读校验：重载落盘资产，任何 TMP_Text 都应已是目标字体。
            int residual = 0;
            GameObject verify = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (TMP_Text text in verify.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.font != _font)
                    residual++;
            }

            Scan();
            if (residual == 0)
            {
                _summary = $"替换完成：{replaced} 个 TMP_Text 已写入预制体并落盘，回读校验通过（残留 0）。";
                Debug.Log($"[TMP Font Replacer] {path} 替换 {replaced} 个 TMP_Text → {_font.name}，校验通过。", _font);
            }
            else
            {
                _summary = $"替换异常：回读仍有 {residual} 个 TMP_Text 未命中目标字体，请检查 Console。";
                Debug.LogError($"[TMP Font Replacer] {path} 替换后回读缺失（残留 {residual}）。", _font);
            }
        }

        /// <summary>
        /// 场景物体替换：Undo 记录后直改，Ctrl+Z 可整批撤销；替换后标记场景脏。
        /// </summary>
        private void ReplaceSceneObjects()
        {
            List<TMP_Text> texts = Collect(_target);

            List<Object> toRecord = new List<Object>();
            foreach (TMP_Text text in texts)
            {
                if (text != null && text.font != _font)
                    toRecord.Add(text);
            }

            int replaced = 0;
            if (toRecord.Count > 0)
            {
                Undo.RecordObjects(toRecord.ToArray(), "TMP Font Replace");
                foreach (Object recorded in toRecord)
                {
                    ((TMP_Text)recorded).font = _font;
                    EditorUtility.SetDirty(recorded);
                    replaced++;
                }
                EditorSceneManager.MarkSceneDirty(_target.scene);
            }

            Scan();
            _summary = replaced > 0
                ? $"替换完成：{replaced} 个 TMP_Text（场景内修改，Undo 可撤销）。"
                : "没有需要替换的 TMP_Text。";
        }

        /// <summary>递归收集目标下全部 TMP_Text（含未激活节点与嵌套预制体）。</summary>
        private static List<TMP_Text> Collect(GameObject root)
        {
            return new List<TMP_Text>(root.GetComponentsInChildren<TMP_Text>(true));
        }

        /// <summary>扫描结果构建：逐组件判定类别并记录相对路径。</summary>
        private void BuildEntries(List<TMP_Text> texts)
        {
            _entries = new List<Entry>(texts.Count);
            foreach (TMP_Text text in texts)
            {
                if (text == null)
                    continue;

                FontAction action = text.font == null ? FontAction.MissingFont
                    : text.font == _font ? FontAction.Same : FontAction.Replace;
                _entries.Add(new Entry
                {
                    Text = text,
                    Name = text.name,
                    Path = GetPath(text.transform, _target.transform),
                    CurrentFont = text.font != null ? text.font.name : "(无)",
                    Action = action,
                });
            }
        }

        /// <summary>类别计数。</summary>
        private int Count(FontAction action)
        {
            int total = 0;
            foreach (Entry entry in _entries)
            {
                if (entry.Action == action)
                    total++;
            }
            return total;
        }

        /// <summary>节点相对目标物体的路径。</summary>
        private static string GetPath(Transform node, Transform root)
        {
            string path = node.name;
            while (node.parent != null && node != root)
            {
                node = node.parent;
                path = node.name + "/" + path;
            }
            return path;
        }
    }
}
#endif
