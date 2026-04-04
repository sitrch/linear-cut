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
            Assert.Equal(2, results.Count); // One for auto, one for manual
            var result = results.First();

            // Total parts length should be 1000 * 2 + 500 = 2500
            
            // Assert using the new VerifyOptimization function
            
            // Note: VerifyOptimization will fail for input vs output because input is just GroupData (table with 1000*2 + 500*1 = 2500),
            // but output includes the manual cuts (1000*2 = 2000 from manual cut + whatever is in GroupData table) if they were not removed.
            // Oh wait, manual parts are matched and removed from group data pts list.
            // GroupData: 1000*2 + 500*1 = 2500, 3 parts.
            // ManualCuts: 1000*2 = 2000.
            // ManualParts removed from pts list: 1000*2. Remaining for auto: 500.
            // Auto result: 500. Count: 1.
            // Manual result: 1000, 1000. Count: 2. Length: 2000.
            // Total results output: Count 3, Length 2500. Matches input!

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
