using OfficeOpenXml;
using System;
using System.Data;

namespace ExcelIntegration
{
    public class ExcelHelper
    {
        public static void ExportDataTableToExcel(DataTable dt, string filePath)
        {
            using (ExcelPackage excelPackage = new ExcelPackage())
            {
                var worksheet = excelPackage.Workbook.Worksheets.Add("Sheet1");
                worksheet.Cells[1, 1].LoadFromDataTable(dt, true);
                excelPackage.SaveAs(new FileInfo(filePath));
            }
        }

        public static DataTable ImportDataTableFromExcel(string filePath)
        {
            using (ExcelPackage excelPackage = new ExcelPackage(new FileInfo(filePath)))
            {
                var worksheet = excelPackage.Workbook.Worksheets[0];
                var dt = new DataTable();

                for (int i = 1; i <= worksheet.Dimension.End.Column; i++)
                {
                    dt.Columns.Add(worksheet.Cells[1, i].Text);
                }

                for (int i = 2; i <= worksheet.Dimension.End.Row; i++)
                {
                    var row = new object[dt.Columns.Count];
                    for (int j = 0; j < dt.Columns.Count; j++)
                    {
                        row[j] = worksheet.Cells[i, j + 1].Text;
                    }
                    dt.Rows.Add(row);
                }
                return dt;
            }
        }
    }
}