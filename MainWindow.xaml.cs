using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Collections.ObjectModel;
using System.Linq;
using System.Data;
using LinearCutWpf.Controls;
using LinearCutWpf.Models;
using LinearCutWpf.Services;

namespace LinearCutWpf
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            
            // Подписываемся на событие изменения настроек
            dataSettingsControl.SettingsChanged += OnSettingsChanged;
            
            // Подписываемся на событие загрузки данных из Excel
            dataSettingsControl.DataLoaded += OnDataLoaded;

            // Подписываемся на событие применения настроек из GroupingControl
            groupingControl.SettingsApplied += OnGroupingSettingsApplied;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var settings = Services.CutSettingsProvider.LoadWindowSettings();
            
            if (!double.IsNaN(settings.Left) && !double.IsNaN(settings.Top))
            {
                this.Left = settings.Left;
                this.Top = settings.Top;
            }
            if (settings.Width > 0) this.Width = settings.Width;
            if (settings.Height > 0) this.Height = settings.Height;

            if (System.Enum.TryParse(settings.WindowState, out WindowState state))
            {
                this.WindowState = state;
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Сохраняем данные о высотах профилей при закрытии программы
            SaveProfileHeights();
            
            var settings = new Services.CutSettingsProvider.WindowSettings
            {
                Left = this.WindowState == WindowState.Normal ? this.Left : this.RestoreBounds.Left,
                Top = this.WindowState == WindowState.Normal ? this.Top : this.RestoreBounds.Top,
                Width = this.WindowState == WindowState.Normal ? this.Width : this.RestoreBounds.Width,
                Height = this.WindowState == WindowState.Normal ? this.Height : this.RestoreBounds.Height,
                WindowState = this.WindowState.ToString()
            };
            
            Services.CutSettingsProvider.SaveWindowSettings(settings);
        }

        private void OnDataLoaded(object sender, System.EventArgs e)
        {
            // Данные уже инициализированы в DataSettingsControl.LoadDataFromSheet
            // Просто обновляем настройки артикулов
            UpdateArticleSettings();
        }

        private void OnSettingsChanged(object sender, System.EventArgs e)
        {
            // Обновляем конфигурацию столбцов в хранилище
            if (dataSettingsControl.ColumnConfigTable != null)
            {
                DataStoreService.Instance.UpdateColumnConfig(dataSettingsControl.ColumnConfigTable);
            }
            
            UpdateArticleSettings();
        }

        private void UpdateArticleSettings()
        {
            var articleSettingsControl = (ArticleSettingsControl)FindName("articleSettingsControl");
            if (articleSettingsControl == null || dataSettingsControl.MainDataTable == null)
                return;

            var currentSettings = articleSettingsControl.GetSettings();

            articleSettingsControl.Initialize(
                dataSettingsControl.MainDataTable,
                dataSettingsControl.GetCheckedCols("IsKey"),
                dataSettingsControl.GetCheckedCols("IsName"),
                dataSettingsControl.GetCheckedCols("IsQty"),
                dataSettingsControl.GetCheckedCols("IsVal"),
                new ObservableCollection<StockLengthModel>(dataSettingsControl.StockLengths),
                new ObservableCollection<PresetModel>(dataSettingsControl.Presets),
                currentSettings,
                dataSettingsControl.DefaultBarLength,
                dataSettingsControl.DefaultPreset
            );
        }

        private void UpdateGrouping()
        {
            if (groupingControl == null || dataSettingsControl.MainDataTable == null)
                return;

            var articleSettingsControl = (ArticleSettingsControl)FindName("articleSettingsControl");
            var articleSettings = articleSettingsControl?.GetSettings() ?? new System.Collections.Generic.Dictionary<string, ArticleSettings>();

            groupingControl.Initialize(
                dataSettingsControl.DefaultBarLength,
                dataSettingsControl.DefaultPreset,
                new ObservableCollection<StockLengthModel>(dataSettingsControl.StockLengths),
                dataSettingsControl.Presets,
                dataSettingsControl.MainDataTable,
                dataSettingsControl.GetCheckedCols,
                dataSettingsControl.InvalidRows
            );

            // Pass article settings to GroupingControl if needed, or apply them
            foreach (var setting in articleSettings)
            {
                var articleRow = groupingControl.GetOrCreateArticleSettings(setting.Key);
                articleRow.BarLength = setting.Value.BarLength;
                articleRow.Preset = setting.Value.Preset;
                articleRow.ArticleDescription = setting.Value.ArticleDescription;
            }

            groupingControl.RunGroupingWithTabs();
        }

        private void OnGroupingSettingsApplied(object sender, System.Collections.Generic.Dictionary<string, ArticleSettings> settings)
        {
            // Обновляем настройки в DataSettingsControl
            dataSettingsControl.UpdateArticleSettings(settings);
        }

        private bool _isRevertingTab = false;

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl tabControl && tabControl.Name == "tabControl")
            {
                if (_isRevertingTab) return;

                // Сохраняем высоты при уходе со вкладки "Настройка артикулов"
                if (e.RemovedItems.Count > 0 && e.RemovedItems[0] is TabItem removedTab && 
                    removedTab.Header?.ToString() == "Настройка артикулов")
                {
                    SaveProfileHeights();
                }

                if (e.RemovedItems.Count > 0 && e.RemovedItems[0] is TabItem removedTab2 && removedTab2.Header?.ToString() == "Ввод данных")
                {
                    if (!dataSettingsControl.ValidateData())
                    {
                        _isRevertingTab = true;
                        Dispatcher.BeginInvoke(new System.Action(() => 
                        {
                            tabControl.SelectedItem = removedTab2;
                            _isRevertingTab = false;
                        }));
                        return;
                    }
                }

                if (tabControl.SelectedItem is TabItem selectedTab)
                {
                    if (selectedTab.Header?.ToString() == "Настройка артикулов")
                    {
                        // Загружаем высоты при переходе на вкладку
                        LoadProfileHeights();
                    }
                    else if (selectedTab.Header?.ToString() == "Группировка")
                    {
                        UpdateGrouping();
                    }
                    else if (selectedTab.Header?.ToString() == "Раскрой")
                    {
                        RunOptimization();
                    }
                }
            }
        }

        /// <summary>
        /// Сохраняет данные о высоте профилей в файл.
        /// </summary>
        private void SaveProfileHeights()
        {
            var articleSettingsControl = (ArticleSettingsControl)FindName("articleSettingsControl");
            if (articleSettingsControl != null)
            {
                // Сохранение теперь происходит в ArticleSettingsControl.GetSettings()
                var settings = articleSettingsControl.GetSettings();
            }
        }

        /// <summary>
        /// Загружает данные о высоте профилей из файла.
        /// </summary>
        private void LoadProfileHeights()
        {
            var articleSettingsControl = (ArticleSettingsControl)FindName("articleSettingsControl");
            if (articleSettingsControl != null)
            {
                // Обновляем настройки в ArticleSettingsControl (загрузка происходит внутри Initialize)
            articleSettingsControl.Initialize(
                dataSettingsControl.MainDataTable,
                dataSettingsControl.GetCheckedCols("IsKey"),
                dataSettingsControl.GetCheckedCols("IsName"),
                dataSettingsControl.GetCheckedCols("IsQty"),
                dataSettingsControl.GetCheckedCols("IsVal"),
                new ObservableCollection<StockLengthModel>(dataSettingsControl.StockLengths),
                new ObservableCollection<PresetModel>(dataSettingsControl.Presets),
                new System.Collections.Generic.Dictionary<string, ArticleSettings>(),
                dataSettingsControl.DefaultBarLength,
                dataSettingsControl.DefaultPreset
            );
            }
        }

        private void RunOptimization()
        {
            if (dataSettingsControl.MainDataTable == null || groupingControl.GroupingTabControl.Items.Count == 0)
                return;

            if (groupingControl.HasAnyManualErrors())
            {
                MessageBox.Show("Есть ошибки в ручном раскрое!\r\nИсправьте их перед запуском автоматического раскроя.", "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                tabControl.SelectedIndex = 1; // Возвращаемся на вкладку группировки
                return;
            }

            var keys = dataSettingsControl.GetCheckedCols("IsKey");
            var vals = dataSettingsControl.GetCheckedCols("IsVal");
            var qtyList = dataSettingsControl.GetCheckedCols("IsQty");
            var qtyCol = qtyList.FirstOrDefault();

            if (!keys.Any() || !vals.Any()) return;

            var optimizer = new Services.CuttingService();
            var results = new System.Collections.Generic.List<Services.CuttingService.OptimizationResult>();
            var allGroupsForVerification = new System.Collections.Generic.List<Services.CuttingService.GroupData>();

            var dtRows = dataSettingsControl.MainDataTable.Rows;
            var validRows = dtRows.Cast<DataRow>()
                .Where((r, i) => !dataSettingsControl.InvalidRows.Contains(i));
                
            var groupsData = validRows
                .GroupBy(r => DataHelper.GetArticleName(keys.Select(k => r[k]?.ToString())));

            foreach (var g in groupsData)
            {
                string groupKey = g.Key;
                // Игнорируем пустые артикулы
                if (string.IsNullOrWhiteSpace(groupKey)) continue;
                var dt = dataSettingsControl.MainDataTable.Clone();
                foreach (var r in g) dt.ImportRow(r);

                var articleSettings = groupingControl.GetOrCreateArticleSettings(groupKey);
                var preset = groupingControl.GetEffectivePreset(groupKey);

                var groupList = new System.Collections.Generic.List<Services.CuttingService.GroupData>
                {
                    new Services.CuttingService.GroupData
                    {
                        GroupKey = groupKey,
                        ValueColumnName = vals.FirstOrDefault(),
                        QtyColumnName = qtyCol,
                        Table = dt
                    }
                };

                allGroupsForVerification.AddRange(groupList);

                // Выбираем только те хлысты, которые разрешены для данного артикула
                var enabledStocks = new System.Collections.Generic.List<StockLengthModel>();
                if (articleSettings.BarLength != null)
                {
                    enabledStocks.Add(new StockLengthModel { Length = articleSettings.BarLength.Value, IsEnabled = true });
                }
                else
                {
                    enabledStocks = dataSettingsControl.StockLengths.Where(s => s.IsEnabled).ToList();
                }

                // Здесь берется коллекция ManualCuts из настроек конкретной группы/артикула
                var groupResults = optimizer.OptimizeAllGroups(
                    groupList,
                    enabledStocks,
                    articleSettings.ManualCuts,
                    preset
                );

                results.AddRange(groupResults);
            }

            if (!optimizer.VerifyOptimization(allGroupsForVerification, results))
            {
                int inC = 0; double inL = 0;
                foreach (var grp in allGroupsForVerification)
                {
                    if (grp.Table != null)
                    {
                        foreach (DataRow r in grp.Table.Rows)
                        {
                            var v = r[grp.ValueColumnName];
                            var q = string.IsNullOrEmpty(grp.QtyColumnName) ? null : r[grp.QtyColumnName];
                            
                            string valStr = v?.ToString().Replace('.', ',');
                            double l = string.IsNullOrWhiteSpace(valStr) ? 0 : (double.TryParse(valStr, out double dp) ? dp : 0);
                            
                            if (l > 0)
                            {
                                string qtyStr = q?.ToString().Replace('.', ',');
                                int c = 1;
                                if (!string.IsNullOrWhiteSpace(qtyStr))
                                {
                                    if (double.TryParse(qtyStr, out double dq) && dq > 0)
                                        c = (int)dq;
                                }
                                
                                inC += c;
                                inL += l * c;
                            }
                        }
                    }
                }
                int outC = results.Sum(r => r.TotalPartsCount);
                double outL = results.Sum(r => r.TotalPartsLength);
                
                MessageBox.Show($"Внимание: Количество или длина деталей в раскрое не совпадает с исходными данными!\r\nВход: {inC} шт, {inL} мм\r\nВыход: {outC} шт, {outL} мм", 
                                "Ошибка проверки", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            var resultsControlObj = (ResultsControl)FindName("resultsControl");
            if (resultsControlObj != null)
            {
                resultsControlObj.DisplayResults(results);
            }

            var exportControlObj = (ExportControl)FindName("exportControl");
            if (exportControlObj != null)
            {
                string nameColumnName = dataSettingsControl.GetCheckedCols("IsName").FirstOrDefault();
                var keyColumnNames = keys.ToList();
                string valColumnName = vals.FirstOrDefault();
                string objectName = dataSettingsControl.ObjectName;
                string leftAngleCol = dataSettingsControl.GetCheckedCols("IsLeftAngle").FirstOrDefault();
                string rightAngleCol = dataSettingsControl.GetCheckedCols("IsRightAngle").FirstOrDefault();
                exportControlObj.LoadData(results, dataSettingsControl.MainDataTable, keyColumnNames, nameColumnName, valColumnName, qtyCol, objectName, leftAngleCol, rightAngleCol);
            }
        }
    }
}
