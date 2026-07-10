using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
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
            /// <summary>Описание артикула (наименование и цвет из данных).</summary>
            public string ArticleDescription { get; set; }
            /// <summary>Флаг, указывающий, что данный результат относится к ручному раскрою.</summary>
            public bool IsManualCut { get; set; }
            /// <summary>Исходный ключ артикула (без префикса "Ручной раскрой"), используется для поиска наименования.</summary>
            public string OriginalGroupKey { get; set; }
            public DataTable ResultTable { get; set; } = new DataTable();
            public double TotalPartsLength { get; set; }
            public double TotalStockLength { get; set; }
            public int TotalPartsCount { get; set; }
            public double TotalRemainderLength { get; set; }
            public Dictionary<double, int> UsedStocks { get; set; } = new Dictionary<double, int>();
            public double MaterialUtilizationRate => TotalStockLength > 0 ? (TotalPartsLength / TotalStockLength * 100.0) : 0;
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
        /// Проверяет целостность результатов после перемещения хлыстов ручного раскроя.
        /// Возвращает список ошибок (пустой список = всё в порядке).
        /// </summary>
        /// <param name="results">Список результатов оптимизации.</param>
        /// <param name="groups">Входные данные групп.</param>
        /// <param name="cutWidth">Ширина реза для нормализации длин деталей.</param>
        /// <returns>Список строк с описанием найденных ошибок.</returns>
        public List<string> VerifyManualBarsIntegrity(List<OptimizationResult> results, IEnumerable<GroupData> groups, double cutWidth)
        {
            var errors = new List<string>();

            // 1. Ни один хлыст с IsFromManualCut=true не должен остаться в автоматических результатах
            foreach (var result in results.Where(r => !r.IsManualCut))
            {
                if (result.DetailedBars != null)
                {
                    foreach (var bar in result.DetailedBars)
                    {
                        if (bar.IsFromManualCut)
                        {
                            errors.Add($"Артикул '{result.GroupKey}': хлыст StockLength={bar.StockLength} с IsFromManualCut=true не должен быть в автоматическом результате.");
                        }
                    }
                }
            }

            // 2. В ручном результате: ManualParts у хлыстов с IsFromManualCut должен быть заполнен
            var manualResult = results.FirstOrDefault(r => r.IsManualCut);
            if (manualResult?.DetailedBars != null)
            {
                foreach (var bar in manualResult.DetailedBars.Where(b => b.IsFromManualCut))
                {
                    if (bar.ManualParts == null || bar.ManualParts.Count == 0)
                    {
                        errors.Add($"Хлыст StockLength={bar.StockLength}: ManualParts пуст, хотя хлыст из ручного раскроя.");
                    }
                }
            }

            // 3. Статистика ручного результата: TotalPartsCount и TotalPartsLength должны быть > 0
            if (manualResult != null)
            {
                if (manualResult.TotalPartsCount <= 0)
                {
                    errors.Add("Ручной результат: TotalPartsCount = 0, хотя ручной раскрой существует.");
                }
                if (manualResult.TotalPartsLength <= 0)
                {
                    errors.Add("Ручной результат: TotalPartsLength = 0, хотя ручной раскрой существует.");
                }
            }

            // 4. Проверка: сумма количеств деталей определённой длины в ручном + автоматическом = входные данные
            var inputCounts = new Dictionary<double, int>();
            foreach (var group in groups ?? Enumerable.Empty<GroupData>())
            {
                var groupDt = group?.Table;
                if (groupDt == null) continue;

                foreach (DataRow row in groupDt.Rows)
                {
                    var valObj = row[group.ValueColumnName];
                    double l = (valObj == null || valObj == DBNull.Value) ? 0 : Convert.ToDouble(valObj);
                    if (l <= 0) continue;

                    if (string.IsNullOrEmpty(group.QtyColumnName))
                        throw new InvalidOperationException($"QtyColumnName is required for group '{group.GroupKey}'.");
                    int count = Convert.ToInt32(row[group.QtyColumnName]);
                    if (count <= 0) continue;

                    if (inputCounts.ContainsKey(l))
                        inputCounts[l] += count;
                    else
                        inputCounts[l] = count;
                }
            }

            // Собираем количества из автоматических результатов
            var outputCounts = new Dictionary<double, int>();
            foreach (var result in results.Where(r => !r.IsManualCut))
            {
                if (result.DetailedBars != null)
                {
                    foreach (var bar in result.DetailedBars)
                    {
                        foreach (var part in bar.Parts)
                        {
                            double rawLen = part.Length - cutWidth;
                            if (outputCounts.ContainsKey(rawLen))
                                outputCounts[rawLen]++;
                            else
                                outputCounts[rawLen] = 1;
                        }
                    }
                }
            }

            // Собираем количества из ручного результата
            if (manualResult?.ResultTable != null)
            {
                foreach (DataRow row in manualResult.ResultTable.Rows)
                {
                    var partsStr = row["Раскрой"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(partsStr))
                    {
                        foreach (var part in partsStr.Split(new[] { " + " }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            if (double.TryParse(part, System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out double len))
                            {
                                if (outputCounts.ContainsKey(len))
                                    outputCounts[len]++;
                                else
                                    outputCounts[len] = 1;
                            }
                        }
                    }
                }
            }

            // Сравниваем: для каждой длины входное количество должно совпадать с выходным
            foreach (var kvp in inputCounts)
            {
                double length = kvp.Key;
                int inputCount = kvp.Value;
                int outputCount = outputCounts.TryGetValue(length, out int c) ? c : 0;

                if (inputCount != outputCount)
                {
                    errors.Add($"Деталь длиной {length} мм: на входе {inputCount} шт, в раскрое (авто+ручной) {outputCount} шт.");
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
                    double l = (valObj == null || valObj == DBNull.Value) ? 0 : Convert.ToDouble(valObj);
                    if (l <= 0) continue;

                    if (string.IsNullOrEmpty(group.QtyColumnName))
                        throw new InvalidOperationException($"QtyColumnName is required for group '{group.GroupKey}'.");
                    int count = Convert.ToInt32(row[group.QtyColumnName]);

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
            var preFilledBars = new List<PreFilledBar>();
            
            // Расчет частично заполненных хлыстов из ручного раскроя
            if (manualCuts != null)
            {
                double reduction = (preset.TrimStart - preset.CutWidth / 2) + (preset.TrimEnd - preset.CutWidth / 2);
                foreach (var mr in manualCuts)
                {
                    if (mr.UseRemainders && mr.BarLength.HasValue && mr.BarLength.Value > 0)
                    {
                        double capacity = mr.BarLength.Value - reduction;
                        var manualBarParts = new List<double>();
                        double used = 0;
                        int count = mr.Count > 0 ? mr.Count : 1;

                        if (double.TryParse(mr.Size1, out var s1) && s1 > 0) { manualBarParts.Add(s1); used += s1 + preset.CutWidth; }
                        if (double.TryParse(mr.Size2, out var s2) && s2 > 0) { manualBarParts.Add(s2); used += s2 + preset.CutWidth; }
                        if (double.TryParse(mr.Size3, out var s3) && s3 > 0) { manualBarParts.Add(s3); used += s3 + preset.CutWidth; }
                        if (double.TryParse(mr.Size4, out var s4) && s4 > 0) { manualBarParts.Add(s4); used += s4 + preset.CutWidth; }

                        double freeSpace = capacity - used;
                        if (freeSpace > 0)
                        {
                            for (int i = 0; i < count; i++)
                            {
                                preFilledBars.Add(new PreFilledBar
                                {
                                    StockLength = mr.BarLength.Value,
                                    ManualParts = new List<double>(manualBarParts),
                                    FreeSpace = freeSpace
                                });
                            }
                        }
                    }
                }
                
                enabledStocks = enabledStocks.OrderByDescending(s => s).ToList();
            }

            var results = new List<OptimizationResult>();
            var allManualBarsFromAuto = new List<CutBarDetailed>();

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
                    double l = (valObj == null || valObj == DBNull.Value) ? 0 : Convert.ToDouble(valObj);
                    if (l <= 0) 
                    {
                        originalRowIndex++;
                        continue;
                    }

                    if (string.IsNullOrEmpty(group.QtyColumnName))
                        throw new InvalidOperationException($"QtyColumnName is required for group '{group.GroupKey}'.");
                    double lWithCut = l + preset.CutWidth;
                    int count = Convert.ToInt32(row[group.QtyColumnName]);

                    for (int i = 0; i < count; i++)
                    {
                        var mIdx = manualParts.FindIndex(mp => mp == lWithCut);
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

                var detailedBars = CutOptimizer.Optimize(pts, enabledStocks, preFilledBars, preset.TrimStart, preset.TrimEnd, preset.CutWidth);

                // Разделяем хлысты: автоматические и хлысты-остатки ручного раскроя (IsFromManualCut)
                var autoDetailedBars = detailedBars.Where(db => !db.IsFromManualCut).ToList();
                var manualBarsFromAuto = detailedBars.Where(db => db.IsFromManualCut).ToList();
                allManualBarsFromAuto.AddRange(manualBarsFromAuto);

                // Пересчитываем totP только для автоматических деталей
                double autoTotP = autoDetailedBars.Sum(db => db.Parts.Sum(p => p.Length - preset.CutWidth));
                int autoPartsCount = autoDetailedBars.Sum(db => db.Parts.Count);

                // Конвертируем обратно в обычные CutBar (только автоматические)
                var cutBars = autoDetailedBars.Select(db => new CutBar
                {
                    StockLength = db.StockLength,
                    Parts = string.Join(" + ", db.Parts.Select(p => p.Length - preset.CutWidth)),
                    Remainder = db.Remainder
                }).ToList();

                var usedStocks = new Dictionary<double, int>();
                foreach (var cb in cutBars)
                {
                    if (!usedStocks.ContainsKey(cb.StockLength))
                        usedStocks[cb.StockLength] = 0;
                    usedStocks[cb.StockLength]++;
                }

                if (autoPartsCount > 0)
                {
                    results.Add(new OptimizationResult
                    {
                        GroupKey = groupKey,
                        TotalPartsLength = autoTotP,
                        TotalPartsCount = autoPartsCount,
                        TotalStockLength = cutBars.Sum(r => r.StockLength),
                        TotalRemainderLength = cutBars.Sum(r => r.Remainder),
                        UsedStocks = usedStocks,
                        ResultTable = BuildResultTable(cutBars),
                        DetailedBars = autoDetailedBars
                    });
                }
            }

            if (manualCuts != null && manualCuts.Any())
            {
                var manualCutBars = BuildManualCutBars(manualCuts, preset);
                if (manualCutBars.Any() || allManualBarsFromAuto.Any())
                {
                    // Заменяем ручные хлысты их «улучшенными» версиями (ручные + автоматические детали)
                    var allManualBars = new List<CutBar>(manualCutBars);
                    foreach (var autoBar in allManualBarsFromAuto)
                    {
                        // Объединяем ручные детали + автоматически размещённые
                        var allParts = (autoBar.ManualParts ?? new List<double>())
                            .Concat(autoBar.Parts.Select(p => p.Length - preset.CutWidth))
                            .ToList();

                        var upgradedBar = new CutBar
                        {
                            StockLength = autoBar.StockLength,
                            Parts = string.Join(" + ", allParts),
                            Remainder = autoBar.Remainder
                        };

                        // Ищем оригинальный ручной хлыст по совпадению StockLength и только ручных деталей
                        var manualPartsStr = autoBar.ManualParts != null ? string.Join(" + ", autoBar.ManualParts) : "";
                        var matchIdx = allManualBars.FindIndex(mb => mb.StockLength == autoBar.StockLength && mb.Parts == manualPartsStr);
                        if (matchIdx >= 0)
                            allManualBars[matchIdx] = upgradedBar; // Заменяем
                        else
                            allManualBars.Add(upgradedBar); // Не нашли — добавляем
                    }

                    var manualUsedStocks = new Dictionary<double, int>();
                    foreach (var cb in allManualBars)
                    {
                        if (!manualUsedStocks.ContainsKey(cb.StockLength))
                            manualUsedStocks[cb.StockLength] = 0;
                        manualUsedStocks[cb.StockLength]++;
                    }

                    var manualGroupKey = groups?.FirstOrDefault()?.GroupKey ?? "Неизвестный артикул";

                    // Статистика: все детали на ручных хлыстах (ручные + автоматически размещённые)
                    double manualPartsLength = 0;
                    int manualPartsCount = 0;
                    foreach (var bar in allManualBars)
                    {
                        var partsArr = bar.Parts.Split(new[] { " + " }, StringSplitOptions.RemoveEmptyEntries);
                        manualPartsCount += partsArr.Length;
                        foreach (var p in partsArr)
                            manualPartsLength += double.Parse(p);
                    }

                    results.Add(new OptimizationResult
                    {
                        GroupKey = $"{manualGroupKey} (Ручной раскрой)",
                        IsManualCut = true,
                        OriginalGroupKey = manualGroupKey,
                        TotalPartsLength = manualPartsLength,
                        TotalPartsCount = manualPartsCount,
                        TotalStockLength = allManualBars.Sum(r => r.StockLength),
                        TotalRemainderLength = allManualBars.Sum(r => r.Remainder),
                        UsedStocks = manualUsedStocks,
                        ResultTable = BuildResultTable(allManualBars),
                        DetailedBars = allManualBarsFromAuto
                    });
                }
            }

            Diagnostic.LogCountDiagnostics(groups, results, enabledStocks, preFilledBars, manualParts);

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