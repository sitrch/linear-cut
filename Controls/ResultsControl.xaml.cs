using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using LinearCutWpf.Services;

namespace LinearCutWpf.Controls
{
    /// <summary>
    /// Контрол для отображения результатов раскроя.
    /// </summary>
    public partial class ResultsControl : UserControl
    {
        /// <summary>
        /// Модель представления для результатов группы (артикула).
        /// </summary>
        public class ResultViewModel
        {
            /// <summary>
            /// Возвращает или задает ключ группы (артикул).
            /// </summary>
            public string GroupKey { get; set; }

            /// <summary>
            /// Возвращает или задает описание артикула (наименование и цвет).
            /// </summary>
            public string ArticleDescription { get; set; }

            /// <summary>
            /// Флаг, указывающий, что данный результат относится к ручному раскрою.
            /// </summary>
            public bool IsManualCut { get; set; }

            /// <summary>
            /// Возвращает или задает таблицу с результатами раскроя для группы.
            /// </summary>
            public System.Data.DataTable ResultTable { get; set; }
            
            /// <summary>
            /// Возвращает или задает строку статистики по деталям.
            /// </summary>
            public string StatsParts { get; set; }

            /// <summary>
            /// Возвращает или задает строку статистики по использованным хлыстам.
            /// </summary>
            public string StatsStocks { get; set; }

            /// <summary>
            /// Возвращает или задает строку статистики по остаткам.
            /// </summary>
            public string StatsRemainders { get; set; }

            /// <summary>
            /// Возвращает или задает строку статистики КПД (процент использования материала).
            /// </summary>
            public string StatsKpd { get; set; }
        }

        /// <summary>
        /// Инициализирует новый экземпляр класса <see cref="ResultsControl"/>.
        /// </summary>
        public ResultsControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Отображает результаты оптимизации раскроя.
        /// </summary>
        /// <param name="results">Список результатов оптимизации от <see cref="CuttingService"/>.</param>
        public void DisplayResults(List<CuttingService.OptimizationResult> results)
        {
            if (results == null || results.Count == 0)
            {
                txtTotalStats.Text = "Нет данных для отображения.";
                resultsItemsControl.ItemsSource = null;
                return;
            }

            double totalParts = results.Sum(r => r.TotalPartsLength);
            double totalStock = results.Sum(r => r.TotalStockLength);
            double totalKpd = totalStock > 0 ? (totalParts / totalStock * 100.0) : 0;

            txtTotalStats.Text = $"Общая длина деталей: {totalParts / 1000:F2} м | Общая длина хлыстов: {totalStock / 1000:F2} м | Общий %ИспМат: {totalKpd:F2}%";

            var viewModels = results.Select(r => 
            {
                var stocksList = r.UsedStocks.Select(kv => $"{kv.Value} хлыстов по {kv.Key / 1000:F2} м").ToList();
                string stocksText = "Использовано " + string.Join(",\n", stocksList);
                if (r.UsedStocks.Count == 0) stocksText = "Использовано 0 хлыстов";

                string groupKeyDisplay = $"Артикул: {r.GroupKey}";
                if (!string.IsNullOrWhiteSpace(r.ArticleDescription))
                    groupKeyDisplay += $"  |  {r.ArticleDescription}";

                return new ResultViewModel
                {
                    GroupKey = groupKeyDisplay,
                    ArticleDescription = r.ArticleDescription,
                    IsManualCut = r.IsManualCut,
                    ResultTable = r.ResultTable,
                    StatsParts = r.IsManualCut 
                        ? $"Раскроено вручную {r.TotalPartsCount} деталей общей длиной {r.TotalPartsLength / 1000:F2} м"
                        : $"Оптимизировано {r.TotalPartsCount} деталей общей длиной {r.TotalPartsLength / 1000:F2} м",
                    StatsStocks = stocksText,
                    StatsRemainders = $"Общая длина остатков: {r.TotalRemainderLength / 1000:F2} м",
                    StatsKpd = $"%ИспМат: {r.MaterialUtilizationRate:F2}%"
                };
            }).ToList();

            resultsItemsControl.ItemsSource = viewModels;
        }
    }
}
