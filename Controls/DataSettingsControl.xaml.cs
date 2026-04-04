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
        private DataTable _mainDataTable;
        private HashSet<(int row, string colName)> _autoFilledKeys = new HashSet<(int, string)>();
        private HashSet<int> _invalidRows = new HashSet<int>();
        private bool _isLoading = false;

        private double _defaultBarLength = 6000;
        private PresetModel _defaultPreset = null;

        private List<PresetModel> _presets = new List<PresetModel>();
        private List<StockLengthModel> _stockLengths = new List<StockLengthModel>();

        private DataTable _columnConfigTable;

        public double DefaultBarLength => _defaultBarLength;
        public PresetModel DefaultPreset => _defaultPreset;
        public List<PresetModel> Presets => _presets;
        public List<StockLengthModel> StockLengths => _stockLengths;
        public DataTable MainDataTable => _mainDataTable;
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
                _currentFilePath = ofd.FileName;
                _isLoading = true;
                cbSheetSelector.Items.Clear();

                using (var workbook = new XLWorkbook(_currentFilePath))
                {
                    foreach (var ws in workbook.Worksheets)
                    {
                        var usedRange = ws.RangeUsed();
                        if (usedRange != null && usedRange.RowCount() > 0)
                            cbSheetSelector.Items.Add(ws.Name);
                    }
                }

                _isLoading = false;
                if (cbSheetSelector.Items.Count > 0)
                    cbSheetSelector.SelectedIndex = 0;
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
                _mainDataTable = new DataTable();

                var headerRow = ws.FirstRowUsed();
                if (headerRow == null) { MessageBox.Show("Лист пуст!"); return; }

                foreach (var cell in headerRow.CellsUsed())
                    _mainDataTable.Columns.Add(cell.GetString());

                // Сначала создаем конфигурацию столбцов
                _columnConfigTable = new DataTable();
                _columnConfigTable.Columns.Add("ColName", typeof(string));
                _columnConfigTable.Columns.Add("IsKey", typeof(bool));
                _columnConfigTable.Columns.Add("IsName", typeof(bool));
                _columnConfigTable.Columns.Add("IsVal", typeof(bool));
                _columnConfigTable.Columns.Add("IsQty", typeof(bool));
                _columnConfigTable.Columns.Add("IsLeftAngle", typeof(bool));
                _columnConfigTable.Columns.Add("IsRightAngle", typeof(bool));

                foreach (DataColumn c in _mainDataTable.Columns)
                    _columnConfigTable.Rows.Add(c.ColumnName, false, false, false, false, false, false);

                // Затем читаем данные
                foreach (var row in ws.RowsUsed().Skip(1))
                {
                    DataRow dr = _mainDataTable.NewRow();
                    for (int i = 0; i < _mainDataTable.Columns.Count; i++)
                    {
                        if (row.Cell(i + 1).IsEmpty())
                        {
                            dr[i] = DBNull.Value;
                        }
                        else
                        {
                            string cellValue = row.Cell(i + 1).GetString().Trim();
                            // Точку на запятую будем менять потом, при обработке данных
                            // так как во время импорта мы еще не знаем, какой столбец является Value или Quantity.
                            dr[i] = cellValue;
                        }
                    }
                    _mainDataTable.Rows.Add(dr);
                }

                dgColumnConfig.ItemsSource = _columnConfigTable.DefaultView;
                ProcessDataLogic();

                // Подписываемся на изменения данных в DataTable для обновления цветов и логики "радиокнопок"
                _columnConfigTable.ColumnChanged -= OnColumnConfigChanged;
                _columnConfigTable.ColumnChanged += OnColumnConfigChanged;

                DataLoaded?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex) { MessageBox.Show("Ошибка загрузки: " + ex.Message); }
        }

        private void ProcessDataLogic()
        {
            if (_mainDataTable == null) return;
            DataTable dt = _mainDataTable.Copy();
            _autoFilledKeys.Clear();

            var keys = GetCheckedCols("IsKey");
            var vals = GetCheckedCols("IsVal");

            var qtys = GetCheckedCols("IsQty");
            var leftAngles = GetCheckedCols("IsLeftAngle");
            var rightAngles = GetCheckedCols("IsRightAngle");

            // Меняем точку на запятую в столбцах Value и Qty
            foreach (var valCol in vals)
            {
                foreach (DataRow r in dt.Rows)
                {
                    if (r[valCol] != DBNull.Value)
                    {
                        string strVal = r[valCol].ToString();
                        if (strVal.Contains('.'))
                            r[valCol] = strVal.Replace('.', ',');
                    }
                }
            }

            foreach (var qtyCol in qtys)
            {
                foreach (DataRow r in dt.Rows)
                {
                    if (r[qtyCol] != DBNull.Value)
                    {
                        string strQty = r[qtyCol].ToString();
                        if (strQty.Contains('.'))
                            r[qtyCol] = strQty.Replace('.', ',');
                    }
                }
            }

            foreach (var angCol in leftAngles.Concat(rightAngles))
            {
                foreach (DataRow r in dt.Rows)
                {
                    if (r[angCol] != DBNull.Value)
                    {
                        string strAng = r[angCol].ToString();
                        if (strAng.Contains('.'))
                            r[angCol] = strAng.Replace('.', ',');
                    }
                }
            }

            if (vals.Any())
            {
                for (int i = dt.Rows.Count - 1; i >= 0; i--)
                {
                    if (vals.All(v => dt.Rows[i][v] == DBNull.Value || string.IsNullOrWhiteSpace(dt.Rows[i][v].ToString())))
                        dt.Rows.RemoveAt(i);
                }
            }

            foreach (string k in keys)
            {
                for (int r = 1; r < dt.Rows.Count; r++)
                {
                    if (string.IsNullOrWhiteSpace(dt.Rows[r][k]?.ToString()))
                    {
                        dt.Rows[r][k] = dt.Rows[r - 1][k];
                        _autoFilledKeys.Add((r, k));
                    }
                }
            }

            dgInput.ItemsSource = dt.DefaultView;
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
            if (_mainDataTable == null) return;
            var row = e.Row;
            int rowIndex = e.Row.GetIndex();

            bool isInvalid = _invalidRows.Contains(rowIndex);
            
            if (isInvalid)
            {
                row.Background = Brushes.LightCoral;
                row.Foreground = Brushes.White;
                return;
            }

            foreach (var key in _autoFilledKeys)
            {
                if (key.row == rowIndex)
                {
                    row.Foreground = Brushes.Red;
                    break;
                }
            }
        }

        private void OnDgColumnConfigLoaded(object sender, RoutedEventArgs e)
        {
            // После загрузки DataGrid обновляем цвета ячеек
            dgInput.Dispatcher.BeginInvoke(new Action(() =>
            {
                RefreshColumnsVisuals();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void OnDgInputAutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            // Устанавливаем стиль ячеек для автоматически генерируемого столбца
            var keys = GetCheckedCols("IsKey");
            var names = GetCheckedCols("IsName");
            var vals = GetCheckedCols("IsVal");
            var qtys = GetCheckedCols("IsQty");
            var leftAngles = GetCheckedCols("IsLeftAngle");
            var rightAngles = GetCheckedCols("IsRightAngle");

            string colName = e.PropertyName;
            Brush bg;
            if (keys.Contains(colName))
                bg = Brushes.LightGreen;
            else if (names.Contains(colName))
                bg = Brushes.LightPink;
            else if (vals.Contains(colName))
                bg = Brushes.LightYellow;
            else if (qtys.Contains(colName))
                bg = Brushes.LightCyan;
            else if (leftAngles.Contains(colName) || rightAngles.Contains(colName))
                bg = Brushes.LightGray;
            else
                bg = Brushes.White;

            var cellStyle = new Style(typeof(DataGridCell));
            cellStyle.Setters.Add(new Setter(DataGridCell.BackgroundProperty, bg));
            e.Column.CellStyle = cellStyle;

            // Устанавливаем стиль заголовка
            var headerStyle = new Style(typeof(DataGridColumnHeader));
            headerStyle.Setters.Add(new Setter(DataGridColumnHeader.BackgroundProperty, bg));
            e.Column.HeaderStyle = headerStyle;
        }

        private void OnDgInputLoaded(object sender, RoutedEventArgs e)
        {
            // После загрузки dgInput обновляем цвета столбцов
            dgInput.Dispatcher.BeginInvoke(new Action(() =>
            {
                RefreshColumnsVisuals();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void RefreshColumnsVisuals()
        {
            if (_columnConfigTable == null || dgInput.Columns.Count == 0) return;

            var keys = GetCheckedCols("IsKey");
            var names = GetCheckedCols("IsName");
            var vals = GetCheckedCols("IsVal");
            var qtys = GetCheckedCols("IsQty");
            var leftAngles = GetCheckedCols("IsLeftAngle");
            var rightAngles = GetCheckedCols("IsRightAngle");

            // Обновляем цвета ячеек чекбоксов в dgColumnConfig
            dgColumnConfig.UpdateLayout();
            for (int i = 0; i < _columnConfigTable.Rows.Count; i++)
            {
                DataRowView rowView = _columnConfigTable.DefaultView[i];
                var row = dgColumnConfig.ItemContainerGenerator.ContainerFromIndex(i) as DataGridRow;
                if (row == null) continue;

                bool isKey = rowView["IsKey"] != DBNull.Value && (bool)rowView["IsKey"];
                bool isName = rowView["IsName"] != DBNull.Value && (bool)rowView["IsName"];
                bool isVal = rowView["IsVal"] != DBNull.Value && (bool)rowView["IsVal"];
                bool isQty = rowView["IsQty"] != DBNull.Value && (bool)rowView["IsQty"];
                bool isLeftAngle = rowView.DataView.Table.Columns.Contains("IsLeftAngle") && rowView["IsLeftAngle"] != DBNull.Value && (bool)rowView["IsLeftAngle"];
                bool isRightAngle = rowView.DataView.Table.Columns.Contains("IsRightAngle") && rowView["IsRightAngle"] != DBNull.Value && (bool)rowView["IsRightAngle"];

                SetCellBackground(dgColumnConfig, row, 1, isKey ? Brushes.LightGreen : Brushes.White);
                SetCellBackground(dgColumnConfig, row, 2, isName ? Brushes.LightPink : Brushes.White);
                SetCellBackground(dgColumnConfig, row, 3, isVal ? Brushes.LightYellow : Brushes.White);
                SetCellBackground(dgColumnConfig, row, 4, isQty ? Brushes.LightCyan : Brushes.White);
                SetCellBackground(dgColumnConfig, row, 5, isLeftAngle ? Brushes.LightGray : Brushes.White);
                SetCellBackground(dgColumnConfig, row, 6, isRightAngle ? Brushes.LightGray : Brushes.White);
            }

            // Красим столбцы в dgInput через CellStyle (аналог DefaultCellStyle.BackColor в WinForms)
            foreach (var col in dgInput.Columns)
            {
                string colName = col.Header?.ToString();
                if (string.IsNullOrEmpty(colName)) continue;

                Brush bg;
                if (keys.Contains(colName))
                    bg = Brushes.LightGreen;
                else if (names.Contains(colName))
                    bg = Brushes.LightPink;
                else if (vals.Contains(colName))
                    bg = Brushes.LightYellow;
                else if (qtys.Contains(colName))
                    bg = Brushes.LightCyan;
                else if (leftAngles.Contains(colName) || rightAngles.Contains(colName))
                    bg = Brushes.LightGray;
                else
                    bg = Brushes.White;

                // Создаём стиль ячеек для столбца
                var cellStyle = new Style(typeof(DataGridCell));
                cellStyle.Setters.Add(new Setter(DataGridCell.BackgroundProperty, bg));
                
                // Принудительно обновляем стиль (сначала null, потом новый)
                col.CellStyle = null;
                col.CellStyle = cellStyle;

                // Обновляем цвет заголовка
                col.HeaderStyle = CreateColumnHeaderStyle(bg);
            }

            // Принудительно обновляем визуальное дерево DataGrid
            dgInput.UpdateLayout();
            dgInput.InvalidateVisual();
            dgInput.Items.Refresh();
        }

        private void SetCellBackground(DataGrid dataGrid, DataGridRow row, int columnIndex, Brush background)
        {
            if (row == null || columnIndex < 0 || columnIndex >= dataGrid.Columns.Count) return;

            // Принудительно обновляем layout строки
            row.UpdateLayout();

            var presenter = FindVisualChild<DataGridCellsPresenter>(row);
            if (presenter == null) return;

            // Обновляем layout презентера
            presenter.UpdateLayout();

            var cell = presenter.ItemContainerGenerator.ContainerFromIndex(columnIndex) as DataGridCell;
            
            // Если ячейка не найдена, пробуем найти через перебор всех ячеек
            if (cell == null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(presenter); i++)
                {
                    var child = VisualTreeHelper.GetChild(presenter, i);
                    if (child is DataGridCell dataGridCell)
                    {
                        // Проверяем, соответствует ли ячейка нужному столбцу
                        if (dataGridCell.Column == dataGrid.Columns[columnIndex])
                        {
                            cell = dataGridCell;
                            break;
                        }
                    }
                }
            }

            if (cell != null)
            {
                cell.Background = background;
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

        private Style CreateColumnHeaderStyle(Brush background)
        {
            var style = new Style(typeof(DataGridColumnHeader));
            style.Setters.Add(new Setter(DataGridColumnHeader.BackgroundProperty, background));
            return style;
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

        /// <summary>
        /// Восстанавливает состояние контрола на основе переданных параметров (например, при переключении между вкладками).
        /// </summary>
        /// <param name="mainDataTable">Таблица с данными.</param>
        /// <param name="defaultBarLength">Длина хлыста по умолчанию.</param>
        /// <param name="defaultPreset">Пресет по умолчанию.</param>
        /// <param name="stockLengths">Список доступных хлыстов.</param>
        /// <param name="presets">Список доступных пресетов.</param>
        /// <param name="columnConfig">Таблица с конфигурацией столбцов.</param>
        public void LoadSettings(DataTable mainDataTable, double defaultBarLength, PresetModel defaultPreset,
            List<StockLengthModel> stockLengths, List<PresetModel> presets, DataTable columnConfig)
        {
            _mainDataTable = mainDataTable;
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
                dgColumnConfig.ItemsSource = _columnConfigTable.DefaultView;

                // Подписываемся на изменения данных в DataTable для обновления цветов и логики "радиокнопок"
                _columnConfigTable.ColumnChanged -= OnColumnConfigChanged;
                _columnConfigTable.ColumnChanged += OnColumnConfigChanged;
            }
            ProcessDataLogic();
        }

        private bool _isHandlingColumnChange = false;
        private bool _isUpdatePending = false;

        private void OnColumnConfigChanged(object sender, DataColumnChangeEventArgs e)
        {
            if (_isHandlingColumnChange) return;

            string[] radioCols = { "IsKey", "IsName", "IsVal", "IsQty", "IsLeftAngle", "IsRightAngle" };

            if (radioCols.Contains(e.Column.ColumnName))
            {
                if (e.ProposedValue is bool isChecked && isChecked)
                {
                    _isHandlingColumnChange = true;
                    try
                    {
                        foreach (var col in radioCols)
                        {
                            if (col != e.Column.ColumnName && e.Row.Table.Columns.Contains(col))
                            {
                                e.Row[col] = false;
                            }
                        }
                    }
                    finally
                    {
                        _isHandlingColumnChange = false;
                    }
                }

                if (!_isUpdatePending)
                {
                    _isUpdatePending = true;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        _isUpdatePending = false;
                        RefreshColumnsVisuals();
                        ProcessDataLogic();
                        SettingsChanged?.Invoke(this, EventArgs.Empty);
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
        }

        /// <summary>
        /// Валидация данных перед переходом на вкладку группировки
        /// </summary>
        public bool ValidateData()
        {
            if (_mainDataTable == null) return true;

            var vals = GetCheckedCols("IsVal");
            var qtys = GetCheckedCols("IsQty");
            
            if (!vals.Any()) return true;

            _invalidRows.Clear();
            
            DataTable errorsTable = _mainDataTable.Clone();
            errorsTable.Columns.Add("Описание ошибки", typeof(string));

            for (int i = 0; i < _mainDataTable.Rows.Count; i++)
            {
                var row = _mainDataTable.Rows[i];
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
                    for (int c = 0; c < _mainDataTable.Columns.Count; c++)
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

        /// <summary>
        /// Обновить настройки артикулов из GroupingControl
        /// </summary>

        public void UpdateArticleSettings(Dictionary<string, ArticleSettings> articleSettings)
        {
            // Здесь можно добавить логику обновления настроек артикулов
            // Например, обновить _defaultBarLength и _defaultPreset на основе данных из GroupingControl
            
            if (articleSettings != null && articleSettings.Count > 0)
            {
                // Берем настройки первого артикула как дефолтные (или можно сделать логику выбора)
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
