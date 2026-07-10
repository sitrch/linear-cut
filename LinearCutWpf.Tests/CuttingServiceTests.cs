using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using Xunit;
using LinearCutWpf.Controls;
using LinearCutWpf.Models;
using LinearCutWpf.Services;

namespace LinearCutWpf.Tests
{
    public class CuttingServiceTests
    {
        [Fact]
        public void VerifyOptimization_DetectsMissingParts()
        {
            var service = new CuttingService();

            var dt = new DataTable();
            dt.Columns.Add("Длина", typeof(double));
            dt.Columns.Add("Количество", typeof(int));

            dt.Rows.Add(1000, 2);
            dt.Rows.Add(500, 1);

            var groupData = new CuttingService.GroupData
            {
                GroupKey = "Test",
                ValueColumnName = "Длина",
                QtyColumnName = "Количество",
                Table = dt
            };

            var stocks = new List<StockLengthModel>
            {
                new StockLengthModel { Length = 6000, IsEnabled = true }
            };

            var manualCuts = new List<ManualCutRow>
            {
                new ManualCutRow
                {
                    BarLength = 6000,
                    Size1 = "1000",
                    Size2 = "1000",
                    Count = 1,
                    UseRemainders = true
                }
            };

            var preset = new PresetModel
            {
                Name = "Test",
                TrimStart = 50,
                TrimEnd = 50,
                CutWidth = 4
            };

            var groups = new List<CuttingService.GroupData> { groupData };

            var results = service.OptimizeAllGroups(groups, stocks, manualCuts, preset);

            Assert.Single(results);
            var result = results.First();

            bool isVerified = service.VerifyOptimization(groups, results);
            Assert.True(isVerified, "The optimization results do not match the input group data in count or length.");

            var totalCount = results.Sum(r => r.TotalPartsCount);
            var totalLength = results.Sum(r => r.TotalPartsLength);

            Assert.Equal(3, totalCount);
            Assert.Equal(2500, totalLength);
        }

        [Fact]
        public void ValidateVisualReportData_SubtractsManualCuts()
        {
            var groupedData = new Dictionary<string, DataRow[]>
            {
                { "Test", new[] { CreateRow("1000", "2"), CreateRow("500", "1") } }
            };
            var results = new List<CuttingService.OptimizationResult>
            {
                new CuttingService.OptimizationResult
                {
                    GroupKey = "Test",
                    DetailedBars = new List<CutBarDetailed>
                    {
                        new CutBarDetailed
                        {
                            StockLength = 6000,
                            Parts = new List<PartItem>
                            {
                                new PartItem { Length = 504, Article = "Test" }
                            },
                            ManualParts = new List<double> { 1000, 1000 }
                        }
                    }
                }
            };
            var manualCuts = new Dictionary<string, ObservableCollection<ManualCutRow>>
            {
                {
                    "Test",
                    new ObservableCollection<ManualCutRow>
                    {
                        new ManualCutRow { Size1 = "1000", Count = 2 }
                    }
                }
            };

            var errors = ExportControl.ValidateVisualReportData(
                groupedData, "Длина", null, null, "Количество", results, 4, manualCuts);

            Assert.Empty(errors);
        }

        [Fact]
        public void OptimizeAllGroups_SubtractsManualParts_FromAuto()
        {
            var service = new CuttingService();

            // 3 identical parts (1000mm), 1 goes to manual, 2 should be subtracted from auto
            var dt = new DataTable();
            dt.Columns.Add("Длина", typeof(double));
            dt.Columns.Add("Количество", typeof(int));
            dt.Rows.Add(1000, 3);

            var groupData = new CuttingService.GroupData
            {
                GroupKey = "Test",
                ValueColumnName = "Длина",
                QtyColumnName = "Количество",
                Table = dt
            };

            var stocks = new List<StockLengthModel>
            {
                new StockLengthModel { Length = 6000, IsEnabled = true }
            };

            var manualCuts = new List<ManualCutRow>
            {
                new ManualCutRow
                {
                    BarLength = 6000,
                    Size1 = "1000",
                    Count = 1,
                    UseRemainders = true
                }
            };

            var preset = new PresetModel { Name = "TestPreset", TrimStart = 10, TrimEnd = 10, CutWidth = 4 };
            var groups = new List<CuttingService.GroupData> { groupData };

            var results = service.OptimizeAllGroups(groups, stocks, manualCuts, preset);

            bool isVerified = service.VerifyOptimization(groups, results);
            Assert.True(isVerified);

            int totalCount = results.Sum(r => r.TotalPartsCount);
            // 1 manual (from manual cut) + 2 auto (placed on remainder) = 3 parts total
            Assert.Equal(3, totalCount);
        }

