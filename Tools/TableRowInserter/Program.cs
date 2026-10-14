using System.Text;
using System.Text.Json;
using ClosedXML.Excel;

namespace TableRowInserter
{
    /// <summary>
    /// 表格行插入工具：向遵循 ExcelTableExporter 约定的 .xlsx 追加数据行（新角色接入等批量造数场景）。
    /// 约定与导出工具一致：第 1 行字段名（# 开头为注释列）、第 2 行类型（int/long/float/bool/string，可空格追加中文注释）、
    /// 第 3 行起数据、第 1 列主键；第一个空字段名即停止读列。
    /// 用法：dotnet run --project Tools/TableRowInserter -- &lt;xlsx路径&gt; &lt;行json文件&gt;
    /// 行 json 文件 = JSON 对象数组，键与第 1 行字段名对应；缺失字段留空单元格。
    /// 幂等：主键（第 1 列）已存在的行跳过不重复插入；数值按类型行写数值单元格，避免导出端读到文本。
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            try
            {
                if (args.Length != 2)
                {
                    PrintUsage();
                    return 1;
                }

                Insert(args[0], args[1]);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"插入失败：{ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("TableRowInserter — 向 ExcelTableExporter 约定的 .xlsx 追加数据行");
            Console.WriteLine();
            Console.WriteLine("用法：dotnet run --project Tools/TableRowInserter -- <xlsx路径> <行json文件>");
            Console.WriteLine("行 json 文件内容为 JSON 对象数组，键 = 第 1 行字段名，如：");
            Console.WriteLine('[' + "{\"id\":\"CH0209\",\"name\":\"...\",\"rarity\":3}" + ']');
        }

        /// <summary>读字段名/类型行 → 既有主键集合 → 逐行按类型写入 → 保存。</summary>
        private static void Insert(string xlsxPath, string jsonPath)
        {
            if (!File.Exists(xlsxPath)) throw new FileNotFoundException($"找不到表文件：{xlsxPath}");
            if (!File.Exists(jsonPath)) throw new FileNotFoundException($"找不到行文件：{jsonPath}");

            JsonElement[] rows = JsonSerializer.Deserialize<JsonElement[]>(File.ReadAllText(jsonPath))
                ?? throw new InvalidOperationException($"行文件不是 JSON 数组：{jsonPath}");

            using XLWorkbook workbook = new XLWorkbook(xlsxPath);
            IXLWorksheet sheet = workbook.Worksheets.First();

            //字段名 → 列号；# 开头注释列跳过（导出工具同样不导出），第一个空字段名即停止
            Dictionary<string, int> columns = new();
            int lastColumn = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
            for (int c = 1; c <= lastColumn; c++)
            {
                string name = sheet.Cell(1, c).GetString().Trim();
                if (name.Length == 0) break;
                if (!name.StartsWith("#")) columns[name] = c;
            }
            if (columns.Count == 0) throw new InvalidOperationException($"第 1 行未读到任何字段名：{xlsxPath}");

            //类型行：数值/布尔决定单元格写法（数字写数值单元格）
            string keyField = columns.Keys.First(); //第 1 列即主键（字典保序）
            int keyColumn = columns[keyField];
            Dictionary<string, string> types = new();
            foreach ((string name, int column) in columns)
            {
                string raw = sheet.Cell(2, columns[name]).GetString().Trim();
                types[name] = raw.Length == 0 ? "string" : raw.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
            }

            //既有主键集合（幂等跳过的依据）
            HashSet<string> existing = new();
            int lastRow = sheet.LastRowUsed()?.RowNumber() ?? 2;
            for (int r = 3; r <= lastRow; r++)
            {
                string key = sheet.Cell(r, keyColumn).GetString().Trim();
                if (key.Length > 0) existing.Add(key);
            }

            int inserted = 0, skipped = 0;
            int writeRow = lastRow;
            foreach (JsonElement row in rows)
            {
                string key = ReadString(row, keyField);
                if (key.Length == 0) throw new InvalidOperationException($"行缺少主键字段 '{keyField}'：{row}");
                if (existing.Contains(key))
                {
                    Console.WriteLine($"[跳过] {key}：主键已存在");
                    skipped++;
                    continue;
                }

                writeRow++;
                foreach ((string name, int column) in columns)
                {
                    if (!row.TryGetProperty(name, out JsonElement value)) continue; //缺失字段留空
                    WriteCell(sheet.Cell(writeRow, column), value, types[name]);
                }
                Console.WriteLine($"[插入] {key} → 第 {writeRow} 行");
                inserted++;
            }

            workbook.SaveAs(xlsxPath);
            Console.WriteLine($"[完成] {Path.GetFileName(xlsxPath)}：插入 {inserted} 行，跳过 {skipped} 行。");
        }

        /// <summary>按类型行写单元格：数字写 double 数值单元格，布尔写布尔，其余写字符串。</summary>
        private static void WriteCell(IXLCell cell, JsonElement value, string type)
        {
            switch (type)
            {
                case "int":
                case "long":
                case "float":
                    cell.Value = ToDouble(value);
                    break;
                case "bool":
                    cell.Value = value.ValueKind == JsonValueKind.True
                        || (value.ValueKind == JsonValueKind.String && string.Equals(value.GetString(), "true", StringComparison.OrdinalIgnoreCase));
                    break;
                default:
                    cell.Value = ReadString(null, null, value);
                    break;
            }
        }

        /// <summary>数值取值：JSON 数字直取，数字字符串解析，其余按 0 报错前兜底。</summary>
        private static double ToDouble(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Number) return value.GetDouble();
            if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), out double parsed)) return parsed;
            throw new InvalidOperationException($"数值字段遇到非数值内容：{value}");
        }

        /// <summary>主键/字符串取值：字符串直取，其他 JSON 类型转文本。</summary>
        private static string ReadString(JsonElement row, string field) =>
            row.TryGetProperty(field, out JsonElement value) ? ReadString(null, null, value) : string.Empty;

        private static string ReadString(object _, string __, JsonElement value) =>
            value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString();
    }
}
