using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        private CancellationTokenSource _loadCts;
        private byte[] _fileBytes;
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
                _loadCts?.Cancel();
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

        private async void OnOpenFileClick(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog { Filter = "Excel|*.xlsx" };
            if (ofd.ShowDialog() != true) return;

            if (IsFileLocked(ofd.FileName))
            {
                MessageBox.Show($"Файл \"{ofd.FileName}\" занят другим приложением.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Отменяем возможную предыдущую фоновую загрузку данных
            _loadCts?.Cancel();

            _currentFilePath = ofd.FileName;
            _isLoading = true;
            cbSheetSelector.Items.Clear();
            _columnConfigTable = null;
            _invalidRows.Clear();
            _fileBytes = null;

            loadingIndicator.Visibility = Visibility.Visible;
            loadingText.Text = "Открытие файла…";

            string filePath = _currentFilePath;
            List<string> sheetNames = null;

            try
            {
                await Task.Run(() =>
                {
                    // 1) Читаем файл в оперативную память один раз (диск/сеть — самый долгий этап)
                    var bytes = System.IO.File.ReadAllBytes(filePath);
                    _fileBytes = bytes;

                    // 2-3) Распаковываем архив в памяти и читаем список листов из xl/workbook.xml
                    //      (без полного разбора книги через ClosedXML — почти мгновенно)
                    sheetNames = XlsxFastReader.GetSheetNames(bytes);
                });
            }
            catch (Exception ex)
            {
                loadingIndicator.Visibility = Visibility.Collapsed;
                MessageBox.Show($"Не удалось загрузить файл Excel: {ex.Message}\n\nФайл может содержать неисправные сводные таблицы.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                _currentFilePath = null;
                _fileBytes = null;
                _isLoading = false;
                return;
            }

            foreach (var name in sheetNames)
                cbSheetSelector.Items.Add(name);

            loadingIndicator.Visibility = Visibility.Collapsed;
            _isLoading = false;

            if (cbSheetSelector.Items.Count > 0)
            {
                int raskroyIdx = -1;
                for (int i = 0; i < cbSheetSelector.Items.Count; i++)
                {
                    if (cbSheetSelector.Items[i].ToString() == "Раскрой")
                    {
                        raskroyIdx = i;
                        break;
                    }
                }
                cbSheetSelector.SelectedIndex = raskroyIdx >= 0 ? raskroyIdx : 0;
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

        /// <summary>
        /// Вычисляет количество строк, помещающихся в видимую область таблицы (одна "страница").
        /// </summary>
        private int CalculatePageSize()
        {
            const double rowHeight = 22.0;
            double height = dgInput.ActualHeight;
            if (height <= 0) height = dgInput.RenderSize.Height;
            int size = height > 0 ? (int)(height / rowHeight) : 0;
            return Math.Max(size, 50);
        }

        /// <summary>
        /// Создаёт конфигурацию столбцов на основе заголовков (если ещё не создана)
        /// и выполняет автоназначение ролей.
        /// </summary>
        private void EnsureColumnConfig(List<string> headers)
        {
            if (_columnConfigTable != null) return;

            _columnConfigTable = new DataTable();
            _columnConfigTable.Columns.Add("ColName", typeof(string));
            _columnConfigTable.Columns.Add("IsKey", typeof(bool));
            _columnConfigTable.Columns.Add("IsName", typeof(bool));
            _columnConfigTable.Columns.Add("IsVal", typeof(bool));
            _columnConfigTable.Columns.Add("IsQty", typeof(bool));
            _columnConfigTable.Columns.Add("IsLeftAngle", typeof(bool));
            _columnConfigTable.Columns.Add("IsRightAngle", typeof(bool));
            _columnConfigTable.Columns.Add("IsColor", typeof(bool));

            foreach (var h in headers)
                _columnConfigTable.Rows.Add(h, false, false, false, false, false, false, false);

            AutoAssignColumnRoles(_columnConfigTable);
        }

        /// <summary>
        /// Строит DataTable из прочитанных в память заголовков и строк.
        /// </summary>
        private static DataTable BuildDataTable(List<string> headers, List<string[]> rows)
        {
            var dt = new DataTable();
            foreach (var h in headers)
                dt.Columns.Add(h);

            foreach (var cells in rows)
            {
                var dr = dt.NewRow();
                for (int i = 0; i < headers.Count; i++)
                    dr[i] = cells[i] == null ? (object)DBNull.Value : cells[i];
                dt.Rows.Add(dr);
            }
            return dt;
        }

        /// <summary>
        /// Асинхронно загружает данные выбранного листа: файл читается в память один раз в фоне,
        /// первая "страница" строк показывается сразу (чтобы можно было назначать столбцы),
        /// остальные строки дочитываются в фоне, затем выполняется полная обработка.
        /// </summary>
        private async void LoadDataFromSheet(string sheetName)
        {
            if (string.IsNullOrEmpty(_currentFilePath) || string.IsNullOrEmpty(sheetName)) return;

            // Отменяем предыдущую (возможно ещё идущую) загрузку
            _loadCts?.Cancel();
            var cts = new CancellationTokenSource();
            _loadCts = cts;
            var token = cts.Token;

            string filePath = _currentFilePath;
            byte[] fileBytes = _fileBytes;
            int pageSize = CalculatePageSize();

            loadingIndicator.Visibility = Visibility.Visible;
            loadingText.Text = "Загрузка…";
            // Интерфейс с данными выключен до заполнения видимой части таблицы
            dgInput.IsEnabled = false;

            try
            {
                // === Шаги 2-3: распаковываем архив в памяти и читаем заголовки напрямую ===
                // Заголовки нужны, чтобы читать строки по индексу; на экран пока не выводим —
                // сначала заполним видимую часть таблицы данными.
                List<string> headers = null;
                if (fileBytes != null)
                {
                    await Task.Run(() => headers = XlsxFastReader.GetHeaders(fileBytes, sheetName), token);
                }

                token.ThrowIfCancellationRequested();

                if (headers != null && headers.Count > 0)
                    EnsureColumnConfig(headers);

                // === Шаг 4: открываем данные как Excel (из памяти) и дочитываем строки в фоне ===
                await Task.Run(() =>
                {
                    List<string> hdrs = headers;
                    var allRows = new List<string[]>();

                    // Открываем из оперативной памяти (если байты закешированы), иначе с диска
                    var ms = fileBytes != null
                        ? new System.IO.MemoryStream(fileBytes, 0, fileBytes.Length, writable: false)
                        : null;
                    using (ms)
                    using (var workbook = ms != null ? new XLWorkbook(ms) : new XLWorkbook(filePath))
                    {
                        var ws = workbook.Worksheet(sheetName);
                        var headerRow = ws.FirstRowUsed();
                        if (headerRow == null)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                dgInput.IsEnabled = true;
                                loadingIndicator.Visibility = Visibility.Collapsed;
                                MessageBox.Show("Лист пуст!");
                            });
                            return;
                        }

                        // Если быстрый разбор не дал заголовков — берём их из ClosedXML
                        if (hdrs == null || hdrs.Count == 0)
                            hdrs = headerRow.CellsUsed().Select(c => c.GetString()).ToList();

                        int colCount = hdrs.Count;

                        bool partialShown = false;
                        foreach (var row in ws.RowsUsed().Skip(1))
                        {
                            token.ThrowIfCancellationRequested();

                            var cells = new string[colCount];
                            for (int i = 0; i < colCount; i++)
                            {
                                var cell = row.Cell(i + 1);
                                cells[i] = cell.IsEmpty() ? null : cell.GetString().Trim();
                            }
                            allRows.Add(cells);

                            // Фаза 1: как только прочитана первая страница — показываем её
                            if (!partialShown && allRows.Count >= pageSize)
                            {
                                partialShown = true;
                                var pageRows = allRows.GetRange(0, pageSize);
                                var pageHeaders = hdrs;
                                Dispatcher.Invoke(() =>
                                {
                                    if (token.IsCancellationRequested) return;
                                    EnsureColumnConfig(pageHeaders);
                                    var partial = BuildDataTable(pageHeaders, pageRows);
                                    // Видимая часть таблицы заполнена данными —
                                    // теперь включаем интерфейс (роли столбцов и т.д.)
                                    _dataStore.Initialize(partial, _columnConfigTable, filePath);
                                    dgInput.IsEnabled = true;
                                });
                            }
                        }
                    } // workbook освобождается здесь — память Excel высвобождается

                    token.ThrowIfCancellationRequested();

                    // === Фаза 2: полная обработка (данные уже в памяти) ===
                    var headersFinal = hdrs;
                    var rowsFinal = allRows;
                    Dispatcher.Invoke(() =>
                    {
                        if (token.IsCancellationRequested) return;
                        EnsureColumnConfig(headersFinal);
                        var fullTable = BuildDataTable(headersFinal, rowsFinal);
                        // Используется актуальная _columnConfigTable (с учётом ролей,
                        // назначенных пользователем во время фоновой загрузки)
                        _dataStore.Initialize(fullTable, _columnConfigTable, filePath);
                        DataLoaded?.Invoke(this, EventArgs.Empty);
                        dgInput.IsEnabled = true;
                        loadingIndicator.Visibility = Visibility.Collapsed;
                    });

                    // Высвобождаем память промежуточного буфера
                    allRows.Clear();
                }, token);
            }
            catch (OperationCanceledException)
            {
                // Загрузка отменена (выбран другой лист или контрол выгружен) — игнорируем
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    dgInput.IsEnabled = true;
                    loadingIndicator.Visibility = Visibility.Collapsed;
                    MessageBox.Show("Ошибка загрузки: " + ex.Message);
                });
            }
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

        /// <summary>
        /// Автоматически назначает роли столбцам на основе их заголовков.
        /// Правила: "Артикул" → IsKey, "Длина" → IsVal, "Наименование" → IsName,
        /// "Цвет" → IsColor, содержит "лев" и "угол" → IsLeftAngle, содержит "прав" и "угол" → IsRightAngle.
        /// Для взаимоисключающих ролей назначается только первому совпавшему столбцу.
        /// </summary>
        private void AutoAssignColumnRoles(DataTable columnConfig)
        {
            if (columnConfig == null) return;

            // Отслеживаем, какие роли уже назначены (кроме IsKey, который может быть у нескольких столбцов)
            var assignedRoles = new HashSet<string>();

            foreach (DataRow row in columnConfig.Rows)
            {
                string colName = row["ColName"]?.ToString();
                if (string.IsNullOrEmpty(colName)) continue;

                string lower = colName.ToLowerInvariant();

                // Артикул → IsKey
                if (lower.Contains("артикул"))
                {
                    row["IsKey"] = true;
                    // IsKey не добавляем в assignedRoles — может быть у нескольких столбцов
                }

                // Наименование → IsName
                if (lower.Contains("наименование") && !assignedRoles.Contains("IsName"))
                {
                    row["IsName"] = true;
                    assignedRoles.Add("IsName");
                }

                // Длина → IsVal
                if (lower.Contains("длина") && !assignedRoles.Contains("IsVal"))
                {
                    row["IsVal"] = true;
                    assignedRoles.Add("IsVal");
                }

                // Цвет → IsColor
                if (lower.Contains("цвет") && !assignedRoles.Contains("IsColor"))
                {
                    row["IsColor"] = true;
                    assignedRoles.Add("IsColor");
                }

                // Левый угол: содержит "лев" и "угол"
                if (lower.Contains("лев") && lower.Contains("угол") && !assignedRoles.Contains("IsLeftAngle"))
                {
                    row["IsLeftAngle"] = true;
                    assignedRoles.Add("IsLeftAngle");
                }

                // Правый угол: содержит "прав" и "угол"
                if (lower.Contains("прав") && lower.Contains("угол") && !assignedRoles.Contains("IsRightAngle"))
                {
                    row["IsRightAngle"] = true;
                    assignedRoles.Add("IsRightAngle");
                }
            }
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
                { "IsLeftAngle", Brushes.LightBlue },
                { "IsRightAngle", Brushes.Lavender },
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