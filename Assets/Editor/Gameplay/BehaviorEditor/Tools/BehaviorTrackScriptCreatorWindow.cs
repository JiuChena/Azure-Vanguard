#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BehaviorEditor
{
    /// <summary>
    /// 创建 BehaviorEditor 轨道基础脚本的编辑器工具。
    /// </summary>
    internal sealed class BehaviorTrackScriptCreatorWindow : EditorWindow
    {
        // 本地保存运行时生成路径的 EditorPrefs 键。
        private const string RuntimeFolderPreferenceKey = "BehaviorEditor.NewTrackScripts.RuntimeFolder";
        // 本地保存编辑器生成路径的 EditorPrefs 键。
        private const string EditorFolderPreferenceKey = "BehaviorEditor.NewTrackScripts.EditorFolder";
        // 默认运行时生成路径，按当前 BehaviorEditor 轨道目录组织。
        private const string DefaultRuntimeFolder =
            "Assets/_Project/Scripts/CSharp/Core/Gameplay/BehaviorEditor/Tracks";
        // 默认编辑器生成路径，按当前 BehaviorEditor 编译器目录组织。
        private const string DefaultEditorFolder =
            "Assets/_Project/Editor/Gameplay/BehaviorEditor/TimelineCompilation";
        // 当前运行时脚本的父文件夹资产路径。
        private string runtimeFolderPath;
        // 当前编辑器脚本的父文件夹资产路径。
        private string editorFolderPath;
        // 当前待生成轨道的 C# 类型名称。
        private string trackName = string.Empty;
        // 窗口打开后是否把焦点放入轨道名称输入框。
        private bool focusTrackName;

        /// <summary>
        /// 打开 BehaviorEditor 新轨道脚本生成窗口。
        /// </summary>
        [MenuItem("Tools/Behavior Editor/Create New Track Scripts")]
        private static void OpenWindow()
        {
            BehaviorTrackScriptCreatorWindow window =
                GetWindow<BehaviorTrackScriptCreatorWindow>("Create New Track Scripts");
            window.runtimeFolderPath = EditorPrefs.GetString(
                RuntimeFolderPreferenceKey,
                DefaultRuntimeFolder);
            window.editorFolderPath = EditorPrefs.GetString(
                EditorFolderPreferenceKey,
                DefaultEditorFolder);
            window.trackName = string.Empty;
            window.focusTrackName = true;
            window.minSize = new Vector2(500f, 250f);
            window.ShowUtility();
        }

        /// <summary>
        /// 绘制轨道名称、生成路径和创建操作界面。
        /// </summary>
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Create New Behavior Track Scripts", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            DrawFolderField(
                "Runtime Folder",
                ref runtimeFolderPath,
                RuntimeFolderPreferenceKey,
                false);
            DrawFolderField(
                "Editor Folder",
                ref editorFolderPath,
                EditorFolderPreferenceKey,
                true);

            EditorGUILayout.HelpBox(
                "运行时四个脚本和 Editor 编译器会分别生成到对应路径下的 <TrackName>Track 文件夹。两个路径都必须位于 Assets 下且已存在。",
                MessageType.Info);

            GUI.SetNextControlName("TrackNameField");
            trackName = EditorGUILayout.TextField("Track Name", trackName);
            if (focusTrackName)
            {
                EditorGUI.FocusTextInControl("TrackNameField");
                focusTrackName = false;
            }

            EditorGUILayout.Space(8f);
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(trackName)))
            {
                if (GUILayout.Button("Create Track Scripts", GUILayout.Height(30f)))
                    CreateTrackScripts();
            }
        }

        /// <summary>
        /// 绘制一个可输入并可浏览的文件夹路径字段。
        /// </summary>
        /// <param name="label">字段显示名称。</param>
        /// <param name="folderPath">需要绘制和更新的资产路径。</param>
        /// <param name="preferenceKey">该路径对应的 EditorPrefs 键。</param>
        /// <param name="editorFolder">是否正在绘制编辑器脚本目录。</param>
        private void DrawFolderField(
            string label,
            ref string folderPath,
            string preferenceKey,
            bool editorFolder)
        {
            EditorGUILayout.BeginHorizontal();
            folderPath = EditorGUILayout.TextField(label, folderPath);
            if (GUILayout.Button("Browse", GUILayout.Width(72f)))
                SelectTargetFolder(ref folderPath, preferenceKey, editorFolder);
            EditorGUILayout.EndHorizontal();
            folderPath = NormalizeAssetPath(
                folderPath,
                editorFolder ? DefaultEditorFolder : DefaultRuntimeFolder);
            EditorPrefs.SetString(preferenceKey, folderPath);
        }

        /// <summary>
        /// 打开文件夹选择器并将结果保存到指定路径字段。
        /// </summary>
        /// <param name="folderPath">需要更新的资产路径字段。</param>
        /// <param name="preferenceKey">该路径对应的 EditorPrefs 键。</param>
        /// <param name="editorFolder">是否正在选择编辑器脚本目录。</param>
        private void SelectTargetFolder(ref string folderPath, string preferenceKey, bool editorFolder)
        {
            string absolutePath = AssetPathToAbsolutePath(folderPath);
            string selectedPath = EditorUtility.OpenFolderPanel(
                editorFolder
                    ? "Select Behavior Track Compiler Parent Folder"
                    : "Select Behavior Track Runtime Parent Folder",
                Directory.Exists(absolutePath) ? absolutePath : Application.dataPath,
                string.Empty);
            if (string.IsNullOrWhiteSpace(selectedPath))
                return;

            if (!TryConvertAbsoluteAssetFolderPath(selectedPath, out string assetPath))
            {
                ShowError("生成路径必须位于当前项目的 Assets 文件夹内。");
                return;
            }

            folderPath = assetPath;
            EditorPrefs.SetString(preferenceKey, folderPath);
            Repaint();
        }

        /// <summary>
        /// 创建轨道专属文件夹并写入五个最小轨道脚本。
        /// </summary>
        private void CreateTrackScripts()
        {
            string currentTrackName = trackName.Trim();
            if (!IsValidTrackName(currentTrackName, out string nameError))
            {
                ShowError(nameError);
                return;
            }

            runtimeFolderPath = NormalizeAssetPath(runtimeFolderPath, DefaultRuntimeFolder);
            editorFolderPath = NormalizeAssetPath(editorFolderPath, DefaultEditorFolder);
            if (!AssetDatabase.IsValidFolder(runtimeFolderPath) ||
                !AssetDatabase.IsValidFolder(editorFolderPath))
            {
                ShowError("Runtime Folder 或 Editor Folder 不存在，请重新选择 Assets 下的已有文件夹。");
                return;
            }

            if (ContainsPathSegment(runtimeFolderPath, "Editor") ||
                !ContainsPathSegment(editorFolderPath, "Editor"))
            {
                ShowError("运行时脚本路径不能位于 Editor 目录；编辑器脚本路径必须位于 Editor 目录内。");
                return;
            }

            string trackFolderName = $"{currentTrackName}Track";
            string runtimeTrackFolderPath = $"{runtimeFolderPath}/{trackFolderName}";
            string editorTrackFolderPath = $"{editorFolderPath}/{trackFolderName}";
            string[] fileNames = GetGeneratedFileNames(currentTrackName);
            string[] assetPaths =
            {
                $"{runtimeTrackFolderPath}/{fileNames[0]}",
                $"{runtimeTrackFolderPath}/{fileNames[1]}",
                $"{runtimeTrackFolderPath}/{fileNames[2]}",
                $"{runtimeTrackFolderPath}/{fileNames[3]}",
                $"{editorTrackFolderPath}/{fileNames[4]}"
            };

            if (!ValidateOutputPaths(assetPaths))
                return;

            CreateTrackFolderIfMissing(runtimeFolderPath, runtimeTrackFolderPath, trackFolderName);
            CreateTrackFolderIfMissing(editorFolderPath, editorTrackFolderPath, trackFolderName);
            AssetDatabase.Refresh();

            if (!AssetDatabase.IsValidFolder(runtimeTrackFolderPath) ||
                !AssetDatabase.IsValidFolder(editorTrackFolderPath))
            {
                ShowError("无法创建运行时或编辑器轨道文件夹。");
                return;
            }

            try
            {
                WriteGeneratedScripts(
                    AssetPathToAbsolutePath(runtimeTrackFolderPath),
                    AssetPathToAbsolutePath(editorTrackFolderPath),
                    currentTrackName,
                    fileNames);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                ShowError($"生成轨道脚本失败：{exception.Message}");
                return;
            }

            EditorPrefs.SetString(RuntimeFolderPreferenceKey, runtimeFolderPath);
            EditorPrefs.SetString(EditorFolderPreferenceKey, editorFolderPath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
            SelectGeneratedFolder(runtimeTrackFolderPath, assetPaths[0]);
            Close();
        }

        /// <summary>
        /// 验证轨道输出文件，防止覆盖已有源码。
        /// </summary>
        /// <param name="assetPaths">待生成的资产路径列表。</param>
        /// <returns>全部输出文件都不存在时返回 true。</returns>
        private bool ValidateOutputPaths(IReadOnlyList<string> assetPaths)
        {
            for (int index = 0; index < assetPaths.Count; index++)
            {
                if (!File.Exists(AssetPathToAbsolutePath(assetPaths[index])))
                    continue;

                ShowError($"目标文件已存在，生成已中止：{assetPaths[index]}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 创建缺失的轨道脚本文件夹。
        /// </summary>
        /// <param name="parentFolderPath">轨道文件夹的父目录。</param>
        /// <param name="trackFolderPath">轨道文件夹资产路径。</param>
        /// <param name="trackFolderName">轨道文件夹名称。</param>
        private static void CreateTrackFolderIfMissing(
            string parentFolderPath,
            string trackFolderPath,
            string trackFolderName)
        {
            if (!AssetDatabase.IsValidFolder(trackFolderPath))
                AssetDatabase.CreateFolder(parentFolderPath, trackFolderName);
        }

        /// <summary>
        /// 获取五个基础轨道脚本的默认文件名。
        /// </summary>
        /// <param name="currentTrackName">已通过标识符校验的轨道名称。</param>
        /// <returns>轨道声明、片段资产、运行时数据、执行器和编译器文件名。</returns>
        private static string[] GetGeneratedFileNames(string currentTrackName)
        {
            return new[]
            {
                $"BehaviorTimeline{currentTrackName}Track.cs",
                $"BehaviorTimeline{currentTrackName}ClipAsset.cs",
                $"{currentTrackName}TrackData.cs",
                $"{currentTrackName}TrackExecutor.cs",
                $"{currentTrackName}TimelineTrackCompiler.cs"
            };
        }

        /// <summary>
        /// 写入五个轨道脚本模板。
        /// </summary>
        /// <param name="absoluteRuntimeFolderPath">运行时脚本输出绝对路径。</param>
        /// <param name="absoluteEditorFolderPath">编辑器脚本输出绝对路径。</param>
        /// <param name="currentTrackName">已通过标识符校验的轨道名称。</param>
        /// <param name="fileNames">输出文件名列表。</param>
        private static void WriteGeneratedScripts(
            string absoluteRuntimeFolderPath,
            string absoluteEditorFolderPath,
            string currentTrackName,
            IReadOnlyList<string> fileNames)
        {
            string[] contents =
            {
                BehaviorTrackScriptTemplates.BuildTrack(currentTrackName),
                BehaviorTrackScriptTemplates.BuildClipAsset(currentTrackName),
                BehaviorTrackScriptTemplates.BuildTrackData(currentTrackName),
                BehaviorTrackScriptTemplates.BuildTrackExecutor(currentTrackName),
                BehaviorTrackScriptTemplates.BuildTrackCompiler(currentTrackName)
            };
            UTF8Encoding encoding = new UTF8Encoding(false);

            for (int index = 0; index < fileNames.Count; index++)
            {
                string folderPath = index == fileNames.Count - 1
                    ? absoluteEditorFolderPath
                    : absoluteRuntimeFolderPath;
                File.WriteAllText(Path.Combine(folderPath, fileNames[index]), contents[index], encoding);
            }
        }

        /// <summary>
        /// 验证轨道名称是否符合 C# 类型和文件命名要求。
        /// </summary>
        /// <param name="currentTrackName">待验证的轨道名称。</param>
        /// <param name="validationError">验证失败时返回错误原因。</param>
        /// <returns>名称合法时返回 true。</returns>
        private static bool IsValidTrackName(string currentTrackName, out string validationError)
        {
            if (string.IsNullOrWhiteSpace(currentTrackName))
            {
                validationError = "轨道名称不能为空。";
                return false;
            }

            if (!IsIdentifierStart(currentTrackName[0]))
            {
                validationError = "轨道名称首字符必须是英文字母或下划线。";
                return false;
            }

            for (int index = 1; index < currentTrackName.Length; index++)
            {
                if (IsIdentifierPart(currentTrackName[index]))
                    continue;

                validationError = "轨道名称只能包含英文字母、数字和下划线。";
                return false;
            }

            if (CSharpKeywords.Contains(currentTrackName))
            {
                validationError = $"轨道名称不能使用 C# 关键字：{currentTrackName}。";
                return false;
            }

            validationError = null;
            return true;
        }

        /// <summary>
        /// 将用户输入的路径标准化为 Unity 资产路径。
        /// </summary>
        /// <param name="path">用户输入的资产路径或项目内绝对路径。</param>
        /// <param name="defaultPath">输入为空时使用的默认资产路径。</param>
        /// <returns>使用正斜杠的标准化路径。</returns>
        private static string NormalizeAssetPath(string path, string defaultPath)
        {
            if (string.IsNullOrWhiteSpace(path))
                return defaultPath;

            string normalizedPath = path.Trim().Trim('"').Replace('\\', '/').TrimEnd('/');
            if (Path.IsPathRooted(normalizedPath) &&
                TryConvertAbsoluteAssetFolderPath(normalizedPath, out string assetPath))
                return assetPath;

            return normalizedPath;
        }

        /// <summary>
        /// 将当前项目 Assets 下的绝对文件夹路径转换为资产路径。
        /// </summary>
        /// <param name="absolutePath">待转换的绝对路径。</param>
        /// <param name="assetPath">转换后的 Unity 资产路径。</param>
        /// <returns>路径位于当前项目 Assets 下时返回 true。</returns>
        private static bool TryConvertAbsoluteAssetFolderPath(string absolutePath, out string assetPath)
        {
            assetPath = null;
            string assetsPath = Path.GetFullPath(Application.dataPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullPath = Path.GetFullPath(absolutePath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (!fullPath.Equals(assetsPath, StringComparison.OrdinalIgnoreCase) &&
                !fullPath.StartsWith(assetsPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                !fullPath.StartsWith(assetsPath + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return false;

            string relativePath = fullPath.Substring(assetsPath.Length).TrimStart(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            assetPath = string.IsNullOrWhiteSpace(relativePath)
                ? "Assets"
                : $"Assets/{relativePath.Replace('\\', '/')}";
            return true;
        }

        /// <summary>
        /// 将 Unity 资产路径转换为当前项目的绝对路径。
        /// </summary>
        /// <param name="assetPath">Assets 下的资产路径。</param>
        /// <returns>对应的绝对路径。</returns>
        private static string AssetPathToAbsolutePath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath) ||
                !assetPath.StartsWith("Assets", StringComparison.Ordinal))
                return string.Empty;

            string relativePath = assetPath.Substring("Assets".Length).TrimStart('/', '\\');
            return Path.GetFullPath(Path.Combine(Application.dataPath, relativePath));
        }

        /// <summary>
        /// 判断资产路径是否包含指定的目录层级名称。
        /// </summary>
        /// <param name="assetPath">待检查的 Unity 资产路径。</param>
        /// <param name="segment">需要匹配的目录名称。</param>
        /// <returns>路径包含指定目录层级时返回 true。</returns>
        private static bool ContainsPathSegment(string assetPath, string segment)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || string.IsNullOrWhiteSpace(segment))
                return false;

            string[] pathSegments = assetPath.Split('/');
            for (int index = 0; index < pathSegments.Length; index++)
            {
                if (string.Equals(pathSegments[index], segment, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 选中并定位生成的轨道文件夹。
        /// </summary>
        /// <param name="trackFolderPath">生成的轨道文件夹资产路径。</param>
        /// <param name="firstAssetPath">生成的第一个脚本资产路径。</param>
        private static void SelectGeneratedFolder(string trackFolderPath, string firstAssetPath)
        {
            UnityEngine.Object folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(trackFolderPath);
            UnityEngine.Object target = folder ?? AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(firstAssetPath);
            if (target == null)
                return;

            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }

        /// <summary>
        /// 显示生成流程错误并保留窗口供用户修正输入。
        /// </summary>
        /// <param name="message">需要显示的错误信息。</param>
        private static void ShowError(string message)
        {
            Debug.LogError(message);
            EditorUtility.DisplayDialog("Create New Track Scripts", message, "OK");
        }

        /// <summary>
        /// 判断字符是否可以作为 C# 标识符首字符。
        /// </summary>
        /// <param name="character">待判断的字符。</param>
        /// <returns>字符可以作为标识符首字符时返回 true。</returns>
        private static bool IsIdentifierStart(char character)
        {
            return character == '_' || character >= 'A' && character <= 'Z' ||
                character >= 'a' && character <= 'z';
        }

        /// <summary>
        /// 判断字符是否可以作为 C# 标识符后续字符。
        /// </summary>
        /// <param name="character">待判断的字符。</param>
        /// <returns>字符可以作为标识符后续字符时返回 true。</returns>
        private static bool IsIdentifierPart(char character)
        {
            return IsIdentifierStart(character) || character >= '0' && character <= '9';
        }

        // C# 关键字集合，用于阻止生成无法编译的类型名称。
        private static readonly HashSet<string> CSharpKeywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
            "long", "namespace", "new", "null", "object", "operator", "out", "params", "private",
            "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof",
            "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try",
            "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void",
            "volatile", "while", "add", "alias", "ascending", "async", "await", "by", "descending",
            "dynamic", "equals", "from", "get", "global", "group", "into", "join", "let", "nameof",
            "notnull", "on", "orderby", "remove", "select", "set", "unmanaged", "value", "var", "when",
            "where", "with", "yield"
        };
    }
}
#endif
