using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Xunit;
using LinearCutWpf.Models;
using LinearCutWpf.Services;

namespace LinearCutWpf.Tests
{
    public class CuttingServiceTests
    {
        [Fact]
        public void VerifyOptimization_DetectsMissingParts()
        {
            // Arrange
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

            // Act
            var results = service.OptimizeAllGroups(groups, stocks, manualCuts, preset);

            // Assert
            // The only auto part (500mm) fits on the remainder of the manual bar (UseRemainders),
            // so autoPartsCount = 0 and the auto result is filtered out.
            // The manual result includes both original manual parts (1000+1000) and the auto-placed part (500).
            Assert.Equal(1, results.Count); // Only manual result (auto part was placed on manual bar remainder)
            var result = results.First();

            bool isVerified = service.VerifyOptimization(groups, results);
            Assert.True(isVerified, "The optimization results do not match the input group data in count or length.");
            
            // Additional direct assertions on the results list
            var totalCount = results.Sum(r => r.TotalPartsCount);
            var totalLength = results.Sum(r => r.TotalPartsLength);
            
            Assert.Equal(3, totalCount);
            Assert.Equal(2500, totalLength);
        }
    }
}
