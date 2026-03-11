using OfficeOpenXml; // Assuming EPPlus is already installed 
using System.IO;
using System.Linq;

namespace YourNamespace
{
    public class ExcelIntegration
    {
        public void SaveResultsToExcel(string filePath, List<CutResult> results)
        {
            // Ensure the file path ends with .xlsx
            if (!filePath.EndsWith(".xlsx"))
            {
                throw new ArgumentException("File must be an .xlsx file");
            }

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Results");
                worksheet.Cells[1, 1].Value = "Header1"; // Customize headers as needed
                worksheet.Cells[1, 2].Value = "Header2";

                int row = 2; // Start from the second row
                foreach (var result in results)
                {
                    worksheet.Cells[row, 1].Value = result.Property1; // Customize properties
                    worksheet.Cells[row, 2].Value = result.Property2;
                    row++;
                }

                // Save the package to the specified file path
                package.SaveAs(new FileInfo(filePath));
            }
        }

        public List<CutResult> LoadResultsFromExcel(string filePath)
        {
            var results = new List<CutResult>();

            // Ensure the file path ends with .xlsx
            if (!filePath.EndsWith(".xlsx"))
            {
                throw new ArgumentException("File must be an .xlsx file");
            }

            using (var package = new ExcelPackage(new FileInfo(filePath)))
            {
                var worksheet = package.Workbook.Worksheets.FirstOrDefault();
                if (worksheet == null)
                {
                    throw new Exception("Worksheet not found");
                }

                int rowCount = worksheet.Dimension.Rows;
                for (int row = 2; row <= rowCount; row++) // Start from the second row
                {
                    var result = new CutResult
                    {
                        Property1 = worksheet.Cells[row, 1].Value?.ToString(), // Customize properties
                        Property2 = worksheet.Cells[row, 2].Value?.ToString()
                    };
                    results.Add(result);
                }
            }

            return results;
        }
    }
}