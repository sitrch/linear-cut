using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using LinearCutWpf.Services;
using Xunit;

namespace LinearCutWpf.Tests
{
    /// <summary>
    /// Проверяет, что быстрый распаковщик XlsxFastReader (чтение напрямую из архива в памяти)
    /// даёт те же результаты, что и полноценный разбор через ClosedXML:
    /// имена листов, имена столбцов и количество строк.
    /// </summary>
    public class XlsxFastReaderTests
    {
        private static readonly string[] SheetNames = { "Раскрой", "Материалы", "Прочее" };
        private static readonly string[] Headers = { "Артикул", "Наименование", "Длина", "Количество", "Лев угол", "Прав угол", "Цвет" };
        private const int DataRowCount = 25;

        /// <summary>
        /// Создаёт в памяти книгу .xlsx с известными листами, заголовками и строками.
        /// </summary>
        private static byte[] CreateWorkbookBytes()
        {
            using var wb = new XLWorkbook();
            foreach (var sheetName in SheetNames)
            {
                var ws = wb.Worksheets.Add(sheetName);
                for (int c = 0; c < Headers.Length; c++)
                    ws.Cell(1, c + 1).Value = Headers[c];

                for (int r = 0; r < DataRowCount; r++)
                {
                    ws.Cell(r + 2, 1).Value = "ART-" + r;
                    ws.Cell(r + 2, 2).Value = "Профиль " + r;
                    ws.Cell(r + 2, 3).Value = 1000 + r;
                    ws.Cell(r + 2, 4).Value = (r % 3) + 1;
                    ws.Cell(r + 2, 5).Value = 90;
                    ws.Cell(r + 2, 6).Value = 45;
                    ws.Cell(r + 2, 7).Value = "RAL9016";
                }
            }

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        private static List<string> ClosedXmlSheetNames(byte[] bytes)
        {
            using var ms = new MemoryStream(bytes, writable: false);
            using var wb = new XLWorkbook(ms);
            return wb.Worksheets.Select(w => w.Name).ToList();
        }

        private static List<string> ClosedXmlHeaders(byte[] bytes, string sheetName)
        {
            using var ms = new MemoryStream(bytes, writable: false);
            using var wb = new XLWorkbook(ms);
            var ws = wb.Worksheet(sheetName);
            return ws.FirstRowUsed().CellsUsed().Select(c => c.GetString()).ToList();
        }

        private static int ClosedXmlRowCount(byte[] bytes, string sheetName)
        {
            using var ms = new MemoryStream(bytes, writable: false);
            using var wb = new XLWorkbook(ms);
            var ws = wb.Worksheet(sheetName);
            return ws.RowsUsed().Count();
        }

        [Fact]
        public void GetSheetNames_MatchesClosedXmlAndExpected()
        {
            var bytes = CreateWorkbookBytes();

            var fast = XlsxFastReader.GetSheetNames(bytes);
            var closedXml = ClosedXmlSheetNames(bytes);

            Assert.Equal(SheetNames, fast);
            Assert.Equal(closedXml, fast);
        }

        [Theory]
        [InlineData("Раскрой")]
        [InlineData("Материалы")]
        [InlineData("Прочее")]
        public void GetHeaders_MatchesClosedXmlAndExpected(string sheetName)
        {
            var bytes = CreateWorkbookBytes();

            var fast = XlsxFastReader.GetHeaders(bytes, sheetName);
            var closedXml = ClosedXmlHeaders(bytes, sheetName);

            Assert.Equal(Headers, fast);
            Assert.Equal(closedXml, fast);
        }

        [Theory]
        [InlineData("Раскрой")]
        [InlineData("Материалы")]
        [InlineData("Прочее")]
        public void GetRowCount_MatchesClosedXmlAndExpected(string sheetName)
        {
            var bytes = CreateWorkbookBytes();

            int fast = XlsxFastReader.GetRowCount(bytes, sheetName);
            int closedXml = ClosedXmlRowCount(bytes, sheetName);

            // Заголовок + строки данных
            Assert.Equal(DataRowCount + 1, fast);
            Assert.Equal(closedXml, fast);
        }

        [Fact]
        public void GetHeaders_UnknownSheet_ReturnsEmpty()
        {
            var bytes = CreateWorkbookBytes();

            var fast = XlsxFastReader.GetHeaders(bytes, "НетТакогоЛиста");

            Assert.Empty(fast);
        }
    }
}
