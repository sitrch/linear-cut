using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using LinearCutWpf.Models;

namespace LinearCutWpf.Services
{
    public static class Diagnostic
    {
        public static bool Enabled = false;

        private static string LogPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "diagnostics.log");

        public static void LogArticleViewDiagnostics(DataView articleView, string valColumnName, string qtyCol, string article)
        {
            if (!Enabled) return;
            try
            {
                int valCount = 0;
                foreach (DataRowView drv in articleView)
                {
                    var vo = drv[valColumnName];
                    double vl = (vo == null || vo == DBNull.Value) ? 0 : Convert.ToDouble(vo);
                    if (vl <= 0) continue;
                    int c = Convert.ToInt32(drv[qtyCol]);
                    if (c <= 0) continue;
                    valCount += c;
                }
                File.AppendAllText(LogPath, $"=== MainWindow groupedData build [{article}] ===\n");
                File.AppendAllText(LogPath, $"  articleView rows: {articleView.Count}\n");
                File.AppendAllText(LogPath, $"  expected from articleView: {valCount}\n");
                File.AppendAllText(LogPath, $"  GCT rows count: {DataStoreService.Instance.GroupedAndCleanDataTable.Rows.Count}\n");
                File.AppendAllText(LogPath, $"=== End groupedData build ===\n\n");
            }
            catch { }
        }

        public static void LogGroupedDataDiagnostics(Dictionary<string, DataRow[]> groupedData, string valColumnName, string qtyColumnName)
        {
            if (!Enabled) return;
            try
            {
                using (var w = new StreamWriter(LogPath, append: true))
                {
                    w.WriteLine($"=== ValidateVisualReportData [{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ===");
                    foreach (var kvp in groupedData)
                    {
                        var rows = kvp.Value;
                        if (rows == null || rows.Length == 0) continue;
                        var lengths = new List<double>();
                        foreach (var row in rows)
                        {
                            double len = row[valColumnName] != DBNull.Value ? Convert.ToDouble(row[valColumnName]) : 0;
                            if (len <= 0) continue;
                            int q = Convert.ToInt32(row[qtyColumnName]);
                            for (int i = 0; i < q; i++) lengths.Add(Math.Round(len, 2));
                        }
                        w.WriteLine($"  {kvp.Key}: expected={lengths.Count} (from groupedData)");
                    }
                    w.WriteLine($"=== End ValidateVisualReportData ===");
                    w.WriteLine();
                }
            }
            catch { }
        }

        public static void LogDtBuildDiagnostics(DataView articleView, DataTable dt, string valColumnName, string qtyCol, string groupKey)
        {
            if (!Enabled) return;
            try
            {
                int dtCount = 0;
                foreach (DataRow r in dt.Rows)
                {
                    var vo = r[valColumnName];
                    double vl = (vo == null || vo == DBNull.Value) ? 0 : Convert.ToDouble(vo);
                    if (vl <= 0) continue;
                    int c = Convert.ToInt32(r[qtyCol]);
                    if (c <= 0) continue;
                    dtCount += c;
                }
                int avCount = 0;
                foreach (DataRowView drv in articleView)
                {
                    var vo = drv[valColumnName];
                    double vl = (vo == null || vo == DBNull.Value) ? 0 : Convert.ToDouble(vo);
                    if (vl <= 0) continue;
                    int c = Convert.ToInt32(drv[qtyCol]);
                    if (c <= 0) continue;
                    avCount += c;
                }
                File.AppendAllText(LogPath, $"=== MainWindow dt build [{groupKey}] ===\n");
                File.AppendAllText(LogPath, $"  articleView rows: {articleView.Count}\n");
                File.AppendAllText(LogPath, $"  dt rows after import: {dt.Rows.Count}\n");
                File.AppendAllText(LogPath, $"  expected from articleView: {avCount}\n");
                File.AppendAllText(LogPath, $"  expected from dt: {dtCount}\n");
                File.AppendAllText(LogPath, $"=== End MainWindow dt build ===\n\n");
            }
            catch { }
        }

