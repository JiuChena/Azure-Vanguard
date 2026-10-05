#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;

namespace Art.Editor
{
    /// <summary>
    /// 预制体 Sprite 批量注入图集工具。
    /// 递归收集预制体（含嵌套预制体、未激活节点）全部 Sprite 引用，与图集现有 packables
    /// 做精确/整图/文件夹三层去重后，把新增条目按 YAML 手术追加写入 .spriteatlasv2 并强制
    /// Reimport 重打包。不走 SpriteAtlasExtensions.Add()——该 API 只改内存态不落盘且不去重
    /// （2026-09-22 实证，见知识库 debugging/spriteatlas-api-add-not-persisted）。
    /// </summary>
    internal sealed class SpriteAtlasInjectorWindow : EditorWindow
    {
        // 可注入的贴图源扩展名白名单。
        private static readonly HashSet<string> ImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".tga", ".psd", ".tif", ".tiff", ".exr", ".bmp",
        };

        // packables 条目行：- {fileID: N, guid: G, type: T}。
        private static readonly Regex EntryRegex = new Regex(
            @"^\s*-\s*\{\s*fileID:\s*(-?\d+)\s*,\s*guid:\s*([0-9a-fA-F]{32})\s*,\s*type:\s*\d+\s*\}",
            RegexOptions.Compiled);

        // packables 头行（含空列表形态 packables: []）。
        private static readonly Regex HeaderRegex = new Regex(
            @"^(\s*)packables:(\s*\[\s*\])?\s*$",
            RegexOptions.Compiled);

        /// <summary>单条引用的注入判定类别。</summary>
        private enum EntryAction
        {
            Add,              // 新增
            AlreadyPacked,    // 图集已有同一 packable
            CoveredByTexture, // 所属整图已在图集
            CoveredByFolder,  // 所在文件夹已在图集
            SkipExternal,     // 引用不在 Assets 下（引擎内置/包内资源）
            SkipNonImage,     // 源不是可打包的贴图文件
        }

        /// <summary>单条引用的判定结果。</summary>
        private sealed class Entry
        {
            public string Name;      // 展示名
            public string Path;      // 源资产路径
            public string Guid;      // packables 写入用 guid
            public long FileId;      // packables 写入用 fileID
            public EntryAction Action;
            public string Note;      // 附加说明
        }

        // 待注入预制体。
        private GameObject _prefab;
        // 目标图集。
        private SpriteAtlas _atlas;
        // 多图贴图按整图（fileID 2800000）注入，覆盖全部切帧。
        private bool _wholeSheet;
        // 最近一次分析的判定结果（注入后保留用于展示）。
        private List<Entry> _entries;
        // 结果列表滚动位置。
        private Vector2 _scroll;
        // 汇总行文案。
        private string _summary = "指定预制体和图集后点击「分析」查看收集结果。";

        /// <summary>打开图集注入窗口。</summary>
        [MenuItem("Tools/Sprite Atlas Injector")]
        private static void Open()
        {
            SpriteAtlasInjectorWindow window = GetWindow<SpriteAtlasInjectorWindow>("Sprite Atlas Injector");
            window.minSize = new Vector2(480f, 420f);
        }

