using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using MessagePack;

namespace ExcelTableExporter
{
    /// <summary>
    /// 导出信封：与运行时 Core.Gear.ConfigTableFile&lt;TRow&gt; 按 [Key] 列名一一对应。
    /// 必须是带属性的强类型——若用 Dictionary&lt;string,object&gt; 做信封，rows 会以 object 静态类型
    /// 落进 DynamicObjectTypeFallbackResolver 的反射字段序列化，写出无法反序列化的结构。
    /// </summary>
    [MessagePackObject]
    public sealed class TableEnvelope
    {
        [Key("version")]
        public int Version;

        [Key("tableName")]
        public string TableName;

        [Key("rows")]
        public List<Dictionary<string, object>> Rows;
    }

    /// <summary>
    /// 配置表导出工具：把 ConfigSources/Excel/ 下的 .xlsx 表导出为
    /// Assets/ConfigTables/&lt;表名&gt;.bytes（MessagePack 二进制，运行时格式）+ 同名 .json（调试副本）。
    ///
    /// Excel 约定：
    ///   第 1 行 = 字段名（与运行时行类型的 [Key("字段名")] 一一对应）；以 # 开头为注释列，不导出
    ///   第 2 行 = 类型（int/long/float/bool/string/skillparams，可空格后追加中文注释）；第一个空字段名即停止读列
    ///   第 3 行起 = 数据行；整行（有效列）全空视为空行跳过
    ///   每个工作表 = 一张表，表名 = 页签名；第 1 列即主键（推荐命名 id，也允许 skillId 等语义化命名，全表唯一）
    /// </summary>
    internal static class Program
    {
        // 收集的校验错误，任何一条都会让导出以非零码退出
        private static readonly List<string> Errors = new List<string>();

        private static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            try
            {
                // --init 模式：生成带表头/类型行/示例数据的模板
                if (args.Length >= 2 && args[0] == "--init")
                {
                    InitTemplate(args[1], args.Skip(2).ToArray());
                    return 0;
                }

                if (args.Length >= 1 && args[0] == "--help")
                {
                    PrintUsage();
                    return 0;
                }

                // 无参数即全量导出
                ExportAll();
                return Errors.Count == 0 ? 0 : 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"导出失败：{ex.Message}");
                for (Exception inner = ex.InnerException; inner != null; inner = inner.InnerException)
                {
                    Console.Error.WriteLine($"  内部异常：{inner.GetType().Name}: {inner.Message}");
                }
                Console.Error.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("ExcelTableExporter — 配置表导出工具");
            Console.WriteLine();
            Console.WriteLine("用法：");
            Console.WriteLine("  导出全部表   dotnet run --project Tools/ExcelTableExporter");
            Console.WriteLine("  生成模板     dotnet run --project Tools/ExcelTableExporter -- --init <表名> <字段:类型>...");
            Console.WriteLine("               示例：--init ItemTable id:int name:string icon:string type:int quality:int maxStack:int price:int desc:string");
            Console.WriteLine();
            Console.WriteLine("Excel 约定：第 1 行字段名(#开头=注释列)；第 2 行类型(int/long/float/bool/string/skillparams+注释)；第 3 行起数据；每个页签一张表，必须有 id 列。");
        }

        #region 模板生成

