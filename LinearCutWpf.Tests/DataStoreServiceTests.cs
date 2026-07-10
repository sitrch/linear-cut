using System;
using System.Data;
using System.Linq;
using Xunit;
using LinearCutWpf.Services;

namespace LinearCutWpf.Tests
{
    public class DataStoreServiceTests : IDisposable
    {
        public DataStoreServiceTests()
        {
            DataStoreService.Instance.Clear();
        }

        public void Dispose()
        {
            DataStoreService.Instance.Clear();
        }

        private static DataTable CreateColumnConfig(params (string name, bool isKey, bool isName, bool isVal, bool isQty, bool isLeftAngle, bool isRightAngle, bool isColor)[] cols)
        {
            var dt = new DataTable();
            dt.Columns.Add("ColName", typeof(string));
            dt.Columns.Add("IsKey", typeof(bool));
            dt.Columns.Add("IsName", typeof(bool));
            dt.Columns.Add("IsVal", typeof(bool));
            dt.Columns.Add("IsQty", typeof(bool));
            dt.Columns.Add("IsLeftAngle", typeof(bool));
            dt.Columns.Add("IsRightAngle", typeof(bool));
            dt.Columns.Add("IsColor", typeof(bool));
            foreach (var (name, isKey, isName, isVal, isQty, isLeftAngle, isRightAngle, isColor) in cols)
                dt.Rows.Add(name, isKey, isName, isVal, isQty, isLeftAngle, isRightAngle, isColor);
            return dt;
        }

        [Fact]
        public void BuildGroupedAndCleanDataTable_FiltersZeroLengthRows()
        {
            var rawData = new DataTable();
            rawData.Columns.Add("Артикул", typeof(string));
            rawData.Columns.Add("Длина", typeof(string));
            rawData.Columns.Add("Количество", typeof(string));

            rawData.Rows.Add("A", "1000", "1");
            rawData.Rows.Add("A", "0", "1");
            rawData.Rows.Add("A", "-5", "1");
            rawData.Rows.Add("A", "2000", "2");

            var columnConfig = CreateColumnConfig(
                ("Артикул", true, false, false, false, false, false, false),
                ("Длина", false, false, true, false, false, false, false),
                ("Количество", false, false, false, true, false, false, false)
            );

            DataStoreService.Instance.Initialize(rawData, columnConfig);
            var grouped = DataStoreService.Instance.GroupedAndCleanDataTable;

            Assert.NotNull(grouped);
            Assert.All(grouped.AsEnumerable(), row =>
            {
                double len = Convert.ToDouble(row["Длина"]);
                Assert.True(len > 0);
            });
        }

        [Fact]
        public void BuildGroupedAndCleanDataTable_GroupsByIdenticalRows()
        {
            var rawData = new DataTable();
            rawData.Columns.Add("Артикул", typeof(string));
            rawData.Columns.Add("Длина", typeof(string));
            rawData.Columns.Add("Количество", typeof(string));

            rawData.Rows.Add("A", "1000", "1");
            rawData.Rows.Add("A", "1000", "2");

            var columnConfig = CreateColumnConfig(
                ("Артикул", true, false, false, false, false, false, false),
                ("Длина", false, false, true, false, false, false, false),
                ("Количество", false, false, false, true, false, false, false)
            );

            DataStoreService.Instance.Initialize(rawData, columnConfig);
            var grouped = DataStoreService.Instance.GroupedAndCleanDataTable;

            Assert.NotNull(grouped);
            Assert.Single(grouped.Rows);

            int totalQty = grouped.AsEnumerable().Sum(r => Convert.ToInt32(r["Количество"]));
            Assert.Equal(3, totalQty);
        }