        public static void LogCountDiagnostics(
            IEnumerable<CuttingService.GroupData> groups,
            List<CuttingService.OptimizationResult> results,
            List<double> enabledStocks,
            List<PreFilledBar> preFilledBars,
            List<double> manualParts)
        {
            if (!Enabled) return;

            using (var w = new StreamWriter(LogPath, append: true))
            {
                w.WriteLine($"=== Count Diagnostics [{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ===");
                w.WriteLine($"Stocks: [{string.Join(", ", enabledStocks)}]");
                w.WriteLine($"PreFilled bars: {preFilledBars?.Count ?? 0}");
                w.WriteLine($"Manual parts: {manualParts?.Count ?? 0}");
                w.WriteLine();

                bool anyMismatch = false;

                foreach (var group in groups ?? Enumerable.Empty<CuttingService.GroupData>())
                {
                    if (group?.Table == null) continue;

                    int expected = 0;
                    var partLengths = new List<double>();
                    foreach (DataRow row in group.Table.Rows)
                    {
                        var valObj = row[group.ValueColumnName];
                        double l = (valObj == null || valObj == DBNull.Value) ? 0 : Convert.ToDouble(valObj);
                        if (l <= 0) continue;

                        if (string.IsNullOrEmpty(group.QtyColumnName))
                            throw new InvalidOperationException($"QtyColumnName is required for group '{group.GroupKey}'.");
                        int count = Convert.ToInt32(row[group.QtyColumnName]);
                        if (count <= 0) continue;

                        expected += count;
                        for (int i = 0; i < count; i++)
                            partLengths.Add(l);
                    }

                    var result = results?.FirstOrDefault(r => r.GroupKey == group.GroupKey && !r.IsManualCut);
                    int actual = result?.TotalPartsCount ?? 0;
                    int diff = expected - actual;

                    if (diff != 0)
                    {
                        anyMismatch = true;
                        w.WriteLine($"==== Артикул: {group.GroupKey} ====");
                        w.WriteLine($"  Expected: {expected}, Actual: {actual}, Diff: {diff}");

                        var lengthGroups = partLengths.GroupBy(l => l).Select(g => $"{g.Key}(x{g.Count()})");
                        w.WriteLine($"  Длины деталей: {string.Join(", ", lengthGroups)}");
                        w.WriteLine($"  Всего деталей (expected): {partLengths.Count}");

                        double maxPartLen = partLengths.Any() ? partLengths.Max() : 0;
                        double maxStock = enabledStocks.Any() ? enabledStocks.Max() : 0;
                        w.WriteLine($"  Самая длинная деталь: {maxPartLen} мм");
                        w.WriteLine($"  Самый длинный хлыст: {maxStock} мм");

                        if (maxStock > 0)
                        {
                            var tooLong = partLengths.Where(l => l > maxStock).ToList();
                            w.WriteLine($"  Деталей длиннее макс. хлыста ({maxStock} мм): {tooLong.Count} шт");
                            if (tooLong.Any())
                            {
                                var longGroups = tooLong.GroupBy(l => l).Select(g => $"{g.Key}(x{g.Count()})");
                                w.WriteLine($"    Длины: {string.Join(", ", longGroups)}");
                            }
                        }

                        var autoBars = result?.DetailedBars;
                        if (autoBars != null)
                        {
                            int prefilledCount = autoBars.Count(b => b.IsFromManualCut);
                            int infiniteCount = autoBars.Count(b => !b.IsFromManualCut);
                            w.WriteLine($"  Баров prefilled (ручной раскрой): {prefilledCount}");
                            w.WriteLine($"  Баров infinite (автомат): {infiniteCount}");
                            w.WriteLine($"  Всего баров: {autoBars.Count}");
                            w.WriteLine($"  Деталей в барах: {autoBars.Sum(b => b.Parts.Count)}");
                        }
                        else
                        {
                            w.WriteLine($"  DetailedBars: null (нет результата)");
                        }

                        w.WriteLine($"  Manual parts: {manualParts?.Count ?? 0}");
                        w.WriteLine();
                    }
                }

                if (!anyMismatch)
                {
                    w.WriteLine("  Все артикулы: expected == actual, расхождений нет.");
                    w.WriteLine();
                }

                w.WriteLine("--- Сводная таблица (expected vs actual) ---");
                w.WriteLine($"{"Артикул",-30} {"Expected",10} {"Actual",10} {"Diff",10}");
                w.WriteLine(new string('-', 60));

                foreach (var group in groups ?? Enumerable.Empty<CuttingService.GroupData>())
                {
                    if (group?.Table == null) continue;

                    if (string.IsNullOrEmpty(group.QtyColumnName))
                        throw new InvalidOperationException($"QtyColumnName is required for group '{group.GroupKey}'.");

                    int expected = 0;
                    foreach (DataRow row in group.Table.Rows)
                    {
                        var valObj = row[group.ValueColumnName];
                        double l = (valObj == null || DBNull.Value.Equals(valObj)) ? 0 : Convert.ToDouble(valObj);
                        if (l <= 0) continue;
                        int count = Convert.ToInt32(row[group.QtyColumnName]);
                        if (count <= 0) continue;
                        expected += count;
                    }

                    var result = results?.FirstOrDefault(r => r.GroupKey == group.GroupKey && !r.IsManualCut);
                    int actual = result?.TotalPartsCount ?? 0;
                    int diff = expected - actual;

                    w.WriteLine($"{group.GroupKey,-30} {expected,10} {actual,10} {diff,10}");
                }

                w.WriteLine(new string('-', 60));
                w.WriteLine($"=== End Diagnostics ===");
                w.WriteLine();
            }
        }
    }
}
