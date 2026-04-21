using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using ClosedXML.Excel;
using LinearCutWpf.Models;
using LinearCutWpf.Services;
using Microsoft.Win32;

namespace LinearCutWpf.Controls
{
    /// <summary>
    /// Контрол для импорта данных (Excel) и настройки глобальных параметров раскроя.
    /// Отвечает за маппинг колонок (ролей: Ключ, Имя, Длина, Количество, Углы), 
    /// выбор длины хлыста и пресета по умолчанию, а также за управление списком хлыстов и пресетов.
    /// </summary>
    public partial class DataSettingsControl : UserControl
    {
        private string _currentFilePath;
        private HashSet<int> _invalidRows = new HashSet<int>();
        private bool _isLoading = false;
        private DataStoreService _dataStore => DataStoreService.Instance;
        private readonly Dictionary<string, ColumnRoleSelector> _columnRoleSelectors = new Dictionary<string, ColumnRoleSelector>();

        private double _defaultBarLength = 6000;
        private PresetModel _defaultPreset = null;

        private List<PresetModel> _presets = new List<PresetModel>();
        private List<StockLengthModel> _stockLengths = new List<StockLengthModel>();

        private DataTable _columnConfigTable;

        public double DefaultBarLength => _defaultBarLength;
        public PresetModel DefaultPreset => _defaultPreset;
        public List<PresetModel> Presets => _presets;
        public List<StockLengthModel> StockLengths => _stockLengths;
        public DataTable MainDataTable => _dataStore.ProcessedDataTable;
        public DataTable ColumnConfigTable => _columnConfigTable;
        public HashSet<int> InvalidRows => _invalidRows;
        public string ObjectName => tbObjectName.Text.Trim();

        /// <summary>
        /// Событие, вызываемое после успешной загрузки и первичной обработки данных из файла.
        /// </summary>
        public event EventHandler DataLoaded;
        
        /// <summary>
        /// Событие, вызываемое при изменении глобальных настроек (длины хлыста или пресета) или конфигурации столбцов.
        /// </summary>
        public event EventHandler SettingsChanged;

        /// <summary>
        /// Инициализирует новый экземпляр <see cref="DataSettingsControl"/> и загружает сохраненные настройки.
        /// </summary>
        public DataSettingsControl()
        {
            InitializeComponent();
            LoadInitialData();
            
            // Подписываемся на изменения обработанных данных
            _dataStore.ProcessedDataChanged += OnProcessedDataChanged;
            
            this.Unloaded += (s, e) =>
            {
                _dataStore.ProcessedDataChanged -= OnProcessedDataChanged;
            };
        }

        private void OnProcessedDataChanged(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_dataStore.ProcessedDataTable != null)
                {
                    dgInput.ItemsSource = _dataStore.ProcessedDataTable.DefaultView;
                    // После смены ItemsSource колонки пересоздаются — восстанавливаем селекторы и раскраску
                    InitializeColumnRoleSelectors();
                    RefreshColumnsVisuals();
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void LoadInitialData()
        {
            double savedWidth = CutSettingsProvider.LoadLeftPanelWidth();
            if (savedWidth > 0)
                LeftColumn.Width = new GridLength(savedWidth);

            _presets = CutSettingsProvider.LoadAll();
            dgPresets.ItemsSource = _presets;

            _stockLengths = CutSettingsProvider.LoadStockLengths();
            _defaultBarLength = CutSettingsProvider.LoadDefaultStockLength();
            UpdateDefaultCombos();
        }

        private void GridSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            CutSettingsProvider.SaveLeftPanelWidth(LeftColumn.Width.Value);
        }

