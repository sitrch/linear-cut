using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using LinearCutWpf.Services;
using LinearCutWpf.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace LinearCutWpf.Controls
{
    /// <summary>
    /// Контрол для экспорта результатов раскроя в форматы Excel и PDF.
    /// Поддерживает различные типы отчетов: подробный, об использовании материалов и визуальный (в PDF).
    /// </summary>
    public partial class ExportControl : UserControl
    {
        /// <summary>
        /// Модель данных для строки в таблице экспорта.
        /// </summary>
        public class ExportRowModel : INotifyPropertyChanged
        {
            private bool _isSelected = true;

            public bool IsSelected
            {
                get => _isSelected;
                set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
            }

            public string Article { get; set; }
            public string Name { get; set; }
            /// <summary>Цвет артикула из исходных данных.</summary>
            public string Color { get; set; }
            public double StockLength { get; set; }
            public int StockCount { get; set; }
            /// <summary>Флаг, указывающий, что данный результат относится к ручному раскрою.</summary>
            public bool IsManualCut { get; set; }
            
            // Ссылка на результаты для выгрузки
            public CuttingService.OptimizationResult ResultData { get; set; }

            public event PropertyChangedEventHandler PropertyChanged;
            protected virtual void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private List<ExportRowModel> _exportData = new List<ExportRowModel>();

        /// <summary>
        /// Инициализирует новый экземпляр класса <see cref="ExportControl"/>.
        /// </summary>
        public ExportControl()
        {
            InitializeComponent();
            txtVisualHeightCoef.Text = CutSettingsProvider.LoadVisualPdfHeightCoefficient().ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private void txtVisualHeightCoef_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (double.TryParse(txtVisualHeightCoef.Text.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double coef))
            {
                CutSettingsProvider.SaveVisualPdfHeightCoefficient(coef);
            }
        }

        private System.Data.DataTable _originalData;
        private List<string> _keyColumnNames;
        private string _nameColumnName;
        private string _valColumnName;
        private string _qtyColumnName;

        private string _leftAngleColumnName;
        private string _rightAngleColumnName;
        private string _colorColumnName;
        private string _objectName;

        private Dictionary<string, System.Data.DataRow[]> _groupedData;
        private Dictionary<string, System.Collections.ObjectModel.ObservableCollection<ManualCutRow>> _manualCutsByArticle;
        private double _cutWidth = 4;

        /// <summary>
        /// Загружает данные результатов раскроя для экспорта.
        /// </summary>
        /// <param name="results">Список результатов оптимизации раскроя.</param>
        /// <param name="originalData">Исходная таблица данных с деталями.</param>
        /// <param name="keyColumnNames">Имена колонок, образующих ключ артикула.</param>
        /// <param name="nameColumnName">Имя колонки с наименованием артикула.</param>
        /// <param name="valColumnName">Имя колонки с длиной детали.</param>
        /// <param name="qtyColumnName">Имя колонки с количеством деталей.</param>
        /// <param name="objectName">Название объекта (опционально).</param>
        /// <param name="leftAngleColumnName">Имя колонки с левым углом реза (опционально).</param>
        /// <param name="rightAngleColumnName">Имя колонки с правым углом реза (опционально).</param>
        /// <param name="colorColumnName">Имя колонки с цветом артикула (опционально).</param>
        /// <param name="groupedData">Сгруппированные строки данных (из вкладки Группировка) для валидации (опционально).</param>
        /// <param name="manualCutsByArticle">Ручной раскрой по артикулам для учёта вычтенных деталей в валидации (опционально).</param>
        public void LoadData(List<CuttingService.OptimizationResult> results, System.Data.DataTable originalData, List<string> keyColumnNames, string nameColumnName, string valColumnName, string qtyColumnName, string objectName = null, string leftAngleColumnName = null, string rightAngleColumnName = null, string colorColumnName = null, Dictionary<string, System.Data.DataRow[]> groupedData = null, double cutWidth = 4, Dictionary<string, System.Collections.ObjectModel.ObservableCollection<ManualCutRow>> manualCutsByArticle = null)
        {
            _originalData = originalData;
            _keyColumnNames = keyColumnNames;
            _nameColumnName = nameColumnName;
            _valColumnName = valColumnName;
            _qtyColumnName = qtyColumnName;
            _leftAngleColumnName = leftAngleColumnName;
            _rightAngleColumnName = rightAngleColumnName;
            _colorColumnName = colorColumnName;
            _objectName = objectName;
            _groupedData = groupedData;
            _manualCutsByArticle = manualCutsByArticle;
            _cutWidth = cutWidth;
            _exportData.Clear();

            if (results == null || results.Count == 0)
            {
                dgExportData.ItemsSource = null;
                return;
            }

            // Находим CheckBox в заголовке
            if (dgExportData.Columns.Count > 0 && dgExportData.Columns[0] is DataGridCheckBoxColumn checkBoxColumn)
            {
                if (checkBoxColumn.Header is StackPanel stackPanel)
                {
                    foreach (var child in stackPanel.Children)
                    {
                        if (child is CheckBox checkBox)
                        {
                            checkBox.IsChecked = true;
                            break;
                        }
                    }
                }
            }

            foreach (var result in results)
            {
                // Ищем наименование в исходной таблице
                // Для ручного раскроя используем OriginalGroupKey, т.к. GroupKey содержит суффикс "(Ручной раскрой)"
                string searchKey = result.IsManualCut ? result.OriginalGroupKey : result.GroupKey;
                string articleName = "";
                string articleColor = "";
                if (originalData != null && _keyColumnNames != null && _keyColumnNames.Any())
                {
                    // Ищем первую строку с таким артикулом
                    foreach (System.Data.DataRow row in originalData.Rows)
                    {
                        var rowKey = DataHelper.GetArticleName(_keyColumnNames.Select(k => row[k]?.ToString()));
                        if (rowKey == searchKey)
                        {
                            if (!string.IsNullOrEmpty(nameColumnName))
                                articleName = row[nameColumnName]?.ToString() ?? "";
                            if (!string.IsNullOrEmpty(colorColumnName) && row.Table.Columns.Contains(colorColumnName))
                            {
                                var colorVal = row[colorColumnName];
                                if (colorVal != DBNull.Value && !string.IsNullOrWhiteSpace(colorVal?.ToString()))
                                    articleColor = colorVal.ToString();
                            }
                            break;
                        }
                    }
                }

                // Определяем основной хлыст (возьмем тот, которого больше всего использовано)
                double mainStockLength = 0;
                int totalStockCount = 0;
                if (result.UsedStocks != null && result.UsedStocks.Count > 0)
                {
                    mainStockLength = result.UsedStocks.OrderByDescending(kv => kv.Value).First().Key;
                    totalStockCount = result.UsedStocks.Values.Sum();
                }

                _exportData.Add(new ExportRowModel
                {
                    IsSelected = true,
                    Article = result.GroupKey,
                    Name = articleName,
                    Color = articleColor,
                    IsManualCut = result.IsManualCut,
                    StockLength = mainStockLength,
                    StockCount = totalStockCount,
                    ResultData = result
                });
            }

            dgExportData.ItemsSource = null;
            dgExportData.ItemsSource = _exportData;

            // Валидация данных раскроя в фоне — не блочим переключение вкладок
            if (_groupedData != null && !string.IsNullOrEmpty(_valColumnName))
            {
                var capturedGrouped = _groupedData;
                var capturedVal = _valColumnName;
                var capturedLeft = _leftAngleColumnName;
                var capturedRight = _rightAngleColumnName;
                var capturedQty = _qtyColumnName;
                var capturedResults = results;
                var capturedManualCuts = _manualCutsByArticle;
                Task.Run(() =>
                {
                    var errors = ValidateVisualReportData(
                        capturedGrouped,
                        capturedVal,
                        capturedLeft,
                        capturedRight,
                        capturedQty,
                        capturedResults,
                        _cutWidth,
                        capturedManualCuts);
                    if (errors.Count > 0)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(
                                $"Обнаружены ошибки в данных раскроя:\r\n\r\n{string.Join("\r\n", errors)}",
                                "Ошибка валидации раскроя",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        });
                    }
                });
            }
        }

        /// <summary>
        /// Обработчик нажатия кнопки "Материалы".
        /// Сохраняет отчёт об использовании материалов в формате Excel (XLSX).
        /// </summary>
        private void BtnExportMaterials_Click(object sender, RoutedEventArgs e)
        {
            var selectedRows = _exportData.Where(x => x.IsSelected).ToList();
            if (selectedRows.Count == 0)
            {
                MessageBox.Show("Выберите хотя бы один артикул для сохранения.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int reportTypeIndex = 1; // Отчёт об использовании материалов
            string reportName = "Материалы";

            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel Files|*.xlsx",
                Title = "Сохранить отчёт об использовании материалов",
                FileName = string.IsNullOrWhiteSpace(_objectName) ? $"{reportName}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx" : $"{reportName}_{_objectName}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    using (var workbook = new ClosedXML.Excel.XLWorkbook())
                    {
                        var summarySheet = workbook.Worksheets.Add("Сводная таблица");
                        ClosedXML.Excel.IXLWorksheet detailSheet = null;
                        if (reportTypeIndex != 1)
                        {
                            detailSheet = workbook.Worksheets.Add("Детальный раскрой");
                        }

                        int summaryRow = 1;

                        if (!string.IsNullOrEmpty(_objectName))
                        {
                            summarySheet.Cell(summaryRow, 1).Value = $"Объект: {_objectName}";
                            summarySheet.Range(summaryRow, 1, summaryRow, 7).Merge().Style.Font.Bold = true;
                            summaryRow++;
                        }
                        summarySheet.Cell(summaryRow, 1).Value = $"Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm}";
                        summarySheet.Range(summaryRow, 1, summaryRow, 7).Merge().Style.Font.Bold = true;
                        summaryRow++;
                        summaryRow++; // Пустая строка

                        // Общая сводная таблица
                        summarySheet.Cell(summaryRow, 1).Value = "Сводная таблица раскроя";
                        summarySheet.Range(summaryRow, 1, summaryRow, 7).Merge().Style.Font.Bold = true;
                        summaryRow++;

                        summarySheet.Cell(summaryRow, 1).Value = "Артикул";
                        summarySheet.Cell(summaryRow, 2).Value = "Наименование";
                        summarySheet.Cell(summaryRow, 3).Value = "Цвет";
                        summarySheet.Cell(summaryRow, 4).Value = "Хлыст";
                        summarySheet.Cell(summaryRow, 5).Value = "Кол-во";
                        summarySheet.Cell(summaryRow, 6).Value = "Длина (м)";
                        summarySheet.Cell(summaryRow, 7).Value = "%ИспМат";
                        var summaryHeaderRange = summarySheet.Range(summaryRow, 1, summaryRow, 7);
                        summaryHeaderRange.Style.Font.Bold = true;
                        summaryHeaderRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;
                        summaryRow++;

                        foreach (var row in selectedRows)
                        {
                            var result = row.ResultData;
                            if (result?.UsedStocks != null)
                            {
                                foreach (var stock in result.UsedStocks)
                                {
                                    summarySheet.Cell(summaryRow, 1).Value = row.Article;
                                    summarySheet.Cell(summaryRow, 2).Value = row.Name;
                                    summarySheet.Cell(summaryRow, 3).Value = row.Color;
                                    summarySheet.Cell(summaryRow, 4).Value = stock.Key;
                                    summarySheet.Cell(summaryRow, 5).Value = stock.Value;
                                    summarySheet.Cell(summaryRow, 6).Value = Math.Round(stock.Key * stock.Value / 1000.0, 2);
                                    summarySheet.Cell(summaryRow, 7).Value = Math.Round(result.MaterialUtilizationRate, 2);
                                    summaryRow++;
                                }
                            }
                        }

                        summaryRow++; // Пустая строка перед статистикой
                        
                        int totalPartsCount = 0;
                        double totalPartsLength = 0;
                        double totalStockLength = 0;
                        double totalRemainderLength = 0;

                        foreach (var row in selectedRows)
                        {
                            if (row.ResultData != null)
                            {
                                totalPartsCount += row.ResultData.TotalPartsCount;
                                totalPartsLength += row.ResultData.TotalPartsLength;
                                totalStockLength += row.ResultData.TotalStockLength;
                                totalRemainderLength += row.ResultData.TotalRemainderLength;
                            }
                        }

                        double overallKpd = totalStockLength > 0 ? (totalPartsLength / totalStockLength) * 100 : 0;

                        int statStartRow = summaryRow;
                        summarySheet.Cell(summaryRow, 1).Value = "Общая статистика:";
                        summarySheet.Range(summaryRow, 1, summaryRow, 4).Merge().Style.Font.Bold = true;
                        summaryRow++;

                        summarySheet.Cell(summaryRow, 1).Value = "Всего деталей:";
                        summarySheet.Cell(summaryRow, 2).Value = totalPartsCount;
                        summaryRow++;

                        summarySheet.Cell(summaryRow, 1).Value = "Общая длина деталей:";
                        summarySheet.Cell(summaryRow, 2).Value = (totalPartsLength / 1000).ToString("F2") + " м";
                        summaryRow++;

                        summarySheet.Cell(summaryRow, 1).Value = "Общая длина хлыстов:";
                        summarySheet.Cell(summaryRow, 2).Value = (totalStockLength / 1000).ToString("F2") + " м";
                        summaryRow++;

                        summarySheet.Cell(summaryRow, 1).Value = "Общая длина остатков:";
                        summarySheet.Cell(summaryRow, 2).Value = (totalRemainderLength / 1000).ToString("F2") + " м";
                        summaryRow++;

                        summarySheet.Cell(summaryRow, 1).Value = "%ИспМат:";
                        summarySheet.Cell(summaryRow, 2).Value = Math.Round(overallKpd, 2).ToString("F2") + "%";
                        summaryRow++;

                        summarySheet.Range(statStartRow, 1, summaryRow - 1, 7).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#F8F8F8");

                        if (detailSheet != null)
                        {
                            int detailRow = 1;

                            if (!string.IsNullOrEmpty(_objectName))
                        {
                            detailSheet.Cell(detailRow, 1).Value = $"Объект: {_objectName}";
                            detailSheet.Range(detailRow, 1, detailRow, 4).Merge().Style.Font.Bold = true;
                            detailRow++;
                        }
                        detailSheet.Cell(detailRow, 1).Value = $"Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm}";
                        detailSheet.Range(detailRow, 1, detailRow, 4).Merge().Style.Font.Bold = true;
                        detailRow++;
                        detailRow++; // Пустая строка

                        foreach (var row in selectedRows)
                        {
                            var result = row.ResultData;
                            if (result?.ResultTable == null) continue;

                            if (detailRow > 1) 
                            {
                                // Вставляем пустую строку перед артикулом (как просил пользователь)
                                detailRow++;
                            }

                            // Заголовок артикула
                            var articleHeaderText = $"Артикул: {row.Article} | {row.Name}";
                            if (!string.IsNullOrEmpty(row.Color)) articleHeaderText += $" | {row.Color}";
                            detailSheet.Cell(detailRow, 1).Value = articleHeaderText;
                            detailSheet.Range(detailRow, 1, detailRow, 4).Merge().Style.Font.Bold = true;
                            detailSheet.Range(detailRow, 1, detailRow, 4).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.AliceBlue;
                            detailRow++;

                            // Заголовки детального раскроя "Хлыст, Кол-во, Раскрой, Остаток"
                            detailSheet.Cell(detailRow, 1).Value = "Хлыст";
                            detailSheet.Cell(detailRow, 2).Value = "Кол-во";
                            detailSheet.Cell(detailRow, 3).Value = "Раскрой";
                            detailSheet.Cell(detailRow, 4).Value = "Остаток";
                            var detailHeaderRange = detailSheet.Range(detailRow, 1, detailRow, 4);
                            detailHeaderRange.Style.Font.Bold = true;
                            detailHeaderRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;
                            detailRow++;

                            foreach (System.Data.DataRow dtRow in result.ResultTable.Rows)
                            {
                                detailSheet.Cell(detailRow, 1).Value = Convert.ToDouble(dtRow["Хлыст"]);
                                detailSheet.Cell(detailRow, 2).Value = Convert.ToInt32(dtRow["Кол-во"]);
                                detailSheet.Cell(detailRow, 3).Value = dtRow["Раскрой"].ToString();
                                detailSheet.Cell(detailRow, 4).Value = Convert.ToDouble(dtRow["Остаток"]);
                                detailRow++;
                            }

                            // Блок подробной статистики
                            int statsStartRow = detailRow;
                            detailSheet.Cell(detailRow, 1).Value = "Статистика:";
                            detailSheet.Range(detailRow, 1, detailRow, 4).Merge().Style.Font.Bold = true;
                            detailRow++;

                            detailSheet.Cell(detailRow, 1).Value = "Всего деталей:";
                            detailSheet.Cell(detailRow, 2).Value = result.TotalPartsCount;
                            detailRow++;

                            detailSheet.Cell(detailRow, 1).Value = "Общая длина деталей:";
                            detailSheet.Cell(detailRow, 2).Value = (result.TotalPartsLength / 1000).ToString("F2") + " м";
                            detailRow++;

                            detailSheet.Cell(detailRow, 1).Value = "Общая длина хлыстов:";
                            detailSheet.Cell(detailRow, 2).Value = (result.TotalStockLength / 1000).ToString("F2") + " м";
                            detailRow++;

                            detailSheet.Cell(detailRow, 1).Value = "Общая длина остатков:";
                            detailSheet.Cell(detailRow, 2).Value = (result.TotalRemainderLength / 1000).ToString("F2") + " м";
                            detailRow++;

                            detailSheet.Cell(detailRow, 1).Value = "%ИспМат:";
                            detailSheet.Cell(detailRow, 2).Value = Math.Round(result.MaterialUtilizationRate, 2).ToString("F2") + "%";
                            detailRow++;

                            detailSheet.Range(statsStartRow, 1, detailRow - 1, 4).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#F8F8F8");
                        }
                        } // конец if (detailSheet != null)

                        summarySheet.Columns().AdjustToContents();
                        if (detailSheet != null)
                        {
                            detailSheet.Column(2).Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Center;
                            detailSheet.Columns().AdjustToContents();
                        }
                        workbook.SaveAs(sfd.FileName);
                    }

                    ShowToast("Файл успешно сохранен!");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при сохранении файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Обработчик нажатия кнопки "Раскрой".
        /// Сохраняет раскрой в формате PDF.
        /// </summary>
        private void BtnExportRaskroj_Click(object sender, RoutedEventArgs e)
        {
            var selectedRows = _exportData.Where(x => x.IsSelected).ToList();
            if (selectedRows.Count == 0)
            {
                MessageBox.Show("Выберите хотя бы один артикул для сохранения.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool isDetailedPdf = false;
            bool isMaterialPdf = false;
            bool isVisualPdf = true;

            string reportName = "Раскрой";

            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PDF Files|*.pdf",
                Title = "Сохранить раскрой",
                FileName = string.IsNullOrWhiteSpace(_objectName) ? $"{reportName}_{DateTime.Now:yyyyMMdd_HHmm}.pdf" : $"{reportName}_{_objectName}_{DateTime.Now:yyyyMMdd_HHmm}.pdf"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    QuestPDF.Settings.License = LicenseType.Community;

                    if (isVisualPdf)
                    {
                        ExportVisualPdf(sfd.FileName, selectedRows, reportName);
                    }
                    else
                    {
                        var document = Document.Create(container =>
                        {
                            container.Page(page =>
                            {
                                page.Size(PageSizes.A4);
                                page.Margin(1, Unit.Centimetre);
                                page.PageColor(Colors.White);
                                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("GOST Type B").Fallback(f => f.FontFamily("GOST Type A")).Fallback(f => f.FontFamily("Arial")));

                                page.Header().Element(c => ComposeHeader(c, reportName));
                                page.Content().Element(c => ComposeContent(c, selectedRows, isDetailedPdf, isMaterialPdf));
                                page.Footer().Element(c => ComposeFooter(c));
                            });
                        });

                        document.GeneratePdf(sfd.FileName);
                    }

                    ShowToast("Файл успешно сохранен!");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при сохранении файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private System.Windows.Threading.DispatcherTimer _toastTimer;

        /// <summary>
        /// Показывает исчезающий баллун-уведомление об успешном действии (аналог индикатора при загрузке Excel).
        /// </summary>
        private void ShowToast(string message)
        {
            toastText.Text = message;
            toast.Visibility = Visibility.Visible;

            var fadeIn = new DoubleAnimation(1, TimeSpan.FromMilliseconds(200));
            var sbIn = new Storyboard();
            sbIn.Children.Add(fadeIn);
            Storyboard.SetTarget(fadeIn, toast);
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath(Border.OpacityProperty));
            sbIn.Begin();

            if (_toastTimer == null)
            {
                _toastTimer = new System.Windows.Threading.DispatcherTimer();
                _toastTimer.Tick += (s, e) =>
                {
                    _toastTimer.Stop();

                    var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(400));
                    var sbOut = new Storyboard();
                    sbOut.Children.Add(fadeOut);
                    Storyboard.SetTarget(fadeOut, toast);
                    Storyboard.SetTargetProperty(fadeOut, new PropertyPath(Border.OpacityProperty));
                    sbOut.Completed += (_, __) => toast.Visibility = Visibility.Collapsed;
                    sbOut.Begin();
                };
            }

            _toastTimer.Interval = TimeSpan.FromSeconds(2);
            _toastTimer.Start();
        }

        private void ComposeHeader(QuestPDF.Infrastructure.IContainer container, string reportName)
        {
            container.Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text(reportName).FontSize(20).SemiBold();
                    if (!string.IsNullOrEmpty(_objectName))
                    {
                        column.Item().Text($"Объект: {_objectName}").FontSize(14).SemiBold();
                    }
                    column.Item().Text($"Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm}");
                });
            });
        }

        private void ComposeContent(QuestPDF.Infrastructure.IContainer container, List<ExportRowModel> selectedRows, bool isDetailedPdf, bool isMaterialPdf)
        {
            container.PaddingVertical(1, Unit.Centimetre).Column(column =>
            {
                if (isMaterialPdf || isDetailedPdf)
                {
                    ComposeMaterialUsageReport(column, selectedRows);
                }
                
                if (isDetailedPdf)
                {
                    column.Item().PaddingVertical(5); // Небольшой отступ

                    foreach (var row in selectedRows)
                    {
                        column.Item().PaddingBottom(15).Element(c => ComposeArticleTable(c, row));
                    }
                }
            });
        }

        /// <summary>
        /// Формирует блок "Отчёт об использовании материалов" (сводная таблица раскроя и общая статистика).
        /// Используется в PDF-отчёте об использовании материалов, а также выводится в начале
        /// визуального раскроя перед детализацией по артикулам.
        /// </summary>
        private void ComposeMaterialUsageReport(QuestPDF.Fluent.ColumnDescriptor column, List<ExportRowModel> selectedRows)
        {
            // Сводная таблица
            column.Item().PaddingBottom(5).Text("Отчёт об использовании материалов").FontSize(14).SemiBold();

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);  // Артикул
                    columns.RelativeColumn(5);  // Наименование (перенос текста)
                    columns.RelativeColumn(1);  // Цвет
                    columns.RelativeColumn(1);  // Хлыст
                    columns.RelativeColumn(1);  // Кол-во
                    columns.RelativeColumn(1);  // Длина (м)
                    columns.RelativeColumn(1);  // %Использования
                });

                table.Header(header =>
                {
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(2).Text("Артикул").SemiBold();
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(2).Text("Наименование").SemiBold();
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(2).Text("Цвет").SemiBold();
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(2).Text("Хлыст").SemiBold();
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(2).Text("Кол-во").SemiBold();
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(2).Text("Длина (м)").SemiBold();
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(2).Text("%Использования").SemiBold();
                });

                foreach (var row in selectedRows)
                {
                    var result = row.ResultData;
                    if (result?.UsedStocks != null)
                    {
                        foreach (var stock in result.UsedStocks)
                        {
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2).Text(row.Article);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2).AlignLeft().Text(row.Name);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2).Text(row.Color);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2).Text(stock.Key.ToString());
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2).Text(stock.Value.ToString());
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2).Text(Math.Round(stock.Key * stock.Value / 1000.0, 2).ToString("F2"));
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2).Text(Math.Round(result.MaterialUtilizationRate, 2).ToString());
                        }
                    }
                }
            });

            int totalPartsCount = 0;
            double totalPartsLength = 0;
            double totalStockLength = 0;
            double totalRemainderLength = 0;

            foreach (var row in selectedRows)
            {
                if (row.ResultData != null)
                {
                    totalPartsCount += row.ResultData.TotalPartsCount;
                    totalPartsLength += row.ResultData.TotalPartsLength;
                    totalStockLength += row.ResultData.TotalStockLength;
                    totalRemainderLength += row.ResultData.TotalRemainderLength;
                }
            }

            double overallKpd = totalStockLength > 0 ? (totalPartsLength / totalStockLength) * 100 : 0;

            column.Item().PaddingTop(10).Background("#F8F8F8").Padding(5).Column(statCol =>
            {
                statCol.Item().Text("Общая статистика:").SemiBold();
                statCol.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

                    table.Cell().Padding(2).Text("Всего деталей:");
                    table.Cell().Padding(2).Text(totalPartsCount.ToString());

                    table.Cell().Padding(2).Text("Общая длина деталей:");
                    table.Cell().Padding(2).Text((totalPartsLength / 1000).ToString("F2") + " м");

                    table.Cell().Padding(2).Text("Общая длина хлыстов:");
                    table.Cell().Padding(2).Text((totalStockLength / 1000).ToString("F2") + " м");

                    table.Cell().Padding(2).Text("Общая длина остатков:");
                    table.Cell().Padding(2).Text((totalRemainderLength / 1000).ToString("F2") + " м");

                    table.Cell().Padding(2).Text("%ИспМат:");
                    table.Cell().Padding(2).Text(Math.Round(overallKpd, 2).ToString("F2") + "%");
                });
            });
        }

        private void ComposePartsListTable(QuestPDF.Infrastructure.IContainer container, ExportRowModel row)
        {
            var result = row.ResultData;
            if (result == null || _originalData == null) return;
            // Для ручного раскроя используем OriginalGroupKey для поиска деталей в исходной таблице
            string searchKey = result.IsManualCut ? result.OriginalGroupKey : result.GroupKey;

            var partsData = new List<dynamic>();
            foreach (System.Data.DataRow dtRow in _originalData.Rows)
            {
                var rowKey = _keyColumnNames != null && _keyColumnNames.Any() 
                    ? DataHelper.GetArticleName(_keyColumnNames.Select(k => dtRow[k]?.ToString()))
                    : "";
                if (rowKey == searchKey)
                {
                    double length = 0;
                    if (dtRow[_valColumnName] != DBNull.Value)
                        length = Convert.ToDouble(dtRow[_valColumnName]);
                    
                    int qty = 0;
                    if (dtRow[_qtyColumnName] != DBNull.Value)
                        qty = Convert.ToInt32(dtRow[_qtyColumnName]);

                    string leftAngle = "90";
                    if (!string.IsNullOrEmpty(_leftAngleColumnName) && dtRow[_leftAngleColumnName] != DBNull.Value)
                        leftAngle = dtRow[_leftAngleColumnName].ToString();

                    string rightAngle = "90";
                    if (!string.IsNullOrEmpty(_rightAngleColumnName) && dtRow[_rightAngleColumnName] != DBNull.Value)
                        rightAngle = dtRow[_rightAngleColumnName].ToString();

                    partsData.Add(new { Length = length, LeftAngle = leftAngle, RightAngle = rightAngle, Qty = qty });
                }
            }

            if (partsData.Count == 0) return;

            var groupedParts = partsData
                .GroupBy(p => new { p.Length, p.LeftAngle, p.RightAngle })
                .Select(g => new { g.Key.Length, g.Key.LeftAngle, g.Key.RightAngle, TotalQty = g.Sum(x => (int)x.Qty) })
                .Where(x => x.TotalQty > 0)
                .OrderByDescending(x => x.Length)
                .ToList();

            container.Column(col =>
            {
                col.Item().PaddingTop(5).PaddingBottom(2).Text("ПЕРЕЧЕНЬ ЗАГОТОВОК").SemiBold();
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(80);
                        columns.ConstantColumn(80);
                        columns.ConstantColumn(80);
                        columns.ConstantColumn(60);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Grey.Lighten2).BorderBottom(1).BorderColor(Colors.Black).Padding(2).Text("Длина").SemiBold();
                        header.Cell().Background(Colors.Grey.Lighten2).BorderBottom(1).BorderColor(Colors.Black).Padding(2).Text("Угол левый").SemiBold();
                        header.Cell().Background(Colors.Grey.Lighten2).BorderBottom(1).BorderColor(Colors.Black).Padding(2).Text("Угол правый").SemiBold();
                        header.Cell().Background(Colors.Grey.Lighten2).BorderBottom(1).BorderColor(Colors.Black).Padding(2).Text("Кол-во").SemiBold();
                    });

                    foreach (var p in groupedParts)
                    {
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2).Text(((double)p.Length).ToString());
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2).Text((string)p.LeftAngle);
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2).Text((string)p.RightAngle);
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2).Text(((int)p.TotalQty).ToString());
                    }
                });
            });
        }

        private void ComposeArticleTable(QuestPDF.Infrastructure.IContainer container, ExportRowModel row)
        {
            var result = row.ResultData;
            if (result?.ResultTable == null) return;

            container.Column(column =>
            {
                // Заголовок артикула
                var pdfArticleHeaderText = $"Артикул: {row.Article} | {row.Name}";
                if (!string.IsNullOrEmpty(row.Color)) pdfArticleHeaderText += $" | {row.Color}";
                column.Item().Background(Colors.Blue.Lighten4).Padding(5).Text(pdfArticleHeaderText).SemiBold();

                // Перечень заготовок
                column.Item().PaddingTop(5).Element(c => ComposePartsListTable(c, row));

                column.Item().PaddingTop(10).PaddingBottom(2).Text("Детальный раскрой").SemiBold();

                // Таблица раскроя
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(1);
                    });

                    // Заголовки
                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Grey.Lighten2).BorderBottom(1).BorderColor(Colors.Black).Padding(2).Text("Хлыст").SemiBold();
                        header.Cell().Background(Colors.Grey.Lighten2).BorderBottom(1).BorderColor(Colors.Black).Padding(2).Text("Кол-во").SemiBold();
                        header.Cell().Background(Colors.Grey.Lighten2).BorderBottom(1).BorderColor(Colors.Black).Padding(2).Text("Раскрой").SemiBold();
                        header.Cell().Background(Colors.Grey.Lighten2).BorderBottom(1).BorderColor(Colors.Black).Padding(2).Text("Остаток").SemiBold();
                    });

                    // Данные
                    foreach (System.Data.DataRow dtRow in result.ResultTable.Rows)
                    {
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(dtRow["Хлыст"].ToString());
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(dtRow["Кол-во"].ToString());
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(dtRow["Раскрой"].ToString());
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(dtRow["Остаток"].ToString());
                    }
                });

                // Статистика
                column.Item().PaddingTop(10).Background("#F8F8F8").Padding(5).Column(statCol =>
                {
                    statCol.Item().Text("Статистика:").SemiBold();
                    statCol.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Cell().Padding(2).Text("Всего деталей:");
                        table.Cell().Padding(2).Text(result.TotalPartsCount.ToString());

                        table.Cell().Padding(2).Text("Общая длина деталей:");
                        table.Cell().Padding(2).Text((result.TotalPartsLength / 1000).ToString("F2") + " м");

                        table.Cell().Padding(2).Text("Общая длина хлыстов:");
                        table.Cell().Padding(2).Text((result.TotalStockLength / 1000).ToString("F2") + " м");

                        table.Cell().Padding(2).Text("Общая длина остатков:");
                        table.Cell().Padding(2).Text((result.TotalRemainderLength / 1000).ToString("F2") + " м");

                        table.Cell().Padding(2).Text("%ИспМат:");
                        table.Cell().Padding(2).Text(Math.Round(result.MaterialUtilizationRate, 2).ToString("F2") + "%");
                    });
                });
            });
        }

        private void ExportVisualPdf(string fileName, List<ExportRowModel> selectedRows, string reportName)
        {
            // Валидация правильности размещения деталей перед генерацией визуального отчёта
            if (_groupedData != null && !string.IsNullOrEmpty(_valColumnName))
            {
                // Валидируем только выбранные для сохранения артикулы, иначе невыбранные
                // артикулы ошибочно дают расхождение "ожидалось N, в отчёте 0".
                var selectedKeys = new HashSet<string>(
                    selectedRows.Select(r => r.ResultData.IsManualCut
                        ? r.ResultData.OriginalGroupKey
                        : r.ResultData.GroupKey));
                var filteredGroupedData = _groupedData
                    .Where(kvp => selectedKeys.Contains(kvp.Key))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                var errors = ValidateVisualReportData(
                    filteredGroupedData,
                    _valColumnName,
                    _leftAngleColumnName,
                    _rightAngleColumnName,
                    _qtyColumnName,
                    selectedRows.Select(r => r.ResultData).ToList(),
                    _cutWidth,
                    _manualCutsByArticle);
                if (errors.Count > 0)
                {
                    var result = MessageBox.Show(
                        $"Обнаружены ошибки в данных визуального раскроя:\r\n\r\n{string.Join("\r\n", errors)}\r\n\r\nПродолжить формирование отчёта?",
                        "Ошибка валидации визуального раскроя",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    if (result == MessageBoxResult.No)
                        return;
                }
            }

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("GOST Type B").Fallback(f => f.FontFamily("GOST Type A")).Fallback(f => f.FontFamily("Arial")));

                    page.Header().Element(c => ComposeHeader(c, reportName));
                    page.Content().Element(c => ComposeVisualContent(c, selectedRows));
                    page.Footer().Element(c => ComposeFooter(c));
                });
            });

            try
            {
                document.GeneratePdf(fileName);
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("Ошибка соответствия данных"))
            {
                if (File.Exists(fileName))
                    File.Delete(fileName);
                MessageBox.Show(
                    $"Не удалось сформировать визуальный отчёт.\r\n\r\n{ex.Message}\r\n\r\n" +
                    $"Проверьте соответствие данных в исходной таблице.",
                    "Ошибка генерации отчёта",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ComposeVisualContent(QuestPDF.Infrastructure.IContainer container, List<ExportRowModel> selectedRows)
        {
            container.PaddingVertical(1, Unit.Centimetre).Column(column =>
            {
                // Отчёт об использовании материалов — в начале документа (первая страница)
                ComposeMaterialUsageReport(column, selectedRows);

                // Данные раскроя начинаются с новой страницы
                column.Item().PageBreak();

                column.Item().PaddingTop(15);

                for (int i = 0; i < selectedRows.Count; i++)
                {
                    column.Item().Element(c => ComposeVisualArticle(c, selectedRows[i]));

                    if (i < selectedRows.Count - 1)
                    {
                        column.Item().Height(20); // Разделитель вместо PaddingBottom на весь Item
                    }
                }
            });
        }

        private void ComposeVisualArticle(QuestPDF.Infrastructure.IContainer container, ExportRowModel row)
        {
            var result = row.ResultData;
            if (result == null || result.DetailedBars == null) return;

            container.Column(column =>
            {
                // Заголовок артикула и детализация с защитой от разрыва страницы (оставляем хотя бы 50 пикселей)
                column.Item().EnsureSpace(50).Column(headCol => 
                {
                    var visualHeaderText = $"Артикул: {row.Article} | {row.Name}";
                    if (!string.IsNullOrEmpty(row.Color)) visualHeaderText += $" | {row.Color}";
                    headCol.Item().Background(Colors.Grey.Lighten3).Padding(5).Text(visualHeaderText).FontSize(12).SemiBold();

                    // Перечень заготовок
                    headCol.Item().PaddingTop(5).Element(c => ComposePartsListTable(c, row));

                    headCol.Item().PaddingVertical(2).Text($"Общее количество деталей: {result.TotalPartsCount} шт").SemiBold();

                    headCol.Item().PaddingVertical(5).Text("Детализация раскроя").SemiBold();
                });

                // Группируем одинаковые хлысты
                var groupedBars = result.DetailedBars.GroupBy(b => new { b.StockLength, PartsStr = string.Join(",", b.Parts.Select(p => p.Length)) }).ToList();

                foreach (var group in groupedBars)
                {
                    var bar = group.First();
                    int count = group.Count();

                    // Защищаем отдельный хлыст (текст + картинка) от разбиения, требуется около 120 пикселей (текст + SVG 80 + отступ 10)
                    column.Item().EnsureSpace(120).PaddingBottom(10).Column(barCol =>
                    {
                        barCol.Item().Text($"{row.Article} Хлыст: {bar.StockLength} мм (Кол-во: {count} хлыстов)");
                        
                        // Отрисовка графической схемы хлыста
                        string svgString = GenerateVisualBarSvg(800, 80, bar, _originalData, _keyColumnNames, _leftAngleColumnName, _rightAngleColumnName, _cutWidth);
                        barCol.Item().Svg(svgString);
                    });
                }

                // Статистика (справа, на светло-сером фоне с закруглёнными краями)
                column.Item().PaddingTop(10).AlignRight().Width(300)
                    .Border(1).BorderColor(Colors.Grey.Lighten2).CornerRadius(5)
                    .Background(Colors.Grey.Lighten3).Padding(8).Column(statsCol =>
                {
                    int partsCount = result.TotalPartsCount;
                    int stockCount = result.UsedStocks.Values.Sum();
                    statsCol.Item().Text("Общая статистика:").SemiBold().Italic();
                    statsCol.Item().Text(text =>
                    {
                        text.Span("Оптимизировано ").Italic();
                        text.Span($"{partsCount}").Bold().Italic();
                        text.Span($" {Plural(partsCount, "деталь", "детали", "деталей")} общей длиной ").Italic();
                        text.Span($"{result.TotalPartsLength / 1000.0:F2} м").Bold().Italic();
                    });
                    statsCol.Item().Text(text =>
                    {
                        text.Span("Использовано ").Italic();
                        text.Span($"{stockCount}").Bold().Italic();
                        text.Span($" {Plural(stockCount, "хлыст", "хлыста", "хлыстов")} общей длиной ").Italic();
                        text.Span($"{result.TotalStockLength / 1000.0:F2} м").Bold().Italic();
                    });
                    statsCol.Item().Text(text =>
                    {
                        text.Span("Процент использования материала: ").Italic();
                        text.Span($"{result.MaterialUtilizationRate:F2}%").Bold().Italic();
                    });
                });
            });
        }

        private static string Plural(int n, string one, string two, string five)
        {
            int abs = Math.Abs(n);
            int mod10 = abs % 10;
            int mod100 = abs % 100;
            if (mod100 >= 11 && mod100 <= 19) return five;
            if (mod10 == 1) return one;
            if (mod10 >= 2 && mod10 <= 4) return two;
            return five;
        }

        private string GenerateVisualBarSvg(float width, float height, CutBarDetailed bar, System.Data.DataTable originalData, List<string> keyColumns, string leftAngleColumn, string rightAngleColumn, double cutWidth)
        {
            var svg = new System.Text.StringBuilder();
            var culture = System.Globalization.CultureInfo.InvariantCulture;

            string article = bar.Parts.FirstOrDefault()?.Article;
            double profileHeight = 25.0;
            if (!string.IsNullOrEmpty(article))
            {
                var heights = ProfileHeightService.LoadProfileHeightsWithMetadata();
                if (heights.TryGetValue(article, out var heightInfo) && heightInfo.height.HasValue)
                {
                    profileHeight = heightInfo.height.Value;
                }
                else
                {
                    var defaultHeight = ProfileHeightService.LoadDefaultHeight();
                    if (defaultHeight.HasValue)
                    {
                        profileHeight = defaultHeight.Value;
                    }
                }
            }

            double coef = CutSettingsProvider.LoadVisualPdfHeightCoefficient();
            float barHeight = 25f;
            if (bar.StockLength > 0)
            {
                barHeight = (float)(width * (profileHeight / bar.StockLength) * coef);
                if (barHeight < 5f) barHeight = 5f;
            }

            float dimensionLineY = barHeight + 20f;
            float totalDimensionLineY = dimensionLineY + 20f;
            float svgHeight = totalDimensionLineY + 15f;

            svg.AppendLine($"<svg viewBox=\"0 0 {width.ToString(culture)} {svgHeight.ToString(culture)}\" xmlns=\"http://www.w3.org/2000/svg\">");
            
            svg.AppendLine($"<rect x=\"0\" y=\"0\" width=\"{width.ToString(culture)}\" height=\"{barHeight.ToString(culture)}\" fill=\"none\" stroke=\"lightgray\" stroke-width=\"1\" />");

            float bgStep = 5f;
            for (float d = 0; d < width + barHeight; d += bgStep)
            {
                float x1 = Math.Max(0, d - barHeight);
                float y1 = barHeight - (d - x1);
                
                float x2 = Math.Min(width, d);
                float y2 = barHeight - (d - x2);
                
                svg.AppendLine($"<line x1=\"{x1.ToString(culture)}\" y1=\"{y1.ToString(culture)}\" x2=\"{x2.ToString(culture)}\" y2=\"{y2.ToString(culture)}\" stroke=\"black\" stroke-width=\"0.3\" />");
            }

            if (bar.StockLength <= 0) 
            {
                svg.AppendLine("</svg>");
                return svg.ToString();
            }

            float scale = width / (float)bar.StockLength;
            float currentX = 0;

            if (bar.Parts == null || bar.Parts.Count == 0)
            {
                svg.AppendLine("</svg>");
                return svg.ToString();
            }

            // Строим индекс строк исходной таблицы для артикула (для поиска углов реза)
            var articleRows = new List<System.Data.DataRow>();
            if (originalData != null && keyColumns != null && keyColumns.Any())
            {
                foreach (System.Data.DataRow row in originalData.Rows)
                {
                    var rowKey = DataHelper.GetArticleName(keyColumns.Select(k => row[k]?.ToString()));
                    if (rowKey == bar.Parts.FirstOrDefault()?.Article)
                        articleRows.Add(row);
                }
            }

            foreach (var part in bar.Parts)
            {
                string lAngle = "90";
                string rAngle = "90";
                double originalLength = part.Length - cutWidth;
                if (originalLength < 0) originalLength = 0;

                // Прямой поиск углов реза по OriginalRowIndex (эвристика не используется)
                if (part.OriginalRowIndex >= 0 && part.OriginalRowIndex < articleRows.Count)
                {
                    var matchedRow = articleRows[part.OriginalRowIndex];
                    // Верификация: проверяем, что строка действительно соответствует артикулу детали
                    var rowArticleKey = DataHelper.GetArticleName(keyColumns.Select(k => matchedRow[k]?.ToString()));
                    if (!string.Equals(rowArticleKey, part.Article, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            $"Ошибка соответствия данных: деталь '{part.Article}' длиной {originalLength} мм " +
                            $"ссылается на строку {part.OriginalRowIndex} с артикулом '{rowArticleKey}'. " +
                            $"Генерация отчёта прервана.");
                    }
                    if (!string.IsNullOrEmpty(leftAngleColumn) && matchedRow[leftAngleColumn] != DBNull.Value)
                        lAngle = matchedRow[leftAngleColumn].ToString();
                    if (!string.IsNullOrEmpty(rightAngleColumn) && matchedRow[rightAngleColumn] != DBNull.Value)
                        rAngle = matchedRow[rightAngleColumn].ToString();
                }

                float actualCutWidth = (float)cutWidth;

                float partWidth = (float)originalLength * scale;
                float sawKerf = actualCutWidth * scale;

                // Для 45 градусов смещение по X равно высоте профиля, для других углов - пропорционально
                // Углы считаются от торца (90 - прямой, 45 - скос)
                float leftOffset = 0;
                float rightOffset = 0;
                
                if (double.TryParse(lAngle.Replace(",", "."), System.Globalization.NumberStyles.Any, culture, out double lVal))
                {
                    if (lVal != 90 && lVal > 0)
                        leftOffset = (float)(barHeight / Math.Tan(lVal * Math.PI / 180.0));
                }
                if (double.TryParse(rAngle.Replace(",", "."), System.Globalization.NumberStyles.Any, culture, out double rVal))
                {
                    if (rVal != 90 && rVal > 0)
                        rightOffset = (float)(barHeight / Math.Tan(rVal * Math.PI / 180.0));
                }

                // Ограничиваем смещения, чтобы деталь не вывернуло наизнанку
                if (leftOffset + rightOffset >= partWidth)
                {
                    leftOffset = partWidth * 0.4f;
                    rightOffset = partWidth * 0.4f;
                }

                // Точки для трапеции: (низ-лево), (низ-право), (верх-право), (верх-лево)
                // Рисуем деталь темным цветом
                string points = $"{(currentX + leftOffset).ToString(culture)},0 " +
                                $"{(currentX + partWidth - rightOffset).ToString(culture)},0 " +
                                $"{(currentX + partWidth).ToString(culture)},{barHeight.ToString(culture)} " +
                                $"{currentX.ToString(culture)},{barHeight.ToString(culture)}";

                svg.AppendLine($"<polygon points=\"{points}\" fill=\"#2A2A35\" stroke=\"white\" stroke-width=\"1\" />");
                
                // Рисуем засечки реза (тонкая линия)
                if (lAngle != "90") {
                    svg.AppendLine($"<line x1=\"{currentX.ToString(culture)}\" y1=\"{barHeight.ToString(culture)}\" x2=\"{(currentX + leftOffset).ToString(culture)}\" y2=\"0\" stroke=\"white\" stroke-width=\"1\" stroke-dasharray=\"2,2\" />");
                }
                if (rAngle != "90") {
                    svg.AppendLine($"<line x1=\"{(currentX + partWidth).ToString(culture)}\" y1=\"{barHeight.ToString(culture)}\" x2=\"{(currentX + partWidth - rightOffset).ToString(culture)}\" y2=\"0\" stroke=\"white\" stroke-width=\"1\" stroke-dasharray=\"2,2\" />");
                }

                // Размерная линия для детали
                float dimStartX = currentX;
                float dimEndX = currentX + partWidth;
                
                // Выносные линии вниз
                svg.AppendLine($"<line x1=\"{dimStartX.ToString(culture)}\" y1=\"{barHeight.ToString(culture)}\" x2=\"{dimStartX.ToString(culture)}\" y2=\"{dimensionLineY.ToString(culture)}\" stroke=\"black\" stroke-width=\"0.2\" />");
                svg.AppendLine($"<line x1=\"{dimEndX.ToString(culture)}\" y1=\"{barHeight.ToString(culture)}\" x2=\"{dimEndX.ToString(culture)}\" y2=\"{dimensionLineY.ToString(culture)}\" stroke=\"black\" stroke-width=\"0.2\" />");
                
                // Горизонтальная размерная линия
                svg.AppendLine($"<line x1=\"{dimStartX.ToString(culture)}\" y1=\"{dimensionLineY.ToString(culture)}\" x2=\"{dimEndX.ToString(culture)}\" y2=\"{dimensionLineY.ToString(culture)}\" stroke=\"black\" stroke-width=\"0.2\" />");
                
                // Стрелки на размерной линии
                float arrowLength = Math.Min(9f, partWidth / 3f);
                float arrowHalfWidth = arrowLength / 6f;
                string arrowLeft = $"{(dimStartX).ToString(culture)},{dimensionLineY.ToString(culture)} " +
                                   $"{(dimStartX + arrowLength).ToString(culture)},{(dimensionLineY - arrowHalfWidth).ToString(culture)} " +
                                   $"{(dimStartX + arrowLength).ToString(culture)},{(dimensionLineY + arrowHalfWidth).ToString(culture)}";
                svg.AppendLine($"<polygon points=\"{arrowLeft}\" fill=\"black\" />");

                string arrowRight = $"{(dimEndX).ToString(culture)},{dimensionLineY.ToString(culture)} " +
                                    $"{(dimEndX - arrowLength).ToString(culture)},{(dimensionLineY - arrowHalfWidth).ToString(culture)} " +
                                    $"{(dimEndX - arrowLength).ToString(culture)},{(dimensionLineY + arrowHalfWidth).ToString(culture)}";
                svg.AppendLine($"<polygon points=\"{arrowRight}\" fill=\"black\" />");

                // Текст длины детали (разрывает линию - для простоты зальем фон белым прямоугольником)
                string text = originalLength.ToString();
                float textApproxWidth = text.Length * 6; // примерная ширина
                
                if (partWidth > textApproxWidth)
                {
                    float textX = currentX + (partWidth) / 2;
                    float textY = dimensionLineY + 3; // по центру линии
                    
                    // Белая подложка под текст
                    svg.AppendLine($"<rect x=\"{(textX - textApproxWidth / 2 - 2).ToString(culture)}\" y=\"{(dimensionLineY - 5).ToString(culture)}\" width=\"{(textApproxWidth + 4).ToString(culture)}\" height=\"10\" fill=\"white\" />");
                    // Сам текст
                    svg.AppendLine($"<text x=\"{textX.ToString(culture)}\" y=\"{textY.ToString(culture)}\" font-family=\"GOST Type B, GOST Type A, ISOCPEUR, GOST Common, Arial\" font-style=\"italic\" font-size=\"10\" fill=\"black\" text-anchor=\"middle\">{text}</text>");
                }
                
                // Рассчитываем позицию для следующей детали с учетом углов запила
                // Деталь занимает пространство от (currentX) до (currentX + partWidth + rightOffset)
                // Следующая деталь должна начинаться с учетом своего левого смещения
                // Поэтому добавляем: ширину детали + пропил + правое смещение текущей детали
                currentX += partWidth + sawKerf + rightOffset;
            }

            // Остаток
            if (bar.Remainder > 0)
            {
                var remWidth = (float)bar.Remainder * scale;
                
                // Рисуем границы остатка
                svg.AppendLine($"<rect x=\"{currentX.ToString(culture)}\" y=\"0\" width=\"{remWidth.ToString(culture)}\" height=\"{barHeight.ToString(culture)}\" fill=\"none\" stroke=\"black\" stroke-width=\"0.5\" />");
                
                // Выносные линии для остатка (к первой размерной линии)
                svg.AppendLine($"<line x1=\"{currentX.ToString(culture)}\" y1=\"{barHeight.ToString(culture)}\" x2=\"{currentX.ToString(culture)}\" y2=\"{dimensionLineY.ToString(culture)}\" stroke=\"black\" stroke-width=\"0.2\" />");
                svg.AppendLine($"<line x1=\"{(currentX + remWidth).ToString(culture)}\" y1=\"{barHeight.ToString(culture)}\" x2=\"{(currentX + remWidth).ToString(culture)}\" y2=\"{dimensionLineY.ToString(culture)}\" stroke=\"black\" stroke-width=\"0.2\" />");
                
                // Горизонтальная линия остатка
                svg.AppendLine($"<line x1=\"{currentX.ToString(culture)}\" y1=\"{dimensionLineY.ToString(culture)}\" x2=\"{(currentX + remWidth).ToString(culture)}\" y2=\"{dimensionLineY.ToString(culture)}\" stroke=\"black\" stroke-width=\"0.2\" />");
                
                // Стрелки
                float arrowLength = Math.Min(9f, remWidth / 3f);
                float arrowHalfWidth = arrowLength / 6f;
                string arrowLeft = $"{(currentX).ToString(culture)},{dimensionLineY.ToString(culture)} " +
                                   $"{(currentX + arrowLength).ToString(culture)},{(dimensionLineY - arrowHalfWidth).ToString(culture)} " +
                                   $"{(currentX + arrowLength).ToString(culture)},{(dimensionLineY + arrowHalfWidth).ToString(culture)}";
                svg.AppendLine($"<polygon points=\"{arrowLeft}\" fill=\"black\" />");

                string arrowRight = $"{(currentX + remWidth).ToString(culture)},{dimensionLineY.ToString(culture)} " +
                                    $"{(currentX + remWidth - arrowLength).ToString(culture)},{(dimensionLineY - arrowHalfWidth).ToString(culture)} " +
                                    $"{(currentX + remWidth - arrowLength).ToString(culture)},{(dimensionLineY + arrowHalfWidth).ToString(culture)}";
                svg.AppendLine($"<polygon points=\"{arrowRight}\" fill=\"black\" />");

                string text = bar.Remainder.ToString();
                float textApproxWidth = text.Length * 6;
                if (remWidth > textApproxWidth)
                {
                    float textX = currentX + (remWidth) / 2;
                    float textY = dimensionLineY + 3;
                    
                    svg.AppendLine($"<rect x=\"{(textX - textApproxWidth / 2 - 2).ToString(culture)}\" y=\"{(dimensionLineY - 5).ToString(culture)}\" width=\"{(textApproxWidth + 4).ToString(culture)}\" height=\"10\" fill=\"white\" />");
                    svg.AppendLine($"<text x=\"{textX.ToString(culture)}\" y=\"{textY.ToString(culture)}\" font-family=\"GOST Type B, GOST Type A, ISOCPEUR, GOST Common, Arial\" font-style=\"italic\" font-size=\"10\" fill=\"black\" text-anchor=\"middle\">{text}</text>");
                }
            }

            // Общая размерная линия для всего хлыста (StockLength)
            svg.AppendLine($"<line x1=\"0\" y1=\"{barHeight.ToString(culture)}\" x2=\"0\" y2=\"{totalDimensionLineY.ToString(culture)}\" stroke=\"black\" stroke-width=\"0.2\" />");
            svg.AppendLine($"<line x1=\"{width.ToString(culture)}\" y1=\"{barHeight.ToString(culture)}\" x2=\"{width.ToString(culture)}\" y2=\"{totalDimensionLineY.ToString(culture)}\" stroke=\"black\" stroke-width=\"0.2\" />");
            
            svg.AppendLine($"<line x1=\"0\" y1=\"{totalDimensionLineY.ToString(culture)}\" x2=\"{width.ToString(culture)}\" y2=\"{totalDimensionLineY.ToString(culture)}\" stroke=\"black\" stroke-width=\"0.2\" />");
            
            float arrowLengthTotal = Math.Min(12f, width / 3f);
            float arrowHalfWidthTotal = arrowLengthTotal / 6f;
            string arrowLeftTotal = $"0,{totalDimensionLineY.ToString(culture)} " +
                                    $"{arrowLengthTotal.ToString(culture)},{(totalDimensionLineY - arrowHalfWidthTotal).ToString(culture)} " +
                                    $"{arrowLengthTotal.ToString(culture)},{(totalDimensionLineY + arrowHalfWidthTotal).ToString(culture)}";
            svg.AppendLine($"<polygon points=\"{arrowLeftTotal}\" fill=\"black\" />");

            string arrowRightTotal = $"{width.ToString(culture)},{totalDimensionLineY.ToString(culture)} " +
                                     $"{(width - arrowLengthTotal).ToString(culture)},{(totalDimensionLineY - arrowHalfWidthTotal).ToString(culture)} " +
                                     $"{(width - arrowLengthTotal).ToString(culture)},{(totalDimensionLineY + arrowHalfWidthTotal).ToString(culture)}";
            svg.AppendLine($"<polygon points=\"{arrowRightTotal}\" fill=\"black\" />");

            string totalText = bar.StockLength.ToString();
            float totalApproxWidth = totalText.Length * 6;
            svg.AppendLine($"<rect x=\"{(width / 2 - totalApproxWidth / 2 - 4).ToString(culture)}\" y=\"{(totalDimensionLineY - 5).ToString(culture)}\" width=\"{(totalApproxWidth + 8).ToString(culture)}\" height=\"10\" fill=\"white\" />");
            svg.AppendLine($"<text x=\"{(width / 2).ToString(culture)}\" y=\"{(totalDimensionLineY + 3).ToString(culture)}\" font-family=\"GOST Type B, GOST Type A, ISOCPEUR, GOST Common, Arial\" font-style=\"italic\" font-size=\"10\" fill=\"black\" text-anchor=\"middle\">{totalText}</text>");

            svg.AppendLine("</svg>");
            return svg.ToString();
        }

        /// <summary>
        /// Проверяет соответствие длин, углов реза и количества деталей в визуальном отчёте
        /// данным из вкладки «Группировка». Выявляет ошибки, при которых в схеме раскроя
        /// отображаются некорректные размеры.
        /// </summary>
        /// <param name="groupedData">Сгруппированные строки по артикулам (из GroupingControl.GetGroupedData()).</param>
        public static List<string> ValidateVisualReportData(
            Dictionary<string, System.Data.DataRow[]> groupedData,
            string valColumnName,
            string leftAngleColumnName,
            string rightAngleColumnName,
            string qtyColumnName,
            List<CuttingService.OptimizationResult> results,
            double defaultCutWidth = 4,
            Dictionary<string, System.Collections.ObjectModel.ObservableCollection<ManualCutRow>> manualCutsByArticle = null)
        {
            var errors = new List<string>();
            if (groupedData == null || results == null || string.IsNullOrEmpty(valColumnName))
                return errors;

            // Собираем все длины из результатов раскроя по артикулам
            var actualPartsByArticle = new Dictionary<string, List<double>>();
            foreach (var result in results)
            {
                if (result.DetailedBars == null || result.DetailedBars.Count == 0) continue;

                double cutWidth = defaultCutWidth;
                string searchKey = result.IsManualCut ? result.OriginalGroupKey : result.GroupKey;

                if (!actualPartsByArticle.TryGetValue(searchKey, out var list))
                {
                    list = new List<double>();
                    actualPartsByArticle[searchKey] = list;
                }

                foreach (var bar in result.DetailedBars)
                {
                    if (bar?.Parts == null) continue;
                    foreach (var part in bar.Parts)
                    {
                        if (part == null) continue;
                        double originalLength = part.Length - cutWidth;
                        if (originalLength > 0)
                            list.Add(Math.Round(originalLength, 2));
                    }
                }
            }

            LinearCutWpf.Services.Diagnostic.LogGroupedDataDiagnostics(groupedData, valColumnName, qtyColumnName);

            // Сравниваем с ожидаемыми длинами из groupedData
            foreach (var kvp in groupedData)
            {
                string article = kvp.Key;
                var articleRows = kvp.Value;
                if (articleRows == null || articleRows.Length == 0) continue;

                // Собираем ожидаемые длины из строк группировки (с учётом количества)
                var expectedLengths = new List<double>();
                foreach (var row in articleRows)
                {
                    double length = 0;
                    if (row[valColumnName] != DBNull.Value)
                        length = Convert.ToDouble(row[valColumnName]);
                    if (length <= 0) continue;

                    int qty = Convert.ToInt32(row[qtyColumnName]);

                    for (int i = 0; i < qty; i++)
                        expectedLengths.Add(Math.Round(length, 2));
                }

                // Вычитаем детали, попавшие в ручной раскрой (повторяем логику CuttingService.OptimizeAllGroups)
                if (manualCutsByArticle != null && manualCutsByArticle.TryGetValue(article, out var articleManualCuts))
                {
                    var manualParts = new List<double>();
                    foreach (var mr in articleManualCuts)
                    {
                        int mrCount = mr.Count > 0 ? mr.Count : 1;
                        for (int i = 0; i < mrCount; i++)
                        {
                            if (double.TryParse(mr.Size1, out var s1) && s1 > 0) manualParts.Add(s1 + defaultCutWidth);
                            if (double.TryParse(mr.Size2, out var s2) && s2 > 0) manualParts.Add(s2 + defaultCutWidth);
                            if (double.TryParse(mr.Size3, out var s3) && s3 > 0) manualParts.Add(s3 + defaultCutWidth);
                            if (double.TryParse(mr.Size4, out var s4) && s4 > 0) manualParts.Add(s4 + defaultCutWidth);
                        }
                    }

                    if (manualParts.Count > 0)
                    {
                        var filtered = new List<double>();
                        foreach (var l in expectedLengths)
                        {
                            double lWithCut = l + defaultCutWidth;
                            int idx = manualParts.FindIndex(mp => Math.Abs(mp - lWithCut) < 0.01);
                            if (idx >= 0)
                                manualParts.RemoveAt(idx);
                            else
                                filtered.Add(l);
                        }
                        expectedLengths = filtered;
                    }
                }

                actualPartsByArticle.TryGetValue(article, out var actualLengths);
                if (actualLengths == null) actualLengths = new List<double>();

                // Сортируем оба списка для сравнения
                expectedLengths.Sort();
                actualLengths.Sort();

                if (expectedLengths.Count != actualLengths.Count)
                {
                    errors.Add($"[{article}] Количество деталей: ожидалось {expectedLengths.Count}, в отчёте {actualLengths.Count}");
                    continue;
                }

                // Сравниваем каждую длину
                int mismatchCount = 0;
                for (int i = 0; i < expectedLengths.Count; i++)
                {
                    if (Math.Abs(expectedLengths[i] - actualLengths[i]) > 0.5)
                    {
                        mismatchCount++;
                        if (mismatchCount <= 5)
                            errors.Add($"[{article}] Несовпадение длины #{i}: ожидалось {expectedLengths[i]}, в отчёте {actualLengths[i]}");
                    }
                }
                if (mismatchCount > 5)
                    errors.Add($"[{article}] ... и ещё {mismatchCount - 5} несовпадений длины");
            }

            // Проверяем артикулы, которые есть в результатах, но нет в groupedData
            foreach (var kvp in actualPartsByArticle)
            {
                if (!groupedData.ContainsKey(kvp.Key) && kvp.Value.Count > 0)
                {
                    errors.Add($"[{kvp.Key}] Артикул есть в результатах раскроя, но отсутствует в данных группировки");
                }
            }

            return errors;
        }

        private void ComposeFooter(QuestPDF.Infrastructure.IContainer container)
        {
            container.AlignCenter().Text(x =>
            {
                x.Span("Страница ");
                x.CurrentPageNumber();
                x.Span(" из ");
                x.TotalPages();
            });
        }

        /// <summary>
        /// Обработчик клика по чекбоксу "Выбрать все" в заголовке таблицы.
        /// </summary>
        private void chkSelectAll_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox chk)
            {
                bool isChecked = chk.IsChecked ?? false;
                foreach (var row in _exportData)
                {
                    row.IsSelected = isChecked;
                }
            }
        }

        /// <summary>
        /// Обработчик нажатия клавиш в таблице экспорта.
        /// Позволяет переключать выбор строки пробелом.
        /// </summary>
        private void dgExportData_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Space)
            {
                if (dgExportData.SelectedItem is ExportRowModel selectedRow)
                {
                    selectedRow.IsSelected = !selectedRow.IsSelected;
                    e.Handled = true; // предотвращаем стандартную обработку пробела
                }
            }
        }
    }
}