        /// <summary>
        /// 生成一张带表头、类型行与 3 行示例数据的模板 Excel。
        /// </summary>
        /// <param name="tableName">表名（即工作表名）。</param>
        /// <param name="columnDefs">列定义数组，格式 "字段名:类型"。</param>
        private static void InitTemplate(string tableName, string[] columnDefs)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new InvalidOperationException("表名不能为空。");
            }
            if (columnDefs.Length == 0)
            {
                throw new InvalidOperationException("需要给出列定义，例如：id:int name:string");
            }

            string root = DetectProjectRoot();
            string path = Path.Combine(root, "ConfigSources", "Excel", tableName + ".xlsx");
            if (File.Exists(path))
            {
                throw new InvalidOperationException($"模板已存在，拒绝覆盖策划数据：{path}");
            }

            // 解析列定义
            List<(string Name, TableColumnType Type)> columns = new List<(string, TableColumnType)>();
            foreach (string def in columnDefs)
            {
                string[] parts = def.Split(':', 2);
                if (parts.Length != 2 || !TryParseColumnType(parts[1], out TableColumnType type))
                {
                    throw new InvalidOperationException($"列定义非法：'{def}'（应为 字段名:类型，类型 = int/long/float/bool/string）");
                }
                columns.Add((parts[0].Trim(), type));
            }

            // 写表头与示例数据
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using XLWorkbook workbook = new XLWorkbook();
            IXLWorksheet sheet = workbook.Worksheets.Add(tableName);
            for (int i = 0; i < columns.Count; i++)
            {
                (string name, TableColumnType type) = columns[i];
                sheet.Cell(1, i + 1).Value = name;
                sheet.Cell(2, i + 1).Value = type.ToString().ToLowerInvariant();
                SetSampleValue(sheet.Cell(3, i + 1), MakeSampleValue(name, type, 1));
                SetSampleValue(sheet.Cell(4, i + 1), MakeSampleValue(name, type, 2));
                SetSampleValue(sheet.Cell(5, i + 1), MakeSampleValue(name, type, 3));
            }
            sheet.Columns().AdjustToContents();
            workbook.SaveAs(path);

            Console.WriteLine($"[OK] 模板已创建：{path}");
            Console.WriteLine("第 1 行=字段名，第 2 行=类型（可在类型后空格追加中文注释），第 3 行起=数据；#开头字段名为注释列不导出。");
        }

        /// <summary>
        /// 把示例值按具体类型写入单元格（XLCellValue 只对明确的基础类型提供隐式转换）。
        /// </summary>
        private static void SetSampleValue(IXLCell cell, object value)
        {
            switch (value)
            {
                case int i: cell.Value = Convert.ToDouble(i); break;
                case long l: cell.Value = Convert.ToDouble(l); break;
                case float f: cell.Value = Convert.ToDouble(f); break;
                case bool b: cell.Value = b; break;
                default: cell.Value = value?.ToString() ?? string.Empty; break;
            }
        }

        /// <summary>
        /// 生成示例数据：id 列用 1001 起的序号，其余按类型给一个可读示例。
        /// </summary>
        private static object MakeSampleValue(string fieldName, TableColumnType type, int rowIndex)
        {
            if (type is TableColumnType.Int or TableColumnType.Long && fieldName.Equals("id", StringComparison.OrdinalIgnoreCase))
            {
                return 1000 + rowIndex;
            }

            // tags 列给可分发的示例标签，顺带示范"多标签逗号分隔"与"空标签=纯展示"两种写法
            if (type == TableColumnType.String && fieldName.Equals("tags", StringComparison.OrdinalIgnoreCase))
            {
                return rowIndex switch
                {
                    1 => "usable,sell",
                    2 => string.Empty,
                    3 => "usable,sell,batch",
                    _ => "usable",
                };
            }

            return type switch
            {
                TableColumnType.Int => rowIndex,
                TableColumnType.Long => (long)rowIndex,
                TableColumnType.Float => rowIndex * 1.5f,
                TableColumnType.Bool => rowIndex % 2 == 1,
                _ => $"示例文本{rowIndex}",
            };
        }

        #endregion

        #region 全量导出

        /// <summary>
        /// 默认导出目录对（Excel 源目录 → .bytes 输出目录，相对工程根）。
        /// 约定（2026-09-28）：所有数据表统一放 Assets/Module/&lt;模块&gt;/ 下，可新建文件夹分级；
        /// 新模块按"模块目录/Excel → 模块目录/(ConfigTables)"追加到这里，无参全量导出即覆盖全部。
        /// </summary>
        private static readonly (string SourceDir, string OutDir)[] DefaultDirPairs =
        {
            (Path.Combine("Assets", "Module", "Items", "Excel"), Path.Combine("Assets", "Module", "Items")),
            (Path.Combine("Assets", "Module", "Unit", "Character", "Excel"), Path.Combine("Assets", "Module", "Unit", "Character", "ConfigTables")),
            (Path.Combine("Assets", "Module", "Unit", "Weapon", "Excel"), Path.Combine("Assets", "Module", "Unit", "Weapon", "ConfigTables")),
            (Path.Combine("Assets", "Module", "Unit", "Trinket", "Excel"), Path.Combine("Assets", "Module", "Unit", "Trinket", "ConfigTables")),
            (Path.Combine("Assets", "Module", "Unit", "Enemy", "Excel"), Path.Combine("Assets", "Module", "Unit", "Enemy", "ConfigTables")),
            (Path.Combine("Assets", "Module", "Battle", "Excel"), Path.Combine("Assets", "Module", "Battle", "ConfigTables")),
        };

        /// <summary>
        /// 全量导出：遍历所有目录对，把每个工作簿的每个可见页签导出为 .bytes + .json。
        /// </summary>
        private static void ExportAll()
        {
            string root = DetectProjectRoot();

            int tableCount = 0;
            foreach ((string sourceRel, string outRel) in DefaultDirPairs)
            {
                string sourceDir = Path.Combine(root, sourceRel);
                string outDir = Path.Combine(root, outRel);
                if (!Directory.Exists(sourceDir))
                {
                    Console.WriteLine($"未找到 {sourceDir}，跳过该目录对。");
                    continue;
                }
                Directory.CreateDirectory(outDir);
                tableCount += ExportDirectory(sourceDir, outDir);
            }

            Console.WriteLine();
            if (Errors.Count > 0)
            {
                Console.WriteLine($"导出完成，但有 {Errors.Count} 条错误：");
                foreach (string error in Errors) Console.WriteLine("  " + error);
            }
            else
            {
                Console.WriteLine($"[完成] 共导出 {tableCount} 张表。");
            }
        }

        /// <summary>
        /// 导出单个源目录下全部 .xlsx 的每个可见页签，返回导出的表数量。
        /// </summary>
        private static int ExportDirectory(string sourceDir, string outDir)
        {

            // 只扫顶层；~$ 开头是 Excel 打开时的锁文件，跳过
            string[] files = Directory.GetFiles(sourceDir, "*.xlsx", SearchOption.TopDirectoryOnly)
                .Where(f => !Path.GetFileName(f).StartsWith("~$"))
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToArray();
            if (files.Length == 0)
            {
                Console.WriteLine($"{sourceDir} 下没有 .xlsx 文件。");
                return 0;
            }

            int tableCount = 0;
            foreach (string file in files)
            {
                using XLWorkbook workbook = new XLWorkbook(file);
                foreach (IXLWorksheet sheet in workbook.Worksheets)
                {
                    // 隐藏页与 # 开头页签不导出，可用来放说明页
                    if (sheet.Visibility != XLWorksheetVisibility.Visible)
                    {
                        Console.WriteLine($"[跳过] {Path.GetFileName(file)}/{sheet.Name}：隐藏页");
                        continue;
                    }
                    if (sheet.Name.StartsWith("#"))
                    {
                        Console.WriteLine($"[跳过] {Path.GetFileName(file)}/{sheet.Name}：注释页");
                        continue;
                    }

                    ExportSheet(sheet, outDir);
                    tableCount++;
                }
            }

            return tableCount;
        }

        /// <summary>
        /// 导出单个工作表为 .bytes + .json。
        /// </summary>
        private static void ExportSheet(IXLWorksheet sheet, string outDir)
        {
            // 1) 解析表头：第 1 行字段名（第一个空名即停止），第 2 行类型
            List<ColumnSpec> columns = new List<ColumnSpec>();
            for (int col = 1; ; col++)
            {
                string name = sheet.Cell(1, col).GetString().Trim();
                if (string.IsNullOrEmpty(name)) break;
                if (name.StartsWith("#"))
                {
                    Console.WriteLine($"  [{sheet.Name}] 注释列不导出：{name}");
                    continue;
                }
                if (columns.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    AddError(sheet.Name, sheet.Cell(1, col).Address.ToString(), $"字段名重复：{name}");
                    continue;
                }

                string typeRaw = sheet.Cell(2, col).GetString();
                if (!TryParseColumnType(typeRaw, out TableColumnType type))
                {
                    AddError(sheet.Name, sheet.Cell(2, col).Address.ToString(),
                        $"类型非法：'{typeRaw.Trim()}'（应为 int/long/float/bool/string/skillparams，可在类型后空格追加注释）");
                    continue;
                }
                columns.Add(new ColumnSpec(col, name, type, col == 1));
            }

            // 主键约定：第 1 列即主键（推荐命名 id，也允许 skillId/effectId 等语义化命名），否则运行时无法建索引
            if (!columns.Any(c => c.IsId))
            {
                AddError(sheet.Name, "A1", "缺少主键列（第 1 列），无法导出。");
            }

            // 2) 读数据行：第 3 行起，整行全空跳过
            int lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
            List<Dictionary<string, object>> rows = new List<Dictionary<string, object>>();
            HashSet<object> seenIds = new HashSet<object>();
            for (int row = 3; row <= lastRow; row++)
            {
                if (columns.All(c => string.IsNullOrWhiteSpace(sheet.Cell(row, c.Index).GetString())))
                {
                    continue;
                }

                Dictionary<string, object> rowData = new Dictionary<string, object>();
                bool rowValid = true;
                foreach (ColumnSpec column in columns)
                {
                    IXLCell cell = sheet.Cell(row, column.Index);
                    string text = cell.GetString().Trim();
                    object value = ConvertCell(column, text, out string error, sheet.Name, cell.Address.ToString());
                    if (error != null)
                    {
                        AddError(sheet.Name, cell.Address.ToString(), $"{column.Name}：{error}");
                        rowValid = false;
                        continue;
                    }

                    // 主键查重：报错并丢弃整行，避免静默覆盖
                    if (column.IsId && !seenIds.Add(value))
                    {
                        AddError(sheet.Name, cell.Address.ToString(), $"主键重复：{value}（该行已丢弃）");
                        rowValid = false;
                        continue;
                    }
                    rowData[column.Name] = value;
                }

                if (rowValid) rows.Add(rowData);
            }

            if (Errors.Count > 0)
            {
                Console.WriteLine($"[失败] {sheet.Name}：存在校验错误，未写出文件。");
                return;
            }

            // 3) 组信封并序列化：行内仍是 Dictionary（列名 → 基元值），走 map 格式按 [Key] 列名与运行时匹配
            TableEnvelope envelope = new TableEnvelope
            {
                Version = 1,
                TableName = sheet.Name,
                Rows = rows,
            };
            byte[] bytes = MessagePackSerializer.Serialize(envelope, MessagePackSerializerOptions.Standard);

            // 4) 调试副本：工具手里就有列名与类型，直接生成带字段名的 JSON（不依赖 ConvertToJson）
            string json = BuildJson(envelope);
            string basePath = Path.Combine(outDir, sheet.Name);
            File.WriteAllBytes(basePath + ".bytes", bytes);
            File.WriteAllText(basePath + ".json", json);

            Console.WriteLine($"[OK] {sheet.Name}：{rows.Count} 行，bytes {bytes.Length} B，json {json.Length} B");
        }

        #endregion

        #region 列与单元格

        /// <summary>表列的解析结果。</summary>
        private sealed class ColumnSpec
        {
            // Excel 中的列号（1 起）
            public readonly int Index;
            // 字段名，与运行时 [Key] 一致
            public readonly string Name;
            // 声明的单元格类型
            public readonly TableColumnType Type;
            // 是否主键列（id）
            public readonly bool IsId;

            public ColumnSpec(int index, string name, TableColumnType type, bool isId)
            {
                Index = index;
                Name = name;
                Type = type;
                IsId = isId;
            }
        }

        /// <summary>支持的列类型。</summary>
        private enum TableColumnType
        {
            Int,
            Long,
            Float,
            Bool,
            String,

            // 技能参数占位符集：{键:初值:成长, ...}，按 string 透传，仅增加导出期格式校验
            SkillParams,
        }

        /// <summary>
        /// 解析类型单元格：取首个空白分隔 token 作为类型名，其余视为注释。
        /// </summary>
        private static bool TryParseColumnType(string raw, out TableColumnType type)
        {
            type = default;
            string token = raw.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (token == null) return false;

            switch (token.ToLowerInvariant())
            {
                case "int": type = TableColumnType.Int; return true;
                case "long": type = TableColumnType.Long; return true;
                case "float": type = TableColumnType.Float; return true;
                case "bool": type = TableColumnType.Bool; return true;
                case "string": type = TableColumnType.String; return true;
                case "skillparams": type = TableColumnType.SkillParams; return true;
                default: return false;
            }
        }

        /// <summary>
        /// 按列类型转换单元格文本；失败时给出带单元格坐标的错误。
        /// </summary>
        private static object ConvertCell(ColumnSpec column, string text, out string error, string sheetName, string cellAddress)
        {
            error = null;
            if (column.Type is not (TableColumnType.String or TableColumnType.SkillParams) && string.IsNullOrEmpty(text))
            {
                error = "必填单元格为空";
                return null;
            }

            try
            {
                switch (column.Type)
                {
                    case TableColumnType.Int:
                        return int.Parse(text, CultureInfo.InvariantCulture);
                    case TableColumnType.Long:
                        return long.Parse(text, CultureInfo.InvariantCulture);
                    case TableColumnType.Float:
                        return float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
                    case TableColumnType.Bool:
                        string lowered = text.ToLowerInvariant();
                        if (lowered == "1" || lowered == "true") return true;
                        if (lowered == "0" || lowered == "false") return false;
                        error = "bool 取值需为 0/1/true/false";
                        return null;
                    case TableColumnType.SkillParams:
                        // 空 = 该技能无参数；非空必须是 {键:初值:成长, ...} 形态
                        if (string.IsNullOrEmpty(text)) return string.Empty;
                        string paramsError = ValidateSkillParams(text);
                        if (paramsError != null)
                        {
                            error = paramsError;
                            return null;
                        }
                        return text;
                    default:
                        return text;
                }
            }
            catch (Exception ex)
            {
                error = $"无法按 {column.Type} 解析 '{text}'：{ex.Message}";
                return null;
            }
        }

        /// <summary>
        /// 校验技能参数占位符集格式：整体花括号包裹，逗号分隔「键:初值:成长」三元组；
        /// 键小写字母开头仅小写字母/数字/下划线且不重复，初值/成长为不变文化数字。
        /// 返回 null 表示合法，否则返回错误说明。
        /// </summary>
        private static string ValidateSkillParams(string text)
        {
            string body = text.Trim();
            if (!body.StartsWith("{") || !body.EndsWith("}"))
            {
                return "参数需整体花括号包裹：{键:初值:成长, ...}";
            }
            body = body.Substring(1, body.Length - 2);
            if (string.IsNullOrWhiteSpace(body))
            {
                return "花括号内为空（无参数请整列留空）";
            }

            HashSet<string> seen = new HashSet<string>();
            foreach (string item in body.Split(','))
            {
                string token = item.Trim();
                if (token.Length == 0) continue;

                string[] parts = token.Split(':');
                if (parts.Length != 3)
                {
                    return $"'{token}' 需为 键:初值:成长 三段";
                }
                string key = parts[0].Trim();
                if (!Regex.IsMatch(key, "^[a-z][a-z0-9_]*$"))
                {
                    return $"键 '{key}' 需为小写字母开头，仅小写字母/数字/下划线";
                }
                if (!seen.Add(key))
                {
                    return $"键重复：{key}";
                }
                if (!float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                {
                    return $"初值 '{parts[1].Trim()}' 不是合法数字";
                }
                if (!float.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                {
                    return $"成长 '{parts[2].Trim()}' 不是合法数字";
                }
            }
            return null;
        }

        /// <summary>登记一条校验错误（工具退出码由此决定）。</summary>
        private static void AddError(string sheetName, string cellAddress, string message)
        {
            Errors.Add($"[{sheetName}] {cellAddress}: {message}");
        }

        #endregion

        #region JSON 调试副本

        /// <summary>
        /// 由信封生成带字段名的 JSON 调试副本。
        /// </summary>
        private static string BuildJson(TableEnvelope envelope)
        {
            JsonObject root = new JsonObject
            {
                ["version"] = envelope.Version,
                ["tableName"] = envelope.TableName,
            };

            JsonArray rows = new JsonArray();
            foreach (Dictionary<string, object> row in envelope.Rows)
            {
                JsonObject rowNode = new JsonObject();
                foreach (KeyValuePair<string, object> pair in row)
                {
                    rowNode[pair.Key] = pair.Value switch
                    {
                        bool b => JsonValue.Create(b),
                        int i => JsonValue.Create(i),
                        long l => JsonValue.Create(l),
                        float f => JsonValue.Create(f),
                        string s => JsonValue.Create(s),
                        _ => JsonValue.Create(pair.Value?.ToString() ?? ""),
                    };
                }
                rows.Add(rowNode);
            }
            root["rows"] = rows;

            // 中文原样输出，方便排查
            return JsonSerializer.Serialize<JsonNode>(root, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });
        }

        #endregion

        #region 工程定位

        /// <summary>
        /// 从当前目录向上查找 Unity 工程根（同时存在 Assets 与 ProjectSettings 的目录）。
        /// </summary>
        private static string DetectProjectRoot()
        {
            DirectoryInfo dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "Assets")) &&
                    Directory.Exists(Path.Combine(dir.FullName, "ProjectSettings")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
            throw new InvalidOperationException("未找到 Unity 工程根（缺少 Assets/ProjectSettings），请在工程目录下运行。");
        }

        #endregion
    }
}
