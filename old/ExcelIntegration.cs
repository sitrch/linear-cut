using MiniExcelLibs; // Вместо OfficeOpenXml
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace LinearCut.old
{
    public class ExcelIntegration
    {
        public void SaveResultsToExcel(string filePath, List<CutResult> results)
        {
            // MiniExcel сам создаст файл или перезапишет существующий
            // Для настройки заголовков можно использовать анонимные типы или атрибуты в классе CutResult
            var dataToSave = results.Select(r => new
            {
                Header1 = r.Property1,
                Header2 = r.Property2
            });

            MiniExcel.SaveAs(filePath, dataToSave);
        }

        public List<CutResult> LoadResultsFromExcel(string filePath)
        {
            // MiniExcel автоматически маппит колонки на свойства класса по именам заголовков
            // или по порядку, если заголовки не указаны.
            var results = MiniExcel.Query<CutResult>(filePath).ToList();

            return results;
        }
    }

    public class CutResult
    {
        // Если заголовки в Excel не совпадают с именами свойств, 
        // используйте атрибут [ExcelColumnName("Header1")]
        public string Property1 { get; set; }
        public string Property2 { get; set; }
    }
}
