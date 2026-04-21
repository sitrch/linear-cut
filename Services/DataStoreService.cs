using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace LinearCutWpf.Services
{
    /// <summary>
    /// Сервис для хранения и управления данными (DataTable, конфигурация столбцов).
    /// Является единым источником истины для всего приложения.
    /// </summary>
    public class DataStoreService
    {
        private static DataStoreService _instance;
        private static readonly object _lock = new object();

        public static DataStoreService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new DataStoreService();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Исходная (сырая) таблица данных из Excel.
        /// </summary>
        public DataTable RawDataTable { get; private set; }

        /// <summary>
        /// Обработанная таблица данных (с заменой точек на запятые, протягиванием ключей и т.д.).
        /// </summary>
        public DataTable ProcessedDataTable { get; private set; }

        /// <summary>
        /// Таблица конфигурации столбцов (роли: IsKey, IsName, IsVal, IsQty, IsLeftAngle, IsRightAngle, Color).
        /// </summary>
        public DataTable ColumnConfigTable { get; private set; }

        /// <summary>
        /// Имя текущего файла.
        /// </summary>
        public string CurrentFilePath { get; private set; }

        /// <summary>
        /// Индексы строк с ошибками валидации.
        /// </summary>
        public HashSet<int> InvalidRows { get; set; } = new HashSet<int>();

        /// <summary>
        /// Множество ячеек (индекс строки, имя столбца), которые были автозаполнены.
        /// </summary>
        public HashSet<(int row, string colName)> AutoFilledKeys { get; set; } = new HashSet<(int, string)>();

        /// <summary>
        /// Событие, вызываемое при изменении обработанных данных.
        /// </summary>
        public event EventHandler ProcessedDataChanged;

        /// <summary>
        /// Событие, вызываемое при изменении конфигурации столбцов.
        /// </summary>
        public event EventHandler ColumnConfigChanged;

        private DataStoreService() { }

        /// <summary>
        /// Инициализирует хранилище сырыми данными из DataTable.
        /// </summary>
        /// <param name="rawDataTable">Исходная таблица данных из Excel.</param>
        /// <param name="columnConfig">Таблица конфигурации столбцов.</param>
        /// <param name="filePath">Путь к файлу.</param>
        public void Initialize(DataTable rawDataTable, DataTable columnConfig, string filePath = null)
        {
            RawDataTable = rawDataTable?.Copy();
            ColumnConfigTable = columnConfig?.Copy();
            CurrentFilePath = filePath;
            InvalidRows.Clear();
            AutoFilledKeys.Clear();

            ProcessData();
        }

        /// <summary>
        /// Обновляет конфигурацию столбцов и пересчитывает обработанные данные.
        /// </summary>
        /// <param name="columnConfig">Новая конфигурация столбцов.</param>
        public void UpdateColumnConfig(DataTable columnConfig)
        {
            ColumnConfigTable = columnConfig?.Copy();
            ProcessData();
            ColumnConfigChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Обрабатывает сырые данные: замена точек на запятые, протягивание ключей, вычисление ArticleKey.
        /// </summary>
        public void ProcessData()
        {
            if (RawDataTable == null)
            {
                ProcessedDataTable = null;
                ProcessedDataChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            AutoFilledKeys.Clear();
            ProcessedDataTable = RawDataTable.Copy();
            DataTable dt = ProcessedDataTable;

            var keys = GetKeyColumns();
            var vals = GetColumnsByType("IsVal");
            var qtys = GetColumnsByType("IsQty");
            var leftAngles = GetColumnsByType("IsLeftAngle");
            var rightAngles = GetColumnsByType("IsRightAngle");

            // Меняем точку на запятую в столбцах Value
            foreach (var valCol in vals)
            {
                foreach (DataRow r in dt.Rows)
                {
                    if (r[valCol] != DBNull.Value)
                    {
                        string strVal = r[valCol].ToString();
                        if (strVal.Contains('.'))
                            r[valCol] = strVal.Replace('.', ',');
                    }
                }
            }

            // Меняем точку на запятую в столбцах Qty
            foreach (var qtyCol in qtys)
            {
                foreach (DataRow r in dt.Rows)
                {
                    if (r[qtyCol] != DBNull.Value)
                    {
                        string strQty = r[qtyCol].ToString();
                        if (strQty.Contains('.'))
                            r[qtyCol] = strQty.Replace('.', ',');
                    }
                }
            }

            // Меняем точку на запятую в столбцах углов
            foreach (var angCol in leftAngles.Concat(rightAngles))
            {
                foreach (DataRow r in dt.Rows)
                {
                    if (r[angCol] != DBNull.Value)
                    {
                        string strAng = r[angCol].ToString();
                        if (strAng.Contains('.'))
                            r[angCol] = strAng.Replace('.', ',');
                    }
                }
            }

            // Удаляем строки, где все столбцы значений (IsVal) пустые
            if (vals.Any())
            {
                for (int i = dt.Rows.Count - 1; i >= 0; i--)
                {
                    if (vals.All(v => dt.Rows[i][v] == DBNull.Value || string.IsNullOrWhiteSpace(dt.Rows[i][v].ToString())))
                    {
                        dt.Rows.RemoveAt(i);
                    }
                }
            }

            // Протягиваем значения ключевых столбцов сверху вниз
            foreach (string k in keys)
            {
                for (int r = 1; r < dt.Rows.Count; r++)
                {
                    if (string.IsNullOrWhiteSpace(dt.Rows[r][k]?.ToString()))
                    {
                        dt.Rows[r][k] = dt.Rows[r - 1][k];
                        AutoFilledKeys.Add((r, k));
                    }
                }
            }

            // Добавляем вычисляемый столбец _ArticleKey_
            EnsureArticleKeyColumn(dt, keys);

            // Удаляем строки, где _ArticleKey_ пустой
            for (int i = dt.Rows.Count - 1; i >= 0; i--)
            {
                string key = dt.Rows[i]["_ArticleKey_"]?.ToString();
                if (string.IsNullOrWhiteSpace(key))
                {
                    InvalidRows.Add(i);
                }
            }

            ProcessedDataChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Получает список ключевых столбцов.
        /// </summary>
        public List<string> GetKeyColumns()
        {
            if (ColumnConfigTable == null) return new List<string>();
            return ColumnConfigTable.AsEnumerable()
                .Where(r => r["IsKey"] != DBNull.Value && (bool)r["IsKey"])
                .Select(r => r["ColName"].ToString())
                .ToList();
        }

        /// <summary>
        /// Получает список столбцов указанного типа.
        /// </summary>
        public List<string> GetColumnsByType(string colType)
        {
            if (ColumnConfigTable == null) return new List<string>();
            return ColumnConfigTable.AsEnumerable()
                .Where(r => r[colType] != DBNull.Value && (bool)r[colType])
                .Select(r => r["ColName"].ToString())
                .ToList();
        }

        /// <summary>
        /// Получает значение цвета для столбца.
        /// </summary>
        public bool GetColumnIsColorChecked(string columnName)
        {
            if (ColumnConfigTable == null) return false;
            var row = ColumnConfigTable.AsEnumerable()
                .FirstOrDefault(r => r["ColName"]?.ToString() == columnName);
            if (row == null || row["IsColor"] == DBNull.Value) return false;
            return (bool)row["IsColor"];
        }

        /// <summary>
        /// Получает DataView отфильтрованный по артикулу.
        /// </summary>
        /// <param name="articleKey">Ключ артикула.</param>
        /// <returns>DataView с отфильтрованными строками.</returns>
        public DataView GetArticleView(string articleKey)
        {
            if (ProcessedDataTable == null) return null;

            var view = new DataView(ProcessedDataTable);
            if (string.IsNullOrEmpty(articleKey))
            {
                view.RowFilter = "([_ArticleKey_] IS NULL OR [_ArticleKey_] = '')";
            }
            else
            {
                var escapedKey = articleKey.Replace("'", "''");
                view.RowFilter = $"[_ArticleKey_] = '{escapedKey}'";
            }
            return view;
        }

        /// <summary>
        /// Получает список уникальных артикулов.
        /// </summary>
        public List<string> GetUniqueArticles()
        {
            if (ProcessedDataTable == null) return new List<string>();

            var articles = new List<string>();
            foreach (DataRow row in ProcessedDataTable.Rows)
            {
                string key = row["_ArticleKey_"]?.ToString();
                if (!string.IsNullOrEmpty(key) && !articles.Contains(key))
                {
                    articles.Add(key);
                }
            }
            return articles;
        }

        /// <summary>
        /// Гарантирует наличие столбца _ArticleKey_ в таблице.
        /// </summary>
        private void EnsureArticleKeyColumn(DataTable dt, List<string> keyColumns)
        {
            if (dt.Columns.Contains("_ArticleKey_"))
            {
                dt.Columns.Remove("_ArticleKey_");
            }
            dt.Columns.Add("_ArticleKey_", typeof(string));

            foreach (DataRow row in dt.Rows)
            {
                row["_ArticleKey_"] = Models.DataHelper.GetArticleName(
                    keyColumns.Select(k => row[k]?.ToString()));
            }
        }

        /// <summary>
        /// Очищает хранилище.
        /// </summary>
        public void Clear()
        {
            RawDataTable = null;
            ProcessedDataTable = null;
            ColumnConfigTable = null;
            CurrentFilePath = null;
            InvalidRows.Clear();
            AutoFilledKeys.Clear();
        }
    }
}