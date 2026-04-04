using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using LinearCutWpf.Models;

namespace LinearCutWpf.Services
{
    /// <summary>
    /// Сервис, управляющий бизнес-логикой подготовки данных для раскроя, валидацией ручных резов и объединением результатов.
    /// Является связующим звеном между UI (MainWindow) и математическим движком (CutOptimizer).
    /// </summary>
    public class CuttingService
    {
        /// <summary>
        /// Модель ошибки валидации строки ручного раскроя.
        /// </summary>
        public class ManualValidationError
        {
            public int RowIndex { get; set; }
            public string ColumnName { get; set; }
            public string Message { get; set; }
        }

        /// <summary>
        /// Модель, содержащая итоговые результаты раскроя для одной группы (артикула).
        /// </summary>
        public class OptimizationResult
        {
            public string GroupKey { get; set; }
            public DataTable ResultTable { get; set; } = new DataTable();
            public double TotalPartsLength { get; set; }
            public double TotalStockLength { get; set; }
            public int TotalPartsCount { get; set; }
            public double TotalRemainderLength { get; set; }
            public Dictionary<double, int> UsedStocks { get; set; } = new Dictionary<double, int>();
            public double KpdPercent => TotalStockLength > 0 ? (TotalPartsLength / TotalStockLength * 100.0) : 0;
            public List<CutBarDetailed> DetailedBars { get; set; } = new List<CutBarDetailed>();
        }

        /// <summary>
        /// Входные данные для раскроя сгруппированные по артикулу (GroupKey).
        /// </summary>
        public class GroupData
        {
            public string GroupKey { get; set; }
            public string ValueColumnName { get; set; }
            public string QtyColumnName { get; set; }
            public DataTable Table { get; set; }
        }

        /// <summary>
        /// Проверяет, что детали, введенные пользователем вручную в таблице "Ручной раскрой",
        /// помещаются в выбранный хлыст с учетом ширины реза и припусков на торцевание.
        /// </summary>
        /// <param name="manualCuts">Список строк ручного раскроя.</param>
        /// <param name="preset">Текущие настройки реза.</param>
        /// <returns>Список найденных ошибок.</returns>
        public List<ManualValidationError> ValidateManualCuts(
            IReadOnlyList<ManualCutRow> manualCuts,
            PresetModel preset)
        {
            var errors = new List<ManualValidationError>();
            if (preset == null) preset = new PresetModel { TrimStart = 10, TrimEnd = 10, CutWidth = 4 };

            double reduction = (preset.TrimStart - preset.CutWidth / 2) + (preset.TrimEnd - preset.CutWidth / 2);
            string[] sizeCols = { "Size1", "Size2", "Size3", "Size4" };

            for (int rowIndex = 0; rowIndex < (manualCuts?.Count ?? 0); rowIndex++)
            {
                var row = manualCuts[rowIndex];
                if (row?.BarLength == null) continue;

                double stock = row.BarLength.Value;
                double used = 0;

                foreach (var col in sizeCols)
                {
                    var raw = GetSizeValue(row, col);
                    if (string.IsNullOrWhiteSpace(raw)) continue;

                    double val = double.TryParse(raw, out var d) ? d : 0;
                    if (val <= 0) continue;

                    if (used + val + preset.CutWidth > (stock - reduction))
                    {
                        errors.Add(new ManualValidationError
                        {
                            RowIndex = rowIndex,
                            ColumnName = col,
                            Message = "Деталь не помещается в хлыст с учетом припусков/реза."
                        });
                    }
                    else
                    {
                        used += (val + preset.CutWidth);
                    }
                }
            }

            return errors;
        }