        private void UpdateDefaultCombos()
        {
            cbDefaultStock.ItemsSource = _stockLengths.Select(s => s.Length).ToList();
            if (_stockLengths.Count > 0)
            {
                int idx = _stockLengths.FindIndex(s => s.Length == _defaultBarLength);
                cbDefaultStock.SelectedIndex = idx >= 0 ? idx : 0;
                _defaultBarLength = (double)cbDefaultStock.SelectedItem;
            }

            var presetItems = new List<object> { "(Нет)" };
            presetItems.AddRange(_presets);
            cbDefaultPreset.ItemsSource = presetItems;
            cbDefaultPreset.DisplayMemberPath = "Name";

            string savedPresetName = CutSettingsProvider.LoadDefaultPresetName();
            if (!string.IsNullOrEmpty(savedPresetName))
            {
                int idx = _presets.FindIndex(p => p.Name == savedPresetName);
                cbDefaultPreset.SelectedIndex = idx >= 0 ? idx + 1 : 0;
            }
            else
            {
                cbDefaultPreset.SelectedIndex = 0;
            }
        }

        private void OnOpenFileClick(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog { Filter = "Excel|*.xlsx" };
            if (ofd.ShowDialog() == true)
            {
                if (IsFileLocked(ofd.FileName))
                {
                    MessageBox.Show($"Файл \"{ofd.FileName}\" занят другим приложением.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _currentFilePath = ofd.FileName;
                _isLoading = true;
                cbSheetSelector.Items.Clear();

                try
                {
                    using (var workbook = new XLWorkbook(_currentFilePath))
                    {
                        foreach (var ws in workbook.Worksheets)
                        {
                            var usedRange = ws.RangeUsed();
                            if (usedRange != null && usedRange.RowCount() > 0)
                                cbSheetSelector.Items.Add(ws.Name);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не удалось загрузить файл Excel: {ex.Message}\n\nФайл может содержать неисправные сводные таблицы.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    _currentFilePath = null;
                    _isLoading = false;
                    return;
                }

                _isLoading = false;
                if (cbSheetSelector.Items.Count > 0)
                    cbSheetSelector.SelectedIndex = 0;
            }
        }

        private bool IsFileLocked(string filePath)
        {
            try
            {
                using (var stream = System.IO.File.Open(filePath, System.IO.FileMode.Open, System.IO.FileAccess.ReadWrite, System.IO.FileShare.None))
                {
                    return false;
                }
            }
            catch (System.IO.IOException)
            {
                return true;
            }
        }

        private void OnSheetSelected(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoading && cbSheetSelector.SelectedItem != null)
                LoadDataFromSheet(cbSheetSelector.SelectedItem.ToString());
        }

        private void LoadDataFromSheet(string sheetName)
        {
            if (string.IsNullOrEmpty(_currentFilePath) || string.IsNullOrEmpty(sheetName)) return;
            try
            {
                using var workbook = new XLWorkbook(_currentFilePath);
                var ws = workbook.Worksheet(sheetName);
                var rawDataTable = new DataTable();

                var headerRow = ws.FirstRowUsed();
                if (headerRow == null) { MessageBox.Show("Лист пуст!"); return; }

                foreach (var cell in headerRow.CellsUsed())
                    rawDataTable.Columns.Add(cell.GetString());

                // Создаем конфигурацию столбцов
                _columnConfigTable = new DataTable();
                _columnConfigTable.Columns.Add("ColName", typeof(string));
                _columnConfigTable.Columns.Add("IsKey", typeof(bool));
                _columnConfigTable.Columns.Add("IsName", typeof(bool));
                _columnConfigTable.Columns.Add("IsVal", typeof(bool));
                _columnConfigTable.Columns.Add("IsQty", typeof(bool));
                _columnConfigTable.Columns.Add("IsLeftAngle", typeof(bool));
                _columnConfigTable.Columns.Add("IsRightAngle", typeof(bool));
                _columnConfigTable.Columns.Add("IsColor", typeof(bool));

                foreach (DataColumn c in rawDataTable.Columns)
                    _columnConfigTable.Rows.Add(c.ColumnName, false, false, false, false, false, false, false);

                // Читаем данные
                foreach (var row in ws.RowsUsed().Skip(1))
                {
                    DataRow dr = rawDataTable.NewRow();
                    for (int i = 0; i < rawDataTable.Columns.Count; i++)
                    {
                        if (row.Cell(i + 1).IsEmpty())
                        {
                            dr[i] = DBNull.Value;
                        }
                        else
                        {
                            dr[i] = row.Cell(i + 1).GetString().Trim();
                        }
                    }
                    rawDataTable.Rows.Add(dr);
                }

                // Инициализируем DataStoreService сырыми данными (OnProcessedDataChanged обновит UI)
                _dataStore.Initialize(rawDataTable, _columnConfigTable, _currentFilePath);

                DataLoaded?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex) { MessageBox.Show("Ошибка загрузки: " + ex.Message); }
        }

        public List<string> GetCheckedCols(string colType)
        {
            if (_columnConfigTable == null) return new List<string>();
            var result = _columnConfigTable.AsEnumerable()
                .Where(r => r[colType] != DBNull.Value && (bool)r[colType])
                .Select(r => r["ColName"].ToString())
                .ToList();
            return result;
        }

        private void OnDgInputLoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (_dataStore.ProcessedDataTable == null) return;
            var row = e.Row;
            int rowIndex = e.Row.GetIndex();

            bool isInvalid = _invalidRows.Contains(rowIndex);
            
            if (isInvalid)
            {
                row.Background = Brushes.LightCoral;
                row.Foreground = Brushes.White;
                return;
            }

            // Подсвечиваем только ячейки, где ключи были автозаполнены
            var autoFilledCols = new List<string>();
            foreach (var key in _dataStore.AutoFilledKeys)
            {
                if (key.row == rowIndex)
                    autoFilledCols.Add(key.colName);
            }

            if (autoFilledCols.Count > 0)
            {
                row.Dispatcher.BeginInvoke(new Action(() =>
                {
                    var presenter = FindVisualChild<DataGridCellsPresenter>(row);
                    if (presenter == null) return;

                    foreach (var col in dgInput.Columns)
                    {
                        string colName = col.Header is ColumnRoleSelector selector ? selector.ColumnName : col.Header?.ToString();
                        if (string.IsNullOrEmpty(colName)) continue;

                        if (autoFilledCols.Contains(colName))
                        {
                            var cell = presenter.ItemContainerGenerator.ContainerFromIndex(dgInput.Columns.IndexOf(col)) as DataGridCell;
                            if (cell != null)
                            {
                                cell.Foreground = Brushes.Red;
                            }
                        }
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private void OnDgInputAutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            string colName = e.PropertyName;

            // Скрываем служебный столбец _ArticleKey_
            if (colName == "_ArticleKey_")
            {
                e.Cancel = true;
                return;
            }

            var selector = new ColumnRoleSelector
            {
                ColumnName = colName
            };
            selector.RoleChanged += OnColumnRoleChanged;

            // Сохраняем ссылку на селектор
            _columnRoleSelectors[colName] = selector;

            // Устанавливаем кастомный заголовок
            e.Column.Header = selector;

            // Раскраска ячеек по ролям будет применена в RefreshColumnsVisuals
        }

        private void OnDgInputLoaded(object sender, RoutedEventArgs e)
        {
            dgInput.Dispatcher.BeginInvoke(new Action(() =>
            {
                InitializeColumnRoleSelectors();
                RefreshColumnsVisuals();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        /// <summary>
        /// Инициализирует состояние селекторов ролей из _columnConfigTable.
        /// </summary>
        private void InitializeColumnRoleSelectors()
        {
            if (_columnConfigTable == null) return;

            foreach (DataRow row in _columnConfigTable.Rows)
            {
                string colName = row["ColName"]?.ToString();
                if (string.IsNullOrEmpty(colName)) continue;

                if (_columnRoleSelectors.TryGetValue(colName, out var selector))
                {
                    string[] roleCols = { "IsKey", "IsName", "IsVal", "IsQty", "IsLeftAngle", "IsRightAngle", "IsColor" };
                    foreach (var roleCol in roleCols)
                    {
                        if (row.Table.Columns.Contains(roleCol) && row[roleCol] != DBNull.Value && (bool)row[roleCol])
                        {
                            selector.SetRoleChecked(roleCol, true);
                        }
                    }
                }
            }
        }

        private void OnColumnRoleChanged(object sender, RoleChangedEventArgs e)
        {
            if (_columnConfigTable == null) return;

            // Обновляем _columnConfigTable
            var row = _columnConfigTable.AsEnumerable().FirstOrDefault(r => r["ColName"]?.ToString() == e.ColumnName);
            if (row != null)
            {
                if (row.Table.Columns.Contains(e.RoleKey))
                {
                    row[e.RoleKey] = e.IsChecked;
                }
            }

            // Распространяем изменение на все остальные селекторы: если другой столбец имел эту же роль — снимаем
            // Кроме IsKey, который может быть назначен нескольким столбцам
            if (e.IsChecked && e.RoleKey != "IsKey")
            {
                foreach (var kvp in _columnRoleSelectors)
                {
                    if (kvp.Key != e.ColumnName)
                    {
                        var otherSelector = kvp.Value;
                        if (otherSelector.GetCheckedRole() == e.RoleKey)
                        {
                            otherSelector.SetRoleChecked(e.RoleKey, false);

                            // Обновляем DataTable
                            var otherRow = _columnConfigTable.AsEnumerable().FirstOrDefault(r => r["ColName"]?.ToString() == kvp.Key);
                            if (otherRow != null && otherRow.Table.Columns.Contains(e.RoleKey))
                            {
                                otherRow[e.RoleKey] = false;
                            }
                        }
                    }
                }
            }

            // Обновляем хранилище и UI
            _dataStore.UpdateColumnConfig(_columnConfigTable);
            RefreshColumnsVisuals();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void RefreshColumnsVisuals()
        {
            if (_columnConfigTable == null || dgInput.Columns.Count == 0) return;

            // Цвета ролей
            var roleColors = new Dictionary<string, Brush>
            {
                { "IsKey", Brushes.LightGreen },
                { "IsName", Brushes.LightPink },
                { "IsVal", Brushes.LightYellow },
                { "IsQty", Brushes.LightCyan },
                { "IsLeftAngle", Brushes.LightGray },
                { "IsRightAngle", Brushes.LightGray },
                { "IsColor", Brushes.LightSalmon }
            };

            var baseHeaderStyle = FindResource(typeof(DataGridColumnHeader)) as Style;
            var baseCellStyle = FindResource(typeof(DataGridCell)) as Style;

            foreach (var col in dgInput.Columns)
            {
                string colName = col.Header is ColumnRoleSelector selector ? selector.ColumnName : col.Header?.ToString();
                if (string.IsNullOrEmpty(colName)) continue;

                var row = _columnConfigTable.AsEnumerable().FirstOrDefault(r => r["ColName"]?.ToString() == colName);
                if (row == null) continue;

                Brush roleBrush = null;
                foreach (var kvp in roleColors)
                {
                    if (row.Table.Columns.Contains(kvp.Key) && row[kvp.Key] != DBNull.Value && (bool)row[kvp.Key])
                    {
                        roleBrush = kvp.Value;
                        break; // первый найденный цвет
                    }
                }

                if (roleBrush != null)
                {
                    // Стиль заголовка
                    var headerStyle = new Style(typeof(DataGridColumnHeader), baseHeaderStyle);
                    headerStyle.Setters.Add(new Setter(Control.BackgroundProperty, roleBrush));
                    col.HeaderStyle = headerStyle;

                    // Стиль ячеек — раскрашивает весь столбец
                    var cellStyle = new Style(typeof(DataGridCell), baseCellStyle);
                    cellStyle.Setters.Add(new Setter(Control.BackgroundProperty, roleBrush));
                    cellStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Normal));
                    col.CellStyle = cellStyle;
                }
                else
                {
                    col.ClearValue(DataGridColumn.HeaderStyleProperty);
                    col.ClearValue(DataGridColumn.CellStyleProperty);
                }
            }
        }

        private void OnDefaultStockChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbDefaultStock.SelectedItem != null)
            {
                _defaultBarLength = (double)cbDefaultStock.SelectedItem;
                CutSettingsProvider.SaveDefaultStockLength(_defaultBarLength);
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnDefaultPresetChanged(object sender, SelectionChangedEventArgs e)
        {
            _defaultPreset = cbDefaultPreset.SelectedItem as PresetModel;
            CutSettingsProvider.SaveDefaultPresetName(_defaultPreset?.Name);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnAddStock(object sender, RoutedEventArgs e)
        {
            var inputWindow = new Window
            {
                Title = "Добавить хлыст",
                Width = 300,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize
            };

            var panel = new StackPanel { Margin = new Thickness(10) };
            panel.Children.Add(new TextBlock { Text = "Длина хлыста (мм):" });
            var tb = new TextBox { Text = "6000", Margin = new Thickness(0, 5, 0, 0) };
            panel.Children.Add(tb);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            var btnOk = new Button { Content = "OK", Width = 70, Margin = new Thickness(0, 0, 5, 0) };
            var btnCancel = new Button { Content = "Отмена", Width = 70 };
            btnOk.Click += (s, ev) => { inputWindow.DialogResult = true; inputWindow.Close(); };
            btnCancel.Click += (s, ev) => { inputWindow.DialogResult = false; inputWindow.Close(); };
            btnPanel.Children.Add(btnOk);
            btnPanel.Children.Add(btnCancel);
            panel.Children.Add(btnPanel);
            inputWindow.Content = panel;

            if (inputWindow.ShowDialog() == true && double.TryParse(tb.Text, out double len) && len > 0)
            {
                _stockLengths.Add(new StockLengthModel { Length = len, IsEnabled = true });
                UpdateDefaultCombos();
                cbDefaultStock.SelectedIndex = cbDefaultStock.Items.Count - 1;
                CutSettingsProvider.SaveStockLengths(_stockLengths);
            }
        }

        private void OnRemoveStock(object sender, RoutedEventArgs e)
        {
            if (_stockLengths.Count <= 1) { MessageBox.Show("Должен остаться хотя бы один хлыст!"); return; }
            if (cbDefaultStock.SelectedIndex >= 0)
            {
                int idx = cbDefaultStock.SelectedIndex;
                _stockLengths.RemoveAt(idx);
                UpdateDefaultCombos();
                if (cbDefaultStock.Items.Count > 0)
                    cbDefaultStock.SelectedIndex = Math.Min(idx, cbDefaultStock.Items.Count - 1);
                CutSettingsProvider.SaveStockLengths(_stockLengths);
            }
        }

        private void OnAddPreset(object sender, RoutedEventArgs e)
        {
            var inputWindow = new Window
            {
                Title = "Добавить пресет",
                Width = 350,
                Height = 280,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize
            };

            var panel = new Grid { Margin = new Thickness(10) };
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            panel.Children.Add(new TextBlock { Text = "Название:", VerticalAlignment = VerticalAlignment.Center });
            Grid.SetRow((panel.Children[^1] as TextBlock), 0); Grid.SetColumn((panel.Children[^1] as TextBlock), 0);
            var tbName = new TextBox { Text = "Новый пресет", Margin = new Thickness(0, 5, 0, 0) };
            panel.Children.Add(tbName); Grid.SetRow(tbName, 0); Grid.SetColumn(tbName, 1);

            panel.Children.Add(new TextBlock { Text = "Обрезка начало:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 5, 0, 0) });
            Grid.SetRow((panel.Children[^1] as TextBlock), 1); Grid.SetColumn((panel.Children[^1] as TextBlock), 0);
            var tbTrimStart = new TextBox { Text = "10", Margin = new Thickness(0, 5, 0, 0) };
            panel.Children.Add(tbTrimStart); Grid.SetRow(tbTrimStart, 1); Grid.SetColumn(tbTrimStart, 1);

            panel.Children.Add(new TextBlock { Text = "Обрезка конец:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 5, 0, 0) });
            Grid.SetRow((panel.Children[^1] as TextBlock), 2); Grid.SetColumn((panel.Children[^1] as TextBlock), 0);
            var tbTrimEnd = new TextBox { Text = "10", Margin = new Thickness(0, 5, 0, 0) };
            panel.Children.Add(tbTrimEnd); Grid.SetRow(tbTrimEnd, 2); Grid.SetColumn(tbTrimEnd, 1);

            panel.Children.Add(new TextBlock { Text = "Ширина реза:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 5, 0, 0) });
            Grid.SetRow((panel.Children[^1] as TextBlock), 3); Grid.SetColumn((panel.Children[^1] as TextBlock), 0);
            var tbCutWidth = new TextBox { Text = "4", Margin = new Thickness(0, 5, 0, 0) };
            panel.Children.Add(tbCutWidth); Grid.SetRow(tbCutWidth, 3); Grid.SetColumn(tbCutWidth, 1);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            var btnOk = new Button { Content = "OK", Width = 70, Margin = new Thickness(0, 0, 5, 0) };
            var btnCancel = new Button { Content = "Отмена", Width = 70 };
            btnOk.Click += (s, ev) => { inputWindow.DialogResult = true; inputWindow.Close(); };
            btnCancel.Click += (s, ev) => { inputWindow.DialogResult = false; inputWindow.Close(); };
            btnPanel.Children.Add(btnOk);
            btnPanel.Children.Add(btnCancel);
            panel.Children.Add(btnPanel); Grid.SetRow(btnPanel, 4); Grid.SetColumnSpan(btnPanel, 2);
            inputWindow.Content = panel;

            if (inputWindow.ShowDialog() == true)
            {
                _presets.Add(new PresetModel
                {
                    Name = tbName.Text,
                    TrimStart = double.TryParse(tbTrimStart.Text, out var ts) ? ts : 10,
                    TrimEnd = double.TryParse(tbTrimEnd.Text, out var te) ? te : 10,
                    CutWidth = double.TryParse(tbCutWidth.Text, out var cw) ? cw : 4
                });
                dgPresets.Items.Refresh();
                UpdateDefaultCombos();
                CutSettingsProvider.SaveAll(_presets);
            }
        }

        private void OnPresetRightClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var dep = (DependencyObject)e.OriginalSource;
            while (dep != null && dep is not DataGridRow)
                dep = VisualTreeHelper.GetParent(dep);

            if (dep is DataGridRow row)
            {
                dgPresets.SelectedIndex = row.GetIndex();
                var menu = new ContextMenu();
                var editItem = new MenuItem { Header = "Редактировать" };
                editItem.Click += (s, ev) => EditPreset(row.GetIndex());
                var deleteItem = new MenuItem { Header = "Удалить" };
                deleteItem.Click += (s, ev) => DeletePreset(row.GetIndex());
                menu.Items.Add(editItem);
                menu.Items.Add(deleteItem);
                menu.IsOpen = true;
            }
        }

        private void EditPreset(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _presets.Count) return;
            var preset = _presets[rowIndex];

            var inputWindow = new Window
            {
                Title = "Редактировать пресет",
                Width = 350,
                Height = 280,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize
            };

            var panel = new Grid { Margin = new Thickness(10) };
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            panel.Children.Add(new TextBlock { Text = "Название:", VerticalAlignment = VerticalAlignment.Center });
            Grid.SetRow((panel.Children[^1] as TextBlock), 0); Grid.SetColumn((panel.Children[^1] as TextBlock), 0);
            var tbName = new TextBox { Text = preset.Name, Margin = new Thickness(0, 5, 0, 0) };
            panel.Children.Add(tbName); Grid.SetRow(tbName, 0); Grid.SetColumn(tbName, 1);

            panel.Children.Add(new TextBlock { Text = "Обрезка начало:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 5, 0, 0) });
            Grid.SetRow((panel.Children[^1] as TextBlock), 1); Grid.SetColumn((panel.Children[^1] as TextBlock), 0);
            var tbTrimStart = new TextBox { Text = preset.TrimStart.ToString(), Margin = new Thickness(0, 5, 0, 0) };
            panel.Children.Add(tbTrimStart); Grid.SetRow(tbTrimStart, 1); Grid.SetColumn(tbTrimStart, 1);

            panel.Children.Add(new TextBlock { Text = "Обрезка конец:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 5, 0, 0) });
            Grid.SetRow((panel.Children[^1] as TextBlock), 2); Grid.SetColumn((panel.Children[^1] as TextBlock), 0);
            var tbTrimEnd = new TextBox { Text = preset.TrimEnd.ToString(), Margin = new Thickness(0, 5, 0, 0) };
            panel.Children.Add(tbTrimEnd); Grid.SetRow(tbTrimEnd, 2); Grid.SetColumn(tbTrimEnd, 1);

            panel.Children.Add(new TextBlock { Text = "Ширина реза:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 5, 0, 0) });
            Grid.SetRow((panel.Children[^1] as TextBlock), 3); Grid.SetColumn((panel.Children[^1] as TextBlock), 0);
            var tbCutWidth = new TextBox { Text = preset.CutWidth.ToString(), Margin = new Thickness(0, 5, 0, 0) };
            panel.Children.Add(tbCutWidth); Grid.SetRow(tbCutWidth, 3); Grid.SetColumn(tbCutWidth, 1);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            var btnOk = new Button { Content = "OK", Width = 70, Margin = new Thickness(0, 0, 5, 0) };
            var btnCancel = new Button { Content = "Отмена", Width = 70 };
            btnOk.Click += (s, ev) => { inputWindow.DialogResult = true; inputWindow.Close(); };
            btnCancel.Click += (s, ev) => { inputWindow.DialogResult = false; inputWindow.Close(); };
            btnPanel.Children.Add(btnOk);
            btnPanel.Children.Add(btnCancel);
            panel.Children.Add(btnPanel); Grid.SetRow(btnPanel, 4); Grid.SetColumnSpan(btnPanel, 2);
            inputWindow.Content = panel;

            if (inputWindow.ShowDialog() == true)
            {
                preset.Name = tbName.Text;
                preset.TrimStart = double.TryParse(tbTrimStart.Text, out var ts) ? ts : preset.TrimStart;
                preset.TrimEnd = double.TryParse(tbTrimEnd.Text, out var te) ? te : preset.TrimEnd;
                preset.CutWidth = double.TryParse(tbCutWidth.Text, out var cw) ? cw : preset.CutWidth;
                dgPresets.Items.Refresh();
                CutSettingsProvider.SaveAll(_presets);
                UpdateDefaultCombos();
            }
        }

        private void DeletePreset(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _presets.Count) return;
            var preset = _presets[rowIndex];
            if (MessageBox.Show($"Удалить пресет \"{preset.Name}\"?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _presets.RemoveAt(rowIndex);
                dgPresets.Items.Refresh();
                CutSettingsProvider.SaveAll(_presets);
                UpdateDefaultCombos();
            }
        }

        public void LoadSettings(DataTable mainDataTable, double defaultBarLength, PresetModel defaultPreset,
            List<StockLengthModel> stockLengths, List<PresetModel> presets, DataTable columnConfig)
        {
            _defaultBarLength = defaultBarLength;
            _defaultPreset = defaultPreset;
            _stockLengths = stockLengths;
            _presets = presets;
            dgPresets.ItemsSource = _presets;
            UpdateDefaultCombos();

            if (defaultPreset != null)
            {
                int idx = presets.FindIndex(p => p.Name == defaultPreset.Name);
                if (idx >= 0)
                {
                    _defaultPreset = presets[idx];
                    cbDefaultPreset.SelectedIndex = idx + 1;
                    CutSettingsProvider.SaveDefaultPresetName(_defaultPreset.Name);
                }
            }

            if (columnConfig != null)
            {
                _columnConfigTable = columnConfig;
            }
            
            // Обновляем обработанные данные через DataStoreService (OnProcessedDataChanged обновит UI)
            if (mainDataTable != null && columnConfig != null)
            {
                _dataStore.Initialize(mainDataTable, columnConfig, null);
            }
        }

        public bool ValidateData()
        {
            if (_dataStore.ProcessedDataTable == null) return true;

            var vals = GetCheckedCols("IsVal");
            var qtys = GetCheckedCols("IsQty");
            
            if (!vals.Any()) return true;

            _invalidRows.Clear();
            
            DataTable errorsTable = _dataStore.ProcessedDataTable.Clone();
            errorsTable.Columns.Add("Описание ошибки", typeof(string));

            for (int i = 0; i < _dataStore.ProcessedDataTable.Rows.Count; i++)
            {
                var row = _dataStore.ProcessedDataTable.Rows[i];
                List<string> errors = new List<string>();

                foreach (var valCol in vals)
                {
                    if (row[valCol] == DBNull.Value || string.IsNullOrWhiteSpace(row[valCol].ToString()))
                    {
                        errors.Add($"Длина '{valCol}' пустая");
                    }
                    else if (!double.TryParse(row[valCol].ToString().Replace('.', ','), out double v) || v <= 0)
                    {
                        errors.Add($"Длина '{valCol}' ({row[valCol]}) не является положительным числом");
                    }
                }

                foreach (var qtyCol in qtys)
                {
                    if (row[qtyCol] != DBNull.Value && !string.IsNullOrWhiteSpace(row[qtyCol].ToString()))
                    {
                        if (!double.TryParse(row[qtyCol].ToString().Replace('.', ','), out double q) || q <= 0)
                        {
                            errors.Add($"Кол-во '{qtyCol}' ({row[qtyCol]}) не является положительным числом");
                        }
                    }
                }

                if (errors.Count > 0)
                {
                    _invalidRows.Add(i);
                    var newRow = errorsTable.NewRow();
                    for (int c = 0; c < _dataStore.ProcessedDataTable.Columns.Count; c++)
                    {
                        newRow[c] = row[c];
                    }
                    newRow["Описание ошибки"] = string.Join("; ", errors);
                    errorsTable.Rows.Add(newRow);
                }
            }

            if (errorsTable.Rows.Count > 0)
            {
                var dialog = new ValidationDialog(errorsTable);
                dialog.Owner = Window.GetWindow(this);
                if (dialog.ShowDialog() == true && dialog.Ignored)
                {
                    RefreshDataGridVisuals();
                    return true;
                }
                
                _invalidRows.Clear();
                RefreshDataGridVisuals();
                return false;
            }

            RefreshDataGridVisuals();
            return true;
        }

        private void RefreshDataGridVisuals()
        {
            if (dgInput != null)
            {
                dgInput.Items.Refresh();
            }
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                    return typedChild;

                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        public void UpdateArticleSettings(Dictionary<string, ArticleSettings> articleSettings)
        {
            if (articleSettings != null && articleSettings.Count > 0)
            {
                var firstSettings = articleSettings.Values.First();
                
                if (firstSettings.BarLength.HasValue)
                {
                    _defaultBarLength = firstSettings.BarLength.Value;
                    CutSettingsProvider.SaveDefaultStockLength(_defaultBarLength);
                }
                
                if (firstSettings.Preset != null)
                {
                    _defaultPreset = firstSettings.Preset;
                    CutSettingsProvider.SaveDefaultPresetName(_defaultPreset.Name);
                }
                
                UpdateDefaultCombos();
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}