        [Fact]
        public void BuildGroupedAndCleanDataTable_SeparatesByAnglesAndColors()
        {
            var rawData = new DataTable();
            rawData.Columns.Add("Артикул", typeof(string));
            rawData.Columns.Add("Длина", typeof(string));
            rawData.Columns.Add("Количество", typeof(string));
            rawData.Columns.Add("ЛевУгол", typeof(string));
            rawData.Columns.Add("ПравУгол", typeof(string));
            rawData.Columns.Add("Цвет", typeof(string));

            rawData.Rows.Add("A", "1000", "1", "45", "90", "RAL9016");
            rawData.Rows.Add("A", "1000", "2", "45", "90", "RAL9016");
            rawData.Rows.Add("A", "1000", "1", "45", "90", "RAL9005");

            var columnConfig = CreateColumnConfig(
                ("Артикул", true, false, false, false, false, false, false),
                ("Длина", false, false, true, false, false, false, false),
                ("Количество", false, false, false, true, false, false, false),
                ("ЛевУгол", false, false, false, false, true, false, false),
                ("ПравУгол", false, false, false, false, false, true, false),
                ("Цвет", false, false, false, false, false, false, true)
            );

            DataStoreService.Instance.Initialize(rawData, columnConfig);
            var grouped = DataStoreService.Instance.GroupedAndCleanDataTable;

            Assert.NotNull(grouped);
            Assert.Equal(2, grouped.Rows.Count);
        }

        [Fact]
        public void GetArticleView_UsesGroupedAndCleanDataTable()
        {
            var rawData = new DataTable();
            rawData.Columns.Add("Артикул", typeof(string));
            rawData.Columns.Add("Длина", typeof(string));
            rawData.Columns.Add("Количество", typeof(string));

            rawData.Rows.Add("A", "1000", "1");

            var columnConfig = CreateColumnConfig(
                ("Артикул", true, false, false, false, false, false, false),
                ("Длина", false, false, true, false, false, false, false),
                ("Количество", false, false, false, true, false, false, false)
            );

            DataStoreService.Instance.Initialize(rawData, columnConfig);
            Assert.NotNull(DataStoreService.Instance.GroupedAndCleanDataTable);

            var view = DataStoreService.Instance.GetArticleView("A");

            Assert.NotNull(view);
            Assert.Same(DataStoreService.Instance.GroupedAndCleanDataTable, view.Table);
        }

        [Fact]
        public void GetArticleView_ReturnsCorrectArticleRows()
        {
            var rawData = new DataTable();
            rawData.Columns.Add("Артикул", typeof(string));
            rawData.Columns.Add("Длина", typeof(string));
            rawData.Columns.Add("Количество", typeof(string));

            rawData.Rows.Add("A", "1000", "1");
            rawData.Rows.Add("A", "2000", "2");
            rawData.Rows.Add("B", "1500", "3");

            var columnConfig = CreateColumnConfig(
                ("Артикул", true, false, false, false, false, false, false),
                ("Длина", false, false, true, false, false, false, false),
                ("Количество", false, false, false, true, false, false, false)
            );

            DataStoreService.Instance.Initialize(rawData, columnConfig);

            var viewA = DataStoreService.Instance.GetArticleView("A");
            Assert.NotNull(viewA);
            Assert.Equal(2, viewA.Count);

            var viewB = DataStoreService.Instance.GetArticleView("B");
            Assert.NotNull(viewB);
            Assert.Single(viewB);
        }

        [Fact]
        public void GetUniqueArticles_ReturnsDistinctFromGrouped()
        {
            var rawData = new DataTable();
            rawData.Columns.Add("Артикул", typeof(string));
            rawData.Columns.Add("Длина", typeof(string));
            rawData.Columns.Add("Количество", typeof(string));

            rawData.Rows.Add("A", "1000", "1");
            rawData.Rows.Add("A", "2000", "2");
            rawData.Rows.Add("B", "1500", "3");

            var columnConfig = CreateColumnConfig(
                ("Артикул", true, false, false, false, false, false, false),
                ("Длина", false, false, true, false, false, false, false),
                ("Количество", false, false, false, true, false, false, false)
            );

            DataStoreService.Instance.Initialize(rawData, columnConfig);

            var articles = DataStoreService.Instance.GetUniqueArticles();

            Assert.Equal(2, articles.Count);
            Assert.Contains("A", articles);
            Assert.Contains("B", articles);
        }
    }
}