        /// <summary>
        /// Проверяет, совпадает ли общее количество и длина деталей на входе (groups)
        /// с количеством и длиной деталей, размещенных в результате раскроя (results).
        /// </summary>
        /// <param name="groups">Входные данные.</param>
        /// <param name="results">Результаты раскроя.</param>
        /// <returns>True если данные сходятся (с погрешностью < 0.1), иначе False.</returns>
        public bool VerifyOptimization(IEnumerable<GroupData> groups, List<OptimizationResult> results)
        {
            int inputCount = 0;
            double inputLength = 0;

            foreach (var group in groups ?? Enumerable.Empty<GroupData>())
            {
                var groupDt = group?.Table;
                if (groupDt == null) continue;

                foreach (DataRow row in groupDt.Rows)
                {
                    var valObj = row[group.ValueColumnName];
                    var qtyObj = string.IsNullOrEmpty(group.QtyColumnName) ? null : row[group.QtyColumnName];

                    double l = (valObj == null || valObj == DBNull.Value) ? 0 : Convert.ToDouble(valObj);
                    if (l <= 0) continue;

                    int count = (qtyObj == null || qtyObj == DBNull.Value || string.IsNullOrWhiteSpace(qtyObj.ToString())) ? 1 : Convert.ToInt32(qtyObj);
                    
                    inputCount += count;
                    inputLength += l * count;
                }
            }

            int outputCount = results.Sum(r => r.TotalPartsCount);
            double outputLength = results.Sum(r => r.TotalPartsLength);

            return inputCount == outputCount && Math.Abs(inputLength - outputLength) < 0.1;
        }