        [Fact]
        public void OptimizeAllGroups_UseRemaindersTrue_FillsRemainder()
        {
            var service = new CuttingService();

            var dt = new DataTable();
            dt.Columns.Add("Длина", typeof(double));
            dt.Columns.Add("Количество", typeof(int));
            dt.Rows.Add(500, 1);
            dt.Rows.Add(1000, 1);

            var groupData = new CuttingService.GroupData
            {
                GroupKey = "Test",
                ValueColumnName = "Длина",
                QtyColumnName = "Количество",
                Table = dt
            };

            var stocks = new List<StockLengthModel>
            {
                new StockLengthModel { Length = 6000, IsEnabled = true }
            };

            var manualCuts = new List<ManualCutRow>
            {
                new ManualCutRow
                {
                    BarLength = 6000,
                    Size1 = "500",
                    Count = 1,
                    UseRemainders = true
                }
            };

            var preset = new PresetModel { Name = "TestPreset", TrimStart = 10, TrimEnd = 10, CutWidth = 4 };
            var groups = new List<CuttingService.GroupData> { groupData };

            var results = service.OptimizeAllGroups(groups, stocks, manualCuts, preset);

            // 500mm went to manual (matches manual cut), 1000mm went to remainder of the same bar
            // Only manual result (auto part filled the remainder)
            bool isVerified = service.VerifyOptimization(groups, results);
            Assert.True(isVerified);

            var manualResult = results.FirstOrDefault(r => r.IsManualCut);
            Assert.NotNull(manualResult);
            Assert.Equal(2, manualResult.TotalPartsCount);
        }

        [Fact]
        public void OptimizeAllGroups_UseRemaindersFalse_DoesNotFillRemainder()
        {
            var service = new CuttingService();

            var dt = new DataTable();
            dt.Columns.Add("Длина", typeof(double));
            dt.Columns.Add("Количество", typeof(int));
            dt.Rows.Add(500, 1);
            dt.Rows.Add(1000, 1);

            var groupData = new CuttingService.GroupData
            {
                GroupKey = "Test",
                ValueColumnName = "Длина",
                QtyColumnName = "Количество",
                Table = dt
            };

            var stocks = new List<StockLengthModel>
            {
                new StockLengthModel { Length = 6000, IsEnabled = true }
            };

            var manualCuts = new List<ManualCutRow>
            {
                new ManualCutRow
                {
                    BarLength = 6000,
                    Size1 = "500",
                    Count = 1,
                    UseRemainders = false
                }
            };

            var preset = new PresetModel { Name = "TestPreset", TrimStart = 10, TrimEnd = 10, CutWidth = 4 };
            var groups = new List<CuttingService.GroupData> { groupData };

            var results = service.OptimizeAllGroups(groups, stocks, manualCuts, preset);

            // 500mm in manual, 1000mm on separate auto bar (remainder NOT used)
            bool isVerified = service.VerifyOptimization(groups, results);
            Assert.True(isVerified);

            Assert.Equal(2, results.Count);

            var manualResult = results.FirstOrDefault(r => r.IsManualCut);
            Assert.NotNull(manualResult);
            Assert.Equal(1, manualResult.TotalPartsCount);

            var autoResult = results.FirstOrDefault(r => !r.IsManualCut);
            Assert.NotNull(autoResult);
            Assert.Equal(1, autoResult.TotalPartsCount);
        }

        [Fact]
        public void OptimizeAllGroups_Remainder_CalculatedCorrectly()
        {
            var service = new CuttingService();

            var dt = new DataTable();
            dt.Columns.Add("Длина", typeof(double));
            dt.Columns.Add("Количество", typeof(int));
            dt.Rows.Add(1000, 2);
            dt.Rows.Add(500, 1);

            var groupData = new CuttingService.GroupData
            {
                GroupKey = "Test",
                ValueColumnName = "Длина",
                QtyColumnName = "Количество",
                Table = dt
            };

            var stocks = new List<StockLengthModel>
            {
                new StockLengthModel { Length = 6000, IsEnabled = true }
            };

            var manualCuts = new List<ManualCutRow>
            {
                new ManualCutRow
                {
                    BarLength = 6000,
                    Size1 = "1000",
                    Size2 = "1000",
                    Count = 1,
                    UseRemainders = true
                }
            };

            var preset = new PresetModel { Name = "TestPreset", TrimStart = 10, TrimEnd = 10, CutWidth = 4 };
            var groups = new List<CuttingService.GroupData> { groupData };

            var results = service.OptimizeAllGroups(groups, stocks, manualCuts, preset);

            // Manual: 2×1000 (from manual) + 1×500 (from auto) on the same bar
            // Capacity = 6000 - ((10-2)+(10-2)) = 6000 - 16 = 5984
            // Used by manual = (1000+4) + (1000+4) = 2008
            // FreeSpace = 5984 - 2008 = 3976
            // Auto part 500mm: lWithCut = 504 → fits
            // Remainder = 3976 - 504 = 3472
            var manualResult = results.FirstOrDefault(r => r.IsManualCut);
            Assert.NotNull(manualResult);
            Assert.Equal(3, manualResult.TotalPartsCount);

            var detailedBar = manualResult.DetailedBars?.FirstOrDefault(b => b.IsFromManualCut);
            if (detailedBar != null)
            {
                Assert.Equal(3472, detailedBar.Remainder, 1);
            }
        }

