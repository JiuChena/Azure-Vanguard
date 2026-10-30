using ClosedXML.Excel;

// 一次性生成 BuffTable.xlsx（对齐 ExcelTableExporter 约定：第1行字段名 / 第2行类型 / 第3行起数据）。
// 仅用于创建表骨架，用完即弃（Tools/_TmpBuffTable 清理）。
if (args.Length != 1) throw new ArgumentException("用法：BuffTableCreate <输出xlsx路径>");
string path = args[0];
if (File.Exists(path)) File.Delete(path);

using XLWorkbook wb = new XLWorkbook();
IXLWorksheet ws = wb.Worksheets.Add("BuffTable");
ws.Cell(1, 1).Value = "buffId";
ws.Cell(1, 2).Value = "name";
ws.Cell(1, 3).Value = "desc";
ws.Cell(1, 4).Value = "icon";
ws.Cell(2, 1).Value = "int";
ws.Cell(2, 2).Value = "string";
ws.Cell(2, 3).Value = "string";
ws.Cell(2, 4).Value = "string";

static void Row(IXLWorksheet ws, int r, int buffId, string name, string desc, string icon)
{
    ws.Cell(r, 1).Value = buffId;
    ws.Cell(r, 2).Value = name;
    ws.Cell(r, 3).Value = desc;
    ws.Cell(r, 4).Value = icon;
}
Row(ws, 3, 1, "ATK Up", "+50 Attack for 10s", "Buff_AtkUp");
Row(ws, 4, 2, "DEF Up", "+30 Defense for 8s", "Buff_DefUp");

wb.SaveAs(path);
Console.WriteLine($"[OK] {path}");
