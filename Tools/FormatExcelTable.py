# -*- coding: utf-8 -*-
"""改表后格式化：所有单元格左对齐 + 列宽/行高自适应（2026-11-01 流水线规则）。
用法: python Tools/FormatExcelTable.py [xlsx路径...]（无参=全部 Assets/Module/**/Excel/*.xlsx）"""
import sys, glob, openpyxl
from openpyxl.styles import Alignment
from openpyxl.utils import get_column_letter

def display_len(s):
    # 中文/全角按 2 宽估算
    return sum(2 if ord(c) > 127 else 1 for c in str(s))

def format_sheet(ws):
    # 所有单元格左对齐 + 垂直居中
    for row in ws.iter_rows():
        for cell in row:
            cell.alignment = Alignment(horizontal='left', vertical='center')
    # 列宽自适应（最大显示宽 + 余量，上限 60）
    for col_cells in ws.columns:
        maxw = max((display_len(c.value) for c in col_cells if c.value is not None), default=8)
        ws.column_dimensions[get_column_letter(col_cells[0].column)].width = min(maxw + 2, 60)
    # 行高自适应：单行文本给默认高度（Excel 自动）
    for r in range(1, ws.max_row + 1):
        ws.row_dimensions[r].height = None

def main():
    paths = sys.argv[1:] or glob.glob('Assets/Module/**/Excel/*.xlsx', recursive=True)
    for p in paths:
        wb = openpyxl.load_workbook(p)
        for ws in wb.worksheets:
            format_sheet(ws)
        wb.save(p)
        print('formatted', p)

if __name__ == '__main__':
    main()