        /// <summary>
        /// Главный метод запуска процесса раскроя. Подготавливает данные: вычитает детали, уже распиленные вручную,
        /// формирует список доступных остатков из ручного раскроя, затем вызывает <see cref="CutOptimizer"/>
        /// для каждой группы деталей (артикула) по очереди.
        /// </summary>
        /// <param name="groups">Список групп данных из загруженного Excel файла.</param>
        /// <param name="stocks">Список доступных длин хлыстов.</param>
        /// <param name="manualCuts">Список деталей, раскроенных вручную пользователем.</param>
        /// <param name="preset">Настройки реза (ширина реза, торцовка).</param>
        /// <returns>Список объектов OptimizationResult с результатами раскроя для отображения в UI.</returns>
        public List<OptimizationResult> OptimizeAllGroups(
            IEnumerable<GroupData> groups,
            IEnumerable<StockLengthModel> stocks,
            IReadOnlyList<ManualCutRow> manualCuts,
            PresetModel preset)
        {
            if (preset == null) preset = new PresetModel { TrimStart = 10, TrimEnd = 10, CutWidth = 4 };

            var manualParts = BuildManualParts(manualCuts, preset);
            var enabledStocks = stocks?.Where(x => x.IsEnabled).Select(x => x.Length).ToList() ?? new List<double>();
            var finiteStocks = new List<double>();
            
            // Расчет остатков из ручного раскроя
            if (manualCuts != null)
            {
                double reduction = (preset.TrimStart - preset.CutWidth / 2) + (preset.TrimEnd - preset.CutWidth / 2);
                foreach (var mr in manualCuts)
                {
                    if (mr.UseRemainders && mr.BarLength.HasValue && mr.BarLength.Value > 0)
                    {
                        double capacity = mr.BarLength.Value - reduction;
                        double used = 0;
                        int count = mr.Count > 0 ? mr.Count : 1;

                        if (double.TryParse(mr.Size1, out var s1) && s1 > 0) used += s1 + preset.CutWidth;
                        if (double.TryParse(mr.Size2, out var s2) && s2 > 0) used += s2 + preset.CutWidth;
                        if (double.TryParse(mr.Size3, out var s3) && s3 > 0) used += s3 + preset.CutWidth;
                        if (double.TryParse(mr.Size4, out var s4) && s4 > 0) used += s4 + preset.CutWidth;

                        double remainder = capacity - used;
                        if (remainder > 0)
                        {
                            for (int i = 0; i < count; i++)
                            {
                                finiteStocks.Add(remainder);
                            }
                        }
                    }
                }
                
                // Сортируем по убыванию, чтобы большие остатки использовались в первую очередь
                enabledStocks = enabledStocks.OrderByDescending(s => s).ToList();
                finiteStocks = finiteStocks.OrderByDescending(s => s).ToList();
            }

            var results = new List<OptimizationResult>();

            foreach (var group in groups ?? Enumerable.Empty<GroupData>())
            {
                var groupKey = group?.GroupKey ?? "";
                var groupDt = group?.Table;
                if (groupDt == null) continue;

                if (string.IsNullOrWhiteSpace(group.ValueColumnName))
                    throw new InvalidOperationException("ValueColumnName is required for optimization.");

                var pts = new List<PartItem>();
                double totP = 0;
                int originalRowIndex = 0;

                foreach (DataRow row in groupDt.Rows)
                {
                    var valObj = row[group.ValueColumnName];
                    var qtyObj = string.IsNullOrEmpty(group.QtyColumnName) ? null : row[group.QtyColumnName];

                    double l = (valObj == null || valObj == DBNull.Value) ? 0 : Convert.ToDouble(valObj);
                    if (l <= 0) 
                    {
                        originalRowIndex++;
                        continue;
                    }

                    double lWithCut = l + preset.CutWidth;
                    int count = (qtyObj == null || qtyObj == DBNull.Value || string.IsNullOrWhiteSpace(qtyObj.ToString())) ? 1 : Convert.ToInt32(qtyObj);

                    for (int i = 0; i < count; i++)
                    {
                        var mIdx = manualParts.FindIndex(mp => Math.Abs(mp - lWithCut) < 0.1);
                        if (mIdx >= 0)
                        {
                            manualParts.RemoveAt(mIdx);
                        }
                        else
                        {
                            pts.Add(new PartItem 
                            { 
                                Article = groupKey, 
                                Length = lWithCut, 
                                Count = 1, 
                                OriginalRowIndex = originalRowIndex 
                            });
                            totP += l;
                        }
                    }
                    originalRowIndex++;
                }

                var detailedBars = CutOptimizer.Optimize(pts, enabledStocks, finiteStocks, preset.TrimStart, preset.TrimEnd, preset.CutWidth);

                // Конвертируем обратно в обычные CutBar для обратной совместимости таблицы
                var cutBars = detailedBars.Select(db => new CutBar
                {
                    StockLength = db.StockLength,
                    Parts = string.Join(" + ", db.Parts.Select(p => p.Length - preset.CutWidth)),
                    Remainder = db.Remainder
                }).ToList();

                // Update finiteStocks removing used remainders (approximate tracking, exact matching might need CutOptimizer changes, but CutOptimizer handles it internally during a single Optimize call. 
                // Note: If there are multiple groups, finiteStocks are reused across groups currently. Let's assume manual cuts are per-group, so this is correct. Wait, manual cuts are per article (group).
                // OptimizeAllGroups processes one group at a time in MainWindow, but here it's an IEnumerable<GroupData>. In MainWindow, groupList contains 1 group.

                var usedStocks = new Dictionary<double, int>();
                foreach (var cb in cutBars)
                {
                    if (!usedStocks.ContainsKey(cb.StockLength))
                        usedStocks[cb.StockLength] = 0;
                    usedStocks[cb.StockLength]++;
                }

                results.Add(new OptimizationResult
                {
                    GroupKey = groupKey,
                    TotalPartsLength = totP,
                    TotalPartsCount = pts.Count,
                    TotalStockLength = cutBars.Sum(r => r.StockLength),
                    TotalRemainderLength = cutBars.Sum(r => r.Remainder),
                    UsedStocks = usedStocks,
                    ResultTable = BuildResultTable(cutBars),
                    DetailedBars = detailedBars
                });
            }

            if (manualCuts != null && manualCuts.Any())
            {
                var manualCutBars = BuildManualCutBars(manualCuts, preset);
                if (manualCutBars.Any())
                {
                    var manualUsedStocks = new Dictionary<double, int>();
                    foreach (var cb in manualCutBars)
                    {
                        if (!manualUsedStocks.ContainsKey(cb.StockLength))
                            manualUsedStocks[cb.StockLength] = 0;
                        manualUsedStocks[cb.StockLength]++;
                    }
                    
                    var manualGroupKey = groups?.FirstOrDefault()?.GroupKey ?? "Неизвестный артикул";

                    results.Add(new OptimizationResult
                    {
                        GroupKey = $"Ручной раскрой ({manualGroupKey})",
                        TotalPartsLength = manualCutBars.Sum(b => b.Parts.Split(new[] { " + " }, StringSplitOptions.RemoveEmptyEntries).Sum(p => double.Parse(p))),
                        TotalPartsCount = manualCutBars.Sum(b => b.Parts.Split(new[] { " + " }, StringSplitOptions.RemoveEmptyEntries).Length),
                        TotalStockLength = manualCutBars.Sum(r => r.StockLength),
                        TotalRemainderLength = manualCutBars.Sum(r => r.Remainder),
                        UsedStocks = manualUsedStocks,
                        ResultTable = BuildResultTable(manualCutBars)
                    });
                }
            }

            return results;
        }

