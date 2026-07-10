using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;

namespace LinearCutWpf.Services
{
    /// <summary>
    /// Быстрое чтение служебной информации из .xlsx напрямую из распакованного в память архива
    /// (без полного разбора книги через ClosedXML). Используется для мгновенного получения
    /// списка листов и заголовков столбцов при открытии файла.
    /// </summary>
    public static class XlsxFastReader
    {
        private const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        /// <summary>
        /// Возвращает имена видимых листов книги, читая xl/workbook.xml из распакованного архива.
        /// </summary>
        public static List<string> GetSheetNames(byte[] bytes)
        {
            var names = new List<string>();
            using var ms = new MemoryStream(bytes, 0, bytes.Length, writable: false);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);

            var wb = zip.GetEntry("xl/workbook.xml");
            if (wb == null) return names;

            using var s = wb.Open();
            using var reader = XmlReader.Create(s);
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "sheet")
                {
                    string name = reader.GetAttribute("name");
                    string state = reader.GetAttribute("state");
                    if (!string.IsNullOrEmpty(name) &&
                        !string.Equals(state, "hidden", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(state, "veryHidden", StringComparison.OrdinalIgnoreCase))
                    {
                        names.Add(name);
                    }
                }
            }
            return names;
        }

        /// <summary>
        /// Возвращает заголовки столбцов (первую непустую строку) указанного листа,
        /// читая XML листа напрямую из распакованного архива.
        /// </summary>
        public static List<string> GetHeaders(byte[] bytes, string sheetName)
        {
            using var ms = new MemoryStream(bytes, 0, bytes.Length, writable: false);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);

            string sheetPath = ResolveSheetPath(zip, sheetName);
            if (sheetPath == null) return new List<string>();

            var entry = zip.GetEntry(sheetPath);
            if (entry == null) return new List<string>();

            string[] sharedStrings = null; // ленивая загрузка

            var headerByColumn = new SortedDictionary<int, string>();
            using (var s = entry.Open())
            using (var reader = XmlReader.Create(s))
            {
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "row")
                    {
                        ReadFirstRowCells(reader, zip, ref sharedStrings, headerByColumn);
                        break; // нужна только первая строка
                    }
                }
            }

            return headerByColumn.Values.Select(v => v ?? string.Empty).ToList();
        }

        /// <summary>
        /// Возвращает количество непустых строк листа (включая строку заголовков),
        /// читая XML листа напрямую из распакованного архива.
        /// </summary>
        public static int GetRowCount(byte[] bytes, string sheetName)
        {
            using var ms = new MemoryStream(bytes, 0, bytes.Length, writable: false);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);

            string sheetPath = ResolveSheetPath(zip, sheetName);
            if (sheetPath == null) return 0;

            var entry = zip.GetEntry(sheetPath);
            if (entry == null) return 0;

            int count = 0;
            using var s = entry.Open();
            using var reader = XmlReader.Create(s);
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "row")
                {
                    bool hasData = false;
                    using (var rowReader = reader.ReadSubtree())
                    {
                        rowReader.Read(); // <row>
                        while (rowReader.Read())
                        {
                            if (rowReader.NodeType == XmlNodeType.Element &&
                                (rowReader.LocalName == "v" || rowReader.LocalName == "t"))
                            {
                                hasData = true;
                                break;
                            }
                        }
                    }
                    if (hasData) count++;
                }
            }
            return count;
        }

        private static void ReadFirstRowCells(XmlReader reader, ZipArchive zip, ref string[] sharedStrings, SortedDictionary<int, string> result)
        {
            using var rowReader = reader.ReadSubtree();
            rowReader.Read(); // <row>
            while (rowReader.Read())
            {
                if (rowReader.NodeType == XmlNodeType.Element && rowReader.LocalName == "c")
                {
                    string cellRef = rowReader.GetAttribute("r");
                    string type = rowReader.GetAttribute("t");
                    int col = ColumnIndexFromRef(cellRef, result.Count);

                    string value = ReadCellValue(rowReader, type, zip, ref sharedStrings);
                    if (col >= 0)
                        result[col] = value;
                }
            }
        }

        private static string ReadCellValue(XmlReader cReader, string type, ZipArchive zip, ref string[] sharedStrings)
        {
            string raw = null;
            bool isInline = false;

            using (var sub = cReader.ReadSubtree())
            {
                sub.Read(); // <c>
                while (sub.Read())
                {
                    if (sub.NodeType != XmlNodeType.Element) continue;

                    if (sub.LocalName == "v")
                    {
                        raw = sub.ReadElementContentAsString();
                    }
                    else if (sub.LocalName == "is")
                    {
                        raw = ReadInlineString(sub);
                        isInline = true;
                    }
                }
            }

            if (raw == null) return string.Empty;

            if (type == "s" && !isInline)
            {
                sharedStrings ??= LoadSharedStrings(zip);
                if (int.TryParse(raw, out int idx) && idx >= 0 && idx < sharedStrings.Length)
                    return sharedStrings[idx];
                return string.Empty;
            }

            return raw;
        }

        private static string ReadInlineString(XmlReader isReader)
        {
            var sb = new StringBuilder();
            using var sub = isReader.ReadSubtree();
            sub.Read(); // <is>
            while (sub.Read())
            {
                if (sub.NodeType == XmlNodeType.Element && sub.LocalName == "t")
                    sb.Append(sub.ReadElementContentAsString());
            }
            return sb.ToString();
        }

        private static string[] LoadSharedStrings(ZipArchive zip)
        {
            var entry = zip.GetEntry("xl/sharedStrings.xml");
            if (entry == null) return Array.Empty<string>();

            var list = new List<string>();
            using var s = entry.Open();
            using var reader = XmlReader.Create(s);
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "si")
                {
                    var sb = new StringBuilder();
                    using (var sub = reader.ReadSubtree())
                    {
                        sub.Read(); // <si>
                        while (sub.Read())
                        {
                            if (sub.NodeType == XmlNodeType.Element && sub.LocalName == "t")
                                sb.Append(sub.ReadElementContentAsString());
                        }
                    }
                    list.Add(sb.ToString());
                }
            }
            return list.ToArray();
        }

        private static string ResolveSheetPath(ZipArchive zip, string sheetName)
        {
            string rid = null;

            var wb = zip.GetEntry("xl/workbook.xml");
            if (wb == null) return null;
            using (var s = wb.Open())
            using (var reader = XmlReader.Create(s))
            {
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "sheet")
                    {
                        if (reader.GetAttribute("name") == sheetName)
                        {
                            rid = reader.GetAttribute("id", RelNs) ?? reader.GetAttribute("r:id");
                            break;
                        }
                    }
                }
            }
            if (string.IsNullOrEmpty(rid)) return null;

            string target = null;
            var rels = zip.GetEntry("xl/_rels/workbook.xml.rels");
            if (rels == null) return null;
            using (var s = rels.Open())
            using (var reader = XmlReader.Create(s))
            {
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "Relationship")
                    {
                        if (reader.GetAttribute("Id") == rid)
                        {
                            target = reader.GetAttribute("Target");
                            break;
                        }
                    }
                }
            }
            if (string.IsNullOrEmpty(target)) return null;

            target = target.Replace('\\', '/');
            if (target.StartsWith("/")) return target.TrimStart('/');
            if (target.StartsWith("xl/")) return target;
            return "xl/" + target;
        }

        /// <summary>
        /// Преобразует ссылку на ячейку (например "B1") в 0-базный индекс столбца.
        /// Если ссылка отсутствует, возвращает <paramref name="fallback"/> (порядковый индекс).
        /// </summary>
        private static int ColumnIndexFromRef(string cellRef, int fallback)
        {
            if (string.IsNullOrEmpty(cellRef)) return fallback;

            int col = 0;
            bool any = false;
            foreach (char ch in cellRef)
            {
                if (ch >= 'A' && ch <= 'Z') { col = col * 26 + (ch - 'A' + 1); any = true; }
                else if (ch >= 'a' && ch <= 'z') { col = col * 26 + (ch - 'a' + 1); any = true; }
                else break;
            }
            return any ? col - 1 : fallback;
        }
    }
}
