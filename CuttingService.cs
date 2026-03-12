using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace LinearCutOptimization
{
    public class CuttingService
    {
        public class ManualValidationError
        {
            public int RowIndex { get; set; }
            public string ColumnName { get; set; }
            public string Message { get; set; }
        }

        public class OptimizationResult
        {
            public string GroupKey { get; set; }
            public DataTable ResultTable { get; set; } = new DataTable();
            public double TotalPartsLength { get; set; }
            public double TotalStockLength { get; set; }
            public double KpdPercent => TotalStockLength > 0 ? (TotalPartsLength / TotalStockLength * 100.0) : 0;
        }

        public class GroupData
        {
            public string GroupKey { get; set; }
            public string ValueColumnName { get; set; }
            public DataTable Table { get; set; }
        }

        public List<ManualValidationError> ValidateManualCuts(
            IReadOnlyList<ManualCutRow> manualCuts,
            PresetModel preset)
        {
            var errors = new List<ManualValidationError>();
            if (preset == null) return errors;

            double reduction = (preset.TrimStart - preset.CutWidth / 2) + (preset.TrimEnd - preset.CutWidth / 2);
            string[] sizeCols = { "Size1", "Size2", "Size3", "Size4" };

            for (int rowIndex = 0; rowIndex < (manualCuts?.Count ?? 0); rowIndex++)
            {
                var row = manualCuts[rowIndex];
                if (row?.StockLength == null) continue;

                double stock = row.StockLength.Value;
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

        public List<OptimizationResult> OptimizeAllGroups(
            IEnumerable<GroupData> groups,
            IEnumerable<StockModel> stocks,
            IReadOnlyList<ManualCutRow> manualCuts,
            PresetModel preset)
        {
            if (preset == null) return new List<OptimizationResult>();

            var manualParts = BuildManualParts(manualCuts, preset);
            var enabledStocks = stocks?.Where(x => x.IsEnabled).Select(x => x.Length).ToList() ?? new List<double>();

            var results = new List<OptimizationResult>();

            foreach (var group in groups ?? Enumerable.Empty<GroupData>())
            {
                var groupKey = group?.GroupKey ?? "";
                var groupDt = group?.Table;
                if (groupDt == null) continue;

                if (string.IsNullOrWhiteSpace(group.ValueColumnName))
                    throw new InvalidOperationException("ValueColumnName is required for optimization.");

                var pts = new List<double>();
                double totP = 0;

                foreach (DataRow row in groupDt.Rows)
                {
                    double l = Convert.ToDouble(row[group.ValueColumnName]);
                    double lWithCut = l + preset.CutWidth;
                    int count = Convert.ToInt32(row["Количество"]);

                    for (int i = 0; i < count; i++)
                    {
                        var mIdx = manualParts.FindIndex(mp => Math.Abs(mp - lWithCut) < 0.1);
                        if (mIdx >= 0)
                        {
                            manualParts.RemoveAt(mIdx);
                        }
                        else
                        {
                            pts.Add(lWithCut);
                            totP += l;
                        }
                    }
                }

                var cutBars = CutOptimizer.Optimize(pts, enabledStocks, preset.TrimStart, preset.TrimEnd, preset.CutWidth);

                results.Add(new OptimizationResult
                {
                    GroupKey = groupKey,
                    TotalPartsLength = totP,
                    TotalStockLength = cutBars.Sum(r => r.StockLength),
                    ResultTable = BuildResultTable(cutBars)
                });
            }

            return results;
        }

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

        private static List<double> BuildManualParts(IReadOnlyList<ManualCutRow> manualCuts, PresetModel preset)
        {
            var manualParts = new List<double>();
            if (manualCuts == null) return manualParts;

            foreach (var mr in manualCuts)
            {
                if (double.TryParse(mr.Size1, out var s1) && s1 > 0) manualParts.Add(s1 + preset.CutWidth);
                if (double.TryParse(mr.Size2, out var s2) && s2 > 0) manualParts.Add(s2 + preset.CutWidth);
                if (double.TryParse(mr.Size3, out var s3) && s3 > 0) manualParts.Add(s3 + preset.CutWidth);
                if (double.TryParse(mr.Size4, out var s4) && s4 > 0) manualParts.Add(s4 + preset.CutWidth);
            }

            return manualParts;
        }

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