        [Fact]
        public void VerifyOptimization_DetectsCountMismatch()
        {
            var service = new CuttingService();

            var dt = new DataTable();
            dt.Columns.Add("Длина", typeof(double));
            dt.Columns.Add("Количество", typeof(int));
            dt.Rows.Add(1000, 3);

            var groupData = new CuttingService.GroupData
            {
                GroupKey = "Test",
                ValueColumnName = "Длина",
                QtyColumnName = "Количество",
                Table = dt
            };

            var results = new List<CuttingService.OptimizationResult>
            {
                new CuttingService.OptimizationResult
                {
                    GroupKey = "Test",
                    TotalPartsCount = 2,
                    TotalPartsLength = 2000
                }
            };

            bool isVerified = service.VerifyOptimization(
                new List<CuttingService.GroupData> { groupData }, results);

            Assert.False(isVerified);
        }

        [Fact]
        public void VerifyManualBarsIntegrity_DetectsCountMismatch()
        {
            var service = new CuttingService();

            var dt = new DataTable();
            dt.Columns.Add("Длина", typeof(double));
            dt.Columns.Add("Количество", typeof(int));
            dt.Rows.Add(1000, 10);

            var groupData = new CuttingService.GroupData
            {
                GroupKey = "Test",
                ValueColumnName = "Длина",
                QtyColumnName = "Количество",
                Table = dt
            };

            var manualResultTable = new DataTable();
            manualResultTable.Columns.Add("Кол-во", typeof(int));
            manualResultTable.Columns.Add("Хлыст", typeof(double));
            manualResultTable.Columns.Add("Раскрой", typeof(string));
            manualResultTable.Columns.Add("Остаток", typeof(double));
            manualResultTable.Rows.Add(1, 6000, "1000 + 1000 + 1000 + 1000", 2000.0);

            var autoResult = new CuttingService.OptimizationResult
            {
                GroupKey = "Test",
                IsManualCut = false,
                TotalPartsCount = 5,
                TotalPartsLength = 5000,
                TotalStockLength = 6000,
                TotalRemainderLength = 960,
                DetailedBars = new List<CutBarDetailed>
                {
                    new CutBarDetailed
                    {
                        StockLength = 6000,
                        Parts = Enumerable.Range(0, 5).Select(_ => new PartItem { Length = 1004, Article = "Test" }).ToList(),
                        Remainder = 960,
                        IsFromManualCut = false,
                        ManualParts = new List<double>()
                    }
                }
            };

            var manualResult = new CuttingService.OptimizationResult
            {
                GroupKey = "Test (Ручной раскрой)",
                IsManualCut = true,
                OriginalGroupKey = "Test",
                TotalPartsCount = 4,
                TotalPartsLength = 4000,
                TotalStockLength = 6000,
                TotalRemainderLength = 2000,
                DetailedBars = new List<CutBarDetailed>
                {
                    new CutBarDetailed
                    {
                        StockLength = 6000,
                        Parts = Enumerable.Range(0, 4).Select(_ => new PartItem { Length = 1004, Article = "Test" }).ToList(),
                        Remainder = 2000,
                        IsFromManualCut = true,
                        ManualParts = new List<double> { 1000, 1000, 1000, 1000 }
                    }
                },
                ResultTable = manualResultTable
            };

            var groups = new List<CuttingService.GroupData> { groupData };
            var results = new List<CuttingService.OptimizationResult> { autoResult, manualResult };

            var errors = service.VerifyManualBarsIntegrity(results, groups, 4);

            Assert.Contains(errors, e => e.Contains("10") && e.Contains("9"));
        }

        [Fact]
        public void ValidateManualCuts_DetectsOverflow()
        {
            var service = new CuttingService();

            var manualCuts = new List<ManualCutRow>
            {
                new ManualCutRow
                {
                    BarLength = 600,
                    Size1 = "1000",
                    Count = 1
                }
            };

            var preset = new PresetModel { Name = "Test", TrimStart = 10, TrimEnd = 10, CutWidth = 4 };

            var errors = service.ValidateManualCuts(manualCuts, preset);

            Assert.NotEmpty(errors);
            Assert.Contains(errors, e => e.ColumnName == "Size1");
        }

        [Fact]
        public void ValidateManualCuts_NoErrors_WhenPartsFit()
        {
            var service = new CuttingService();

            var manualCuts = new List<ManualCutRow>
            {
                new ManualCutRow
                {
                    BarLength = 6000,
                    Size1 = "500",
                    Size2 = "500",
                    Size3 = "500",
                    Size4 = "500",
                    Count = 1
                }
            };

            var preset = new PresetModel { Name = "Test", TrimStart = 10, TrimEnd = 10, CutWidth = 4 };

            var errors = service.ValidateManualCuts(manualCuts, preset);

            Assert.Empty(errors);
        }

        private static DataRow CreateRow(string length, string qty)
        {
            var dt = new DataTable();
            dt.Columns.Add("Длина", typeof(double));
            dt.Columns.Add("Количество", typeof(int));
            var row = dt.NewRow();
            row["Длина"] = double.Parse(length);
            row["Количество"] = int.Parse(qty);
            dt.Rows.Add(row);
            return row;
        }
    }
}
