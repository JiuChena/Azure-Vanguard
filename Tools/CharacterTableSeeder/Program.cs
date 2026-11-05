using System.Text;
using ClosedXML.Excel;

namespace CharacterTableSeeder
{
    /// <summary>
    /// 角色数值表种子工具：一次生成角色模块全部 6 张表（含 CH0221 全套示例数据）到
    /// Assets/Module/Unit/Character/Excel/。文件已存在时跳过，保护策划数据；
    /// 需要重新生成时先删除对应 xlsx 再运行。
    ///
    /// 输出格式遵循 ExcelTableExporter 约定：第 1 行字段名、第 2 行类型(+中文注释)、第 3 行起数据。
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            try
            {
                string root = DetectProjectRoot();
                string outDir = Path.Combine(root, "Assets", "Module", "Unit", "Character", "Excel");
                Directory.CreateDirectory(outDir);

                WriteTable(Path.Combine(outDir, "CharacterTable.xlsx"), "CharacterTable", CharacterColumns, BuildCharacterRows());
                WriteTable(Path.Combine(outDir, "CharacterLevelTable.xlsx"), "CharacterLevelTable", LevelColumns, BuildLevelRows());
                WriteTable(Path.Combine(outDir, "CharacterSkillTable.xlsx"), "CharacterSkillTable", SkillColumns, BuildSkillRows());
                WriteTable(Path.Combine(outDir, "SkillEffectTable.xlsx"), "SkillEffectTable", SkillEffectColumns, BuildSkillEffectRows());
                WriteTable(Path.Combine(outDir, "UnlockNodeTable.xlsx"), "UnlockNodeTable", UnlockNodeColumns, BuildUnlockNodeRows());
                WriteTable(Path.Combine(outDir, "BreakthroughTable.xlsx"), "BreakthroughTable", BreakthroughColumns, BuildBreakthroughRows());

                Console.WriteLine("[完成] 角色数值表种子生成结束。");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"种子生成失败：{ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        #region 表定义（列 = 字段名/类型/中文注释）

        // 列定义三元组：字段名（与运行时行类 [Key] 一致）、类型、类型行追加的中文注释
        private static readonly (string Name, string Type, string Comment)[] CharacterColumns =
        {
            ("id", "string", "单位ID（=UnitConfig.unitId=头像图集sprite名=存档仓库键，如 CH0221）"),
            ("name", "string", "名称"),
            ("rarity", "int", "稀有度"),
            ("profession", "string", "职业定位"),
            ("camp", "string", "阵营"),
            ("cost", "int", "局内放置点数消耗"),
            ("hp", "int", "基础生命"),
            ("atk", "int", "基础攻击"),
            ("def", "int", "基础防御"),
            ("atkInterval", "float", "攻击间隔(秒)"),
            ("range", "float", "射程(格)"),
            ("moveSpeed", "float", "移动速度(格/秒)"),
            ("normalAtkId", "int", "普攻技能id"),
            ("skillEId", "int", "小技能E id"),
            ("skillQId", "int", "爆发技能Q id"),
            ("talentIds", "string", "天赋节点id，逗号分隔"),
            ("bonusIds", "string", "属性加成节点id，逗号分隔"),
        };

        private static readonly (string Name, string Type, string Comment)[] LevelColumns =
        {
            ("level", "int", "等级"),
            ("multiplier", "float", "全属性成长系数"),
            ("expToNext", "int", "升到下一级所需经验（80×L^1.5 向上取整；满级 0）"),
            ("costItems", "string", "升到下一级消耗（itemId:count 逗号分隔；满级空）"),
        };

        private static readonly (string Name, string Type, string Comment)[] SkillColumns =
        {
            ("skillId", "int", "技能id = 角色id×100+槽位(01普攻/02E/03Q)"),
            ("name", "string", "技能名称"),
            ("type", "string", "类型：normal普攻/e小技能/burst爆发"),
            ("maxLevel", "int", "最大等级(1~10)"),
            ("cooldown", "float", "冷却秒数，0=无冷却"),
            ("cost", "int", "局内点数消耗"),
            ("effectIds", "string", "效果id列表，逗号分隔，一效果一行"),
            ("desc", "string", "描述"),
        };

        private static readonly (string Name, string Type, string Comment)[] SkillEffectColumns =
        {
            ("effectId", "int", "效果id = skillId×100+序号"),
            ("effectType", "string", "效果类型：damage/heal/slow…运行时注册表分发"),
            ("target", "string", "目标：self/team_all/team_except_self/single_target/enemies_in_range"),
            ("range", "float", "作用范围(格)，0=单体"),
            ("value", "float", "1级数值（单位由effectType语义决定）"),
            ("growth", "float", "每级增量，某级数值=value+growth×(level-1)"),
            ("duration", "float", "持续秒数，0=瞬时"),
            ("desc", "string", "说明"),
        };

        private static readonly (string Name, string Type, string Comment)[] UnlockNodeColumns =
        {
            ("nodeId", "int", "节点id = 角色221段位+序号"),
            ("type", "string", "类型：talent天赋(被动触发)/bonus属性加成"),
            ("effectKey", "string", "talent=行为逻辑键；bonus=属性名(hp_pct/atk_pct/crit_rate/crit_dmg…)"),
            ("effectValue", "float", "加成数值"),
            ("params", "skillparams", "天赋行为参数 {键:初值:成长} 逗号分隔（talent 行用）"),
            ("cost", "string", "独立解锁消耗 itemId:count 逗号分隔；空=随突破解锁"),
            ("desc", "string", "说明"),
        };

        private static readonly (string Name, string Type, string Comment)[] BreakthroughColumns =
        {
            ("breakStage", "int", "突破阶段(1起)"),
            ("levelCap", "int", "突破后等级上限"),
            ("costItems", "string", "突破消耗 itemId:count 逗号分隔（金币=1）"),
            ("unlockNodeIds", "string", "突破附带解锁的节点id，逗号分隔"),
        };

        #endregion

        #region 行数据（CH0221 示例数值，平衡数值仅作活样板）

        // 注：突破/解锁消耗里的 1002/1003 暂用 ItemTable 现有示例行占位，真实材料行待 ItemTable 扩充后对齐
        private static object[][] BuildCharacterRows()
        {
            return new[]
            {
                new object[] { "CH0221", "柚鸟夏【乐队】", 4, "support", "student", 10, 900, 110, 42, 1.1f, 5.5f, 3.2f, 22101, 22102, 22103, "22111,22112", "22121,22122,22123,22124" },
            };
        }

        // 成长曲线示例：1 + 0.05×(level-1)，线性；曲线形状后续全表统一调
        private static object[][] BuildLevelRows()
        {
            object[][] rows = new object[80][];
            for (int level = 1; level <= 80; level++)
            {
                int exp = level < 80 ? (int)Math.Ceiling(80.0 * Math.Pow(level, 1.5)) : 0;
                string cost = level < 80 ? $"Currency_CreditPoint:{500 * level}" : string.Empty;
                rows[level - 1] = new object[]
                {
                    level, Math.Round(1.0 + (level - 1) * 0.05, 3), exp, cost
                };
            }
            return rows;
        }

        private static object[][] BuildSkillRows()
        {
            return new[]
            {
                new object[] { 22101, "音波弹", "normal", 10, 0f, 0, "2210101", "对目标敌人造成一次音波伤害" },
                new object[] { 22102, "和弦扩散", "e", 10, 8f, 2, "2210201,2210202", "以自身为中心扩散音浪，伤害并减速范围内敌人" },
                new object[] { 22103, "终乐章", "burst", 10, 25f, 6, "2210301,2210302", "奏响终乐章，对大范围敌人造成重伤并治愈全队" },
            };
        }

        private static object[][] BuildSkillEffectRows()
        {
            return new[]
            {
                new object[] { 2210101, "damage", "single_target", 0f, 1.0f, 0.08f, 0f, "对目标造成攻击力倍率伤害" },
                new object[] { 2210201, "damage", "enemies_in_range", 4f, 1.5f, 0.12f, 0f, "范围内敌人受攻击力倍率伤害" },
                new object[] { 2210202, "slow", "enemies_in_range", 4f, 0.4f, 0.03f, 2.5f, "范围内敌人移动速度降低（0.4=减速40%）" },
                new object[] { 2210301, "damage", "enemies_in_range", 8f, 3.2f, 0.25f, 0f, "大范围敌人受攻击力倍率伤害" },
                new object[] { 2210302, "heal", "team_all", 0f, 0.15f, 0.01f, 0f, "全队回复各自最大生命值的比例" },
            };
        }

        private static object[][] BuildUnlockNodeRows()
        {
            return new[]
            {
                new object[] { 22111, "talent", "ch0221_encore", 0f, "{chance:0.3:0, ratio:0.05:0}", "Currency_StarlightStone:5000,Material_UpgradeManual:2", "追奏：全队获得追奏效果；对敌人造成伤害时30%概率追加一次伤害（基础值=CH0221最大生命×充能效率×5%）" },
                new object[] { 22121, "bonus", "atk_pct", 0.04f, "", "", "突破2解锁：攻击力+4%" },
                new object[] { 22122, "bonus", "hp_pct", 0.05f, "", "", "突破3解锁：生命+5%" },
                new object[] { 22123, "bonus", "crit_rate", 0.05f, "", "", "突破4解锁：暴击率+5%" },
                new object[] { 22124, "bonus", "crit_dmg", 0.1f, "", "", "突破5解锁：暴击伤害+10%" },
            };
        }

        private static object[][] BuildBreakthroughRows()
        {
            return new[]
            {
                new object[] { 1, 40, "1:20000,1002:3,1003:5", "" },
                new object[] { 2, 50, "1:40000,1002:5,1003:8", "22121" },
                new object[] { 3, 60, "1:60000,1002:8,1003:12", "22122" },
                new object[] { 4, 70, "1:100000,1002:12,1003:16", "22123" },
                new object[] { 5, 80, "1:160000,1002:16,1003:24", "22124" },
            };
        }

        #endregion

        #region 写表

        /// <summary>
        /// 按导出工具约定写一张 xlsx：第 1 行字段名、第 2 行类型(+注释)、第 3 行起数据。
        /// 文件已存在时跳过，保护策划数据。
        /// </summary>
        private static void WriteTable(string path, string sheetName, (string Name, string Type, string Comment)[] columns, object[][] rows)
        {
            if (File.Exists(path))
            {
                Console.WriteLine($"[跳过] 已存在（保护策划数据，重生成请先删除）：{path}");
                return;
            }

            using XLWorkbook workbook = new XLWorkbook();
            IXLWorksheet sheet = workbook.Worksheets.Add(sheetName);

            for (int i = 0; i < columns.Length; i++)
            {
                (string name, string type, string comment) = columns[i];
                sheet.Cell(1, i + 1).Value = name;
                // 类型行：类型 token 在前（工具按首个空白分隔 token 解析），中文注释随后
                sheet.Cell(2, i + 1).Value = string.IsNullOrEmpty(comment) ? type : $"{type} {comment}";
            }

            for (int r = 0; r < rows.Length; r++)
            {
                object[] row = rows[r];
                if (row.Length != columns.Length)
                {
                    throw new InvalidOperationException($"[{sheetName}] 第 {r + 1} 条数据列数 {row.Length} 与表头 {columns.Length} 不一致。");
                }
                for (int c = 0; c < row.Length; c++)
                {
                    SetCell(sheet.Cell(r + 3, c + 1), row[c]);
                }
            }

            sheet.Columns().AdjustToContents();
            workbook.SaveAs(path);
            Console.WriteLine($"[OK] {sheetName}：{rows.Length} 行 → {path}");
        }

        /// <summary>
        /// 按具体类型写入单元格（XLCellValue 只对明确的基础类型提供隐式转换；null = 空单元格）。
        /// </summary>
        private static void SetCell(IXLCell cell, object value)
        {
            switch (value)
            {
                case null: break;
                case int i: cell.Value = Convert.ToDouble(i); break;
                case long l: cell.Value = Convert.ToDouble(l); break;
                case float f: cell.Value = Convert.ToDouble(f); break;
                case double d: cell.Value = d; break;
                case bool b: cell.Value = b; break;
                default: cell.Value = value.ToString(); break;
            }
        }

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