        /// <summary>绘制窗口主界面：输入、操作按钮与结果列表。</summary>
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Sprite Atlas Injector", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "递归收集预制体全部 Sprite 引用，去重后追加进目标图集 packables，并强制 Reimport 重打包。" +
                "写入是文件级手术（Add() API 不落盘），操作前请先提交或备份图集文件。",
                MessageType.Info);

            _prefab = (GameObject)EditorGUILayout.ObjectField("预制体", _prefab, typeof(GameObject), false);
            _atlas = (SpriteAtlas)EditorGUILayout.ObjectField("目标图集", _atlas, typeof(SpriteAtlas), false);
            _wholeSheet = EditorGUILayout.Toggle(
                new GUIContent("多图贴图按整图注入", "开启后多图模式贴图按整图（fileID 2800000）注入，覆盖全部切帧；关闭则只注入被引用到的切帧"),
                _wholeSheet);

            EditorGUILayout.Space(8f);
            using (new EditorGUI.DisabledScope(_prefab == null || _atlas == null))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("分析（不写入）", GUILayout.Height(28f)))
                        Analyze();
                    if (GUILayout.Button("分析并注入", GUILayout.Height(28f)))
                    {
                        if (Analyze())
                            WriteIntoAtlas();
                    }
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

        /// <summary>绘制单条判定结果：按类别着色的名称行 + 路径行。</summary>
        private static void DrawEntry(Entry entry)
        {
            Color cache = GUI.color;
            switch (entry.Action)
            {
                case EntryAction.Add:
                    GUI.color = new Color(0.55f, 1f, 0.55f);
                    break;
                case EntryAction.SkipExternal:
                case EntryAction.SkipNonImage:
                    GUI.color = new Color(1f, 0.8f, 0.5f);
                    break;
                default:
                    GUI.color = new Color(0.75f, 0.75f, 0.75f);
                    break;
            }

            string note = string.IsNullOrEmpty(entry.Note) ? string.Empty : $"（{entry.Note}）";
            EditorGUILayout.LabelField($"[{ActionLabel(entry.Action)}] {entry.Name}{note}");
            GUI.color = cache;
            EditorGUILayout.LabelField($"    {entry.Path}", EditorStyles.miniLabel);
        }

        /// <summary>类别的中文标签。</summary>
        private static string ActionLabel(EntryAction action)
        {
            switch (action)
            {
                case EntryAction.Add: return "新增";
                case EntryAction.AlreadyPacked: return "已在图集";
                case EntryAction.CoveredByTexture: return "整图已覆盖";
                case EntryAction.CoveredByFolder: return "文件夹已覆盖";
                case EntryAction.SkipExternal: return "非 Assets 资源";
                case EntryAction.SkipNonImage: return "非贴图资源";
                default: return action.ToString();
            }
        }

        /// <summary>
        /// 收集并判定全部 Sprite 引用，结果写入 _entries 与 _summary。
        /// 返回 false 表示前置条件不满足（未分析成功）。
        /// </summary>
        private bool Analyze()
        {
            if (_prefab == null || _atlas == null)
            {
                _summary = "请先指定预制体和目标图集。";
                return false;
            }

            string atlasPath = AssetDatabase.GetAssetPath(_atlas);
            if (string.IsNullOrEmpty(atlasPath) || !File.Exists(atlasPath))
            {
                _summary = "目标图集不是有效资产文件。";
                return false;
            }

            // 1. 解析图集现有 packables：精确集合 + 整图 guid 集合 + 文件夹路径集合。
            string atlasText = File.ReadAllText(atlasPath);
            List<(string guid, long fileId)> existing = ParsePackableEntries(atlasText, out int unparsedLines);
            HashSet<(string, long)> exactSet = new HashSet<(string, long)>(existing);
            HashSet<string> textureGuids = new HashSet<string>();
            HashSet<string> folderPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach ((string guid, long fileId) in existing)
            {
                if (fileId == 2800000L)
                {
                    textureGuids.Add(guid);
                }
                else if (fileId == 102900000L)
                {
                    string folderPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetDatabase.IsValidFolder(folderPath))
                        folderPaths.Add(folderPath);
                }
            }

            // 2. 递归收集预制体全部组件的 Sprite 字段引用。
            List<Sprite> sprites = CollectSprites(_prefab);

            // 3. 逐条判定。
            List<Entry> results = new List<Entry>();
            HashSet<(string, long)> seenInPrefab = new HashSet<(string, long)>();
            HashSet<string> addedTextures = new HashSet<string>();
            int duplicates = 0;

            void ClassifyObject(string name, string path, string guid, long fileId, string sourceTextureGuid)
            {
                if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new Entry
                    {
                        Name = name, Path = string.IsNullOrEmpty(path) ? "(内置资源)" : path,
                        Guid = guid, FileId = fileId,
                        Action = EntryAction.SkipExternal, Note = "引用不在 Assets 下",
                    });
                    return;
                }

                string extension = Path.GetExtension(path);
                if (!ImageExtensions.Contains(extension))
                {
                    results.Add(new Entry
                    {
                        Name = name, Path = path, Guid = guid, FileId = fileId,
                        Action = EntryAction.SkipNonImage, Note = $"扩展名 {extension} 不在白名单",
                    });
                    return;
                }

                if (exactSet.Contains((guid, fileId)))
                {
                    results.Add(new Entry { Name = name, Path = path, Guid = guid, FileId = fileId, Action = EntryAction.AlreadyPacked });
                    return;
                }

                // 整图覆盖：引用的切帧所属贴图已按整图在图集里。
                if (!string.IsNullOrEmpty(sourceTextureGuid) && textureGuids.Contains(sourceTextureGuid))
                {
                    results.Add(new Entry { Name = name, Path = path, Guid = guid, FileId = fileId, Action = EntryAction.CoveredByTexture, Note = "所属贴图已整图入集" });
                    return;
                }

                // 文件夹覆盖：所在文件夹已作为 packable。
                foreach (string folder in folderPaths)
                {
                    if (path.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(new Entry { Name = name, Path = path, Guid = guid, FileId = fileId, Action = EntryAction.CoveredByFolder, Note = folder });
                        return;
                    }
                }

                results.Add(new Entry { Name = name, Path = path, Guid = guid, FileId = fileId, Action = EntryAction.Add });
            }

            foreach (Sprite sprite in sprites)
            {
                if (sprite == null)
                    continue;

                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long fileId))
                {
                    results.Add(new Entry
                    {
                        Name = sprite.name, Path = AssetDatabase.GetAssetPath(sprite),
                        Guid = null, FileId = 0,
                        Action = EntryAction.SkipNonImage, Note = "无法解析 guid/fileID",
                    });
                    continue;
                }

                // 预制体内去重（同一引用只判定一次）。
                if (!seenInPrefab.Add((guid, fileId)))
                {
                    duplicates++;
                    continue;
                }

                Texture2D sourceTexture = sprite.texture;
                string sourceTextureGuid = null;
                long sourceTextureFileId = 0;
                if (sourceTexture != null)
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sourceTexture, out sourceTextureGuid, out sourceTextureFileId);

                // 整图注入：多图切帧换整图条目，批内按贴图 guid 去重。
                // 判定不用 TextureImporter.spriteMode（本项目引用环境下该成员不可用）：
                // 单图切帧 fileID 恒为 21300000，多图切帧为 sheet 内部 id，以此区分。
                if (_wholeSheet && sourceTexture != null && fileId != 21300000L)
                {
                    if (addedTextures.Add(sourceTextureGuid))
                        ClassifyObject(sourceTexture.name, AssetDatabase.GetAssetPath(sourceTexture), sourceTextureGuid, sourceTextureFileId, null);
                    continue;
                }

                ClassifyObject(sprite.name, AssetDatabase.GetAssetPath(sprite), guid, fileId, sourceTextureGuid);
            }

            int addCount = 0, packedCount = 0, coveredTexture = 0, coveredFolder = 0, skipped = 0;
            foreach (Entry entry in results)
            {
                switch (entry.Action)
                {
                    case EntryAction.Add: addCount++; break;
                    case EntryAction.AlreadyPacked: packedCount++; break;
                    case EntryAction.CoveredByTexture: coveredTexture++; break;
                    case EntryAction.CoveredByFolder: coveredFolder++; break;
                    default: skipped++; break;
                }
            }

            _entries = results;
            _summary = $"引用 {sprites.Count}（预制体内重复合并 {duplicates}）→ 新增 {addCount}｜已在图集 {packedCount}｜" +
                       $"整图已覆盖 {coveredTexture}｜文件夹已覆盖 {coveredFolder}｜跳过 {skipped}" +
                       (unparsedLines > 0 ? $"｜⚠ packables 未识别条目行 {unparsedLines}" : string.Empty);
            return true;
        }

        /// <summary>
        /// 把分析结果中的新增条目写入图集文件并强制 Reimport，随后回读校验。
        /// 调用前必须先成功 Analyze()。
        /// </summary>
        private void WriteIntoAtlas()
        {
            List<Entry> adds = new List<Entry>();
            foreach (Entry entry in _entries)
            {
                if (entry.Action == EntryAction.Add)
                    adds.Add(entry);
            }

            if (adds.Count == 0)
            {
                _summary = "没有需要新增的条目，未写入。";
                return;
            }

            string atlasPath = AssetDatabase.GetAssetPath(_atlas);
            string atlasText = File.ReadAllText(atlasPath);
            bool crlf = atlasText.Contains("\r\n");
            string newline = crlf ? "\r\n" : "\n";
            List<string> lines = new List<string>(atlasText.Replace("\r\n", "\n").Split('\n'));

            // 定位 packables 头行。
            int headerIndex = -1;
            string headerIndent = "    ";
            bool emptyBracket = false;
            for (int i = 0; i < lines.Count; i++)
            {
                Match match = HeaderRegex.Match(lines[i]);
                if (match.Success)
                {
                    headerIndex = i;
                    headerIndent = match.Groups[1].Value;
                    emptyBracket = !string.IsNullOrEmpty(match.Groups[2].Value);
                    break;
                }
            }

            if (headerIndex < 0)
            {
                _summary = "写入失败：图集文件里没有 packables 段（可能不是 .spriteatlasv2 格式）。";
                Debug.LogError($"[SpriteAtlasInjector] {atlasPath} 中未找到 packables 段，未写入。", this);
                return;
            }

            // 组装条目行（4 空格缩进，与 Unity 原生格式一致）。
            List<string> entryLines = new List<string>(adds.Count);
            foreach (Entry entry in adds)
                entryLines.Add($"    - {{fileID: {entry.FileId}, guid: {entry.Guid}, type: 3}}");

            int insertAt;
            if (emptyBracket)
            {
                lines[headerIndex] = headerIndent + "packables:";
                insertAt = headerIndex + 1;
            }
            else
            {
                // packables 段内最后一个条目行（条目连续，遇到不深于头行缩进的非条目行即段结束）。
                int lastEntryIndex = -1;
                for (int i = headerIndex + 1; i < lines.Count; i++)
                {
                    string line = lines[i];
                    if (line.Trim().Length == 0)
                        continue;
                    if (EntryRegex.IsMatch(line))
                    {
                        lastEntryIndex = i;
                        continue;
                    }
                    if (LeadingWhitespace(line) <= headerIndent.Length)
                        break;
                }
                insertAt = lastEntryIndex >= 0 ? lastEntryIndex + 1 : headerIndex + 1;
            }

            lines.InsertRange(insertAt, entryLines);
            File.WriteAllText(atlasPath, string.Join(newline, lines), new UTF8Encoding(false));

            // 强制 Reimport 让图集按新 packables 重打包。
            AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceUpdate);

            // 回读校验：重新解析落盘文件，确认新增条目全部存在。
            string verifyText = File.ReadAllText(atlasPath);
            HashSet<(string, long)> written = new HashSet<(string, long)>(ParsePackableEntries(verifyText, out _));
            int confirmed = 0;
            foreach (Entry entry in adds)
            {
                if (written.Contains((entry.Guid, entry.FileId)))
                    confirmed++;
            }

            if (confirmed == adds.Count)
            {
                _summary = $"注入完成：新增 {confirmed} 条已写入并 Reimport，回读校验全部通过。";
                Debug.Log($"[SpriteAtlasInjector] {_prefab.name} → {atlasPath} 新增 {confirmed} 条 packables，校验通过。", _atlas);
            }
            else
            {
                _summary = $"注入异常：写入 {adds.Count} 条，回读只确认 {confirmed} 条，请检查 Console。";
                Debug.LogError($"[SpriteAtlasInjector] {_atlas.name} 注入后回读缺失条目（{confirmed}/{adds.Count}）。", _atlas);
            }
        }

        /// <summary>递归收集预制体（含未激活节点与嵌套预制体）全部组件上的 Sprite 字段引用。</summary>
        private static List<Sprite> CollectSprites(GameObject root)
        {
            List<Sprite> sprites = new List<Sprite>();
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null)
                    continue;   // 缺失脚本等空槽位
                SerializedObject serialized = new SerializedObject(component);
                SerializedProperty property = serialized.GetIterator();
                bool enterChildren = true;
                while (property.Next(enterChildren))
                {
                    enterChildren = true;
                    if (property.propertyType == SerializedPropertyType.ObjectReference &&
                        property.objectReferenceValue is Sprite sprite)
                    {
                        sprites.Add(sprite);
                    }
                }
            }
            return sprites;
        }

        /// <summary>解析 .spriteatlasv2 文本里的 packables 条目；无法识别的条目行计数返回。</summary>
        private static List<(string guid, long fileId)> ParsePackableEntries(string atlasText, out int unparsedLines)
        {
            List<(string guid, long fileId)> entries = new List<(string, long)>();
            unparsedLines = 0;
            string[] fileLines = atlasText.Replace("\r\n", "\n").Split('\n');
            foreach (string line in fileLines)
            {
                Match match = EntryRegex.Match(line);
                if (match.Success)
                {
                    entries.Add((match.Groups[2].Value.ToLowerInvariant(), long.Parse(match.Groups[1].Value)));
                    continue;
                }
                // 形似条目但不是单行内联格式（多行形态等）——计数提示，防止去重漏判。
                string trimmed = line.Trim();
                if (trimmed.StartsWith("- ", StringComparison.Ordinal) && trimmed.Contains("fileID:"))
                    unparsedLines++;
            }
            return entries;
        }

        /// <summary>行首空白字符数。</summary>
        private static int LeadingWhitespace(string line)
        {
            int count = 0;
            while (count < line.Length && (line[count] == ' ' || line[count] == '\t'))
                count++;
            return count;
        }
    }
}
#endif