        /// <summary>
        /// Преобразует строки из таблицы "Ручной раскрой" в формат результатов раскроя (CutBar).
        /// </summary>
        private static List<CutBar> BuildManualCutBars(IReadOnlyList<ManualCutRow> manualCuts, PresetModel preset)
        {
            var bars = new List<CutBar>();
            if (manualCuts == null) return bars;
            if (preset == null) preset = new PresetModel { TrimStart = 10, TrimEnd = 10, CutWidth = 4 };

            double reduction = (preset.TrimStart - preset.CutWidth / 2) + (preset.TrimEnd - preset.CutWidth / 2);

            foreach (var mr in manualCuts)
            {
                if (!mr.BarLength.HasValue || mr.BarLength.Value <= 0) continue;
                
                int count = mr.Count > 0 ? mr.Count : 1;
                for (int i = 0; i < count; i++)
                {
                    double used = 0;
                    var parts = new List<double>();
                    
                    if (double.TryParse(mr.Size1, out var s1) && s1 > 0) { parts.Add(s1); used += s1 + preset.CutWidth; }
                    if (double.TryParse(mr.Size2, out var s2) && s2 > 0) { parts.Add(s2); used += s2 + preset.CutWidth; }
                    if (double.TryParse(mr.Size3, out var s3) && s3 > 0) { parts.Add(s3); used += s3 + preset.CutWidth; }
                    if (double.TryParse(mr.Size4, out var s4) && s4 > 0) { parts.Add(s4); used += s4 + preset.CutWidth; }

                    if (parts.Count == 0) continue;

                    double capacity = mr.BarLength.Value - reduction;
                    double remainder = capacity - used;
                    if (remainder < 0) remainder = 0; 

                    bars.Add(new CutBar
                    {
                        StockLength = mr.BarLength.Value,
                        Parts = string.Join(" + ", parts),
                        Remainder = Math.Round(remainder, 2)
                    });
                }
            }
            return bars;
        }

        /// <summary>
        /// Группирует одинаковые хлысты (по длине и карте раскроя) и строит DataTable для отображения в DataGrid.
        /// </summary>
        private static DataTable BuildResultTable(List<CutBar> cutBars)
        {
            var rd = new DataTable();
            rd.Columns.Add("Кол-во", typeof(int));
            rd.Columns.Add("Хлыст", typeof(double));
            rd.Columns.Add("Раскрой", typeof(string));
            rd.Columns.Add("Остаток", typeof(double));

            foreach (var gb in (cutBars ?? new List<CutBar>()).GroupBy(r => $"{r.StockLength}::{r.Parts}"))
            {
                var first = gb.First();
                var nr = rd.NewRow();
                nr["Кол-во"] = gb.Count();
                nr["Хлыст"] = first.StockLength;
                nr["Раскрой"] = first.Parts;
                nr["Остаток"] = first.Remainder;
                rd.Rows.Add(nr);
            }

            return rd;
        }

        /// <summary>
        /// Собирает все детали из ручного раскроя в плоский список (с учетом ширины реза),
        /// чтобы позже вычесть их из общего списка деталей (исключить двойной рез).
        /// </summary>
        private static List<double> BuildManualParts(IReadOnlyList<ManualCutRow> manualCuts, PresetModel preset)
        {
            var manualParts = new List<double>();
            if (manualCuts == null) return manualParts;
            if (preset == null) preset = new PresetModel { TrimStart = 10, TrimEnd = 10, CutWidth = 4 };

            foreach (var mr in manualCuts)
            {
                int count = mr.Count > 0 ? mr.Count : 1;
                for (int i = 0; i < count; i++)
                {
                    if (double.TryParse(mr.Size1, out var s1) && s1 > 0) manualParts.Add(s1 + preset.CutWidth);
                    if (double.TryParse(mr.Size2, out var s2) && s2 > 0) manualParts.Add(s2 + preset.CutWidth);
                    if (double.TryParse(mr.Size3, out var s3) && s3 > 0) manualParts.Add(s3 + preset.CutWidth);
                    if (double.TryParse(mr.Size4, out var s4) && s4 > 0) manualParts.Add(s4 + preset.CutWidth);
                }
            }

            return manualParts;
        }

        /// <summary>
        /// Вспомогательный метод получения значения колонки из строки ручного раскроя.
        /// </summary>
        private static string GetSizeValue(ManualCutRow row, string col)
        {
            switch (col)
            {
                case "Size1": return row.Size1;
                case "Size2": return row.Size2;
                case "Size3": return row.Size3;
                case "Size4": return row.Size4;
                default: return null;
            }
        }
    }
}