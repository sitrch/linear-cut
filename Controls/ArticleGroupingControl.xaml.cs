using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using LinearCutWpf.Models;

namespace LinearCutWpf.Controls
{
    /// <summary>
    /// Контрол для отображения и настройки группировки деталей конкретного артикула.
    /// Позволяет задать хлыст, пресет обрезки, параметры ручного раскроя, а также просмотреть детализацию.
    /// </summary>
    public partial class ArticleGroupingControl : UserControl
    {
        /// <summary>
        /// Свойство зависимости для ширины левой панели.
        /// </summary>
        public static readonly DependencyProperty LeftPanelWidthProperty =
            DependencyProperty.Register("LeftPanelWidth", typeof(GridLength), typeof(ArticleGroupingControl),
                new PropertyMetadata(new GridLength(420)));

        /// <summary>
        /// Ширина левой панели.
        /// </summary>
        public GridLength LeftPanelWidth
        {
            get => (GridLength)GetValue(LeftPanelWidthProperty);
            set => SetValue(LeftPanelWidthProperty, value);
        }

        private string _articleName;
        private ArticleSettings _settings;
        private double _defaultBarLength;
        private PresetModel _defaultPreset;
        private ObservableCollection<StockLengthModel> _stockLengths;
        private List<PresetModel> _presets;
        private DataTable _gridInput;
        private Func<string, List<string>> _getCheckedCols;
        private List<string> _keys;
        private List<string> _vals;
        private string _qty;
        private DataRow[] _articleRows;
        private DataView _articleView;

        /// <summary>Строки исходных данных для данного артикула (из вкладки Группировка).</summary>
        public DataRow[] GetArticleRows()
        {
            return _articleRows ?? _articleView?.Cast<DataRowView>().Select(dvr => dvr.Row).ToArray();
        }

        private ManualCutControl _manualCutControl;

        /// <summary>
        /// Контрол ручного раскроя, связанный с данным артикулом.
        /// </summary>
        public ManualCutControl ManualCutControl => _manualCutControl;
        
        /// <summary>
        /// Таблица детализации артикула (доступные размеры и количество).
        /// </summary>
        public DataGrid GridDetails => gridDetails;

        /// <summary>
        /// Событие при изменении настроек (хлыст/пресет) — для перекраски таба
        /// </summary>
        public event Action<string> SettingsChanged;

        /// <summary>
        /// Инициализирует новый экземпляр класса <see cref="ArticleGroupingControl"/>.
        /// </summary>
        public ArticleGroupingControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Инициализация контрола данными артикула (через DataRow[])
        /// </summary>
        public void Initialize(string articleName, ArticleSettings settings,
            double defaultBarLength, PresetModel defaultPreset,
            ObservableCollection<StockLengthModel> stockLengths, List<PresetModel> presets,
            DataTable gridInput, Func<string, List<string>> getCheckedCols,
            List<string> keys, List<string> vals, string qty,
            DataRow[] articleRows)
        {
            _articleName = articleName;
            _settings = settings;
            _defaultBarLength = defaultBarLength;
            _defaultPreset = defaultPreset;
            _stockLengths = stockLengths;
            _presets = presets;
            _gridInput = gridInput;
            _getCheckedCols = getCheckedCols;
            _keys = keys;
            _vals = vals;
            _qty = qty;
            _articleRows = articleRows;
            _articleView = null;

            BuildLeftPanel();
            BuildRightPanel(_articleRows);
        }

        /// <summary>
        /// Инициализация контрола данными артикула (через DataView)
        /// </summary>
        public void Initialize(string articleName, ArticleSettings settings,
            double defaultBarLength, PresetModel defaultPreset,
            ObservableCollection<StockLengthModel> stockLengths, List<PresetModel> presets,
            DataTable gridInput, Func<string, List<string>> getCheckedCols,
            List<string> keys, List<string> vals, string qty,
            DataView articleView)
        {
            _articleName = articleName;
            _settings = settings;
            _defaultBarLength = defaultBarLength;
            _defaultPreset = defaultPreset;
            _stockLengths = stockLengths;
            _presets = presets;
            _gridInput = gridInput;
            _getCheckedCols = getCheckedCols;
            _keys = keys;
            _vals = vals;
            _qty = qty;
            _articleView = articleView;
            _articleRows = null;

            // Подписываемся на изменения DataView
            if (_articleView != null)
            {
                ((IBindingList)_articleView).ListChanged += OnArticleViewListChanged;
            }

            BuildLeftPanel();
            BuildRightPanelFromView();

            // Отписка при выгрузке
            this.Unloaded += (s, e) =>
            {
                if (_articleView != null)
                {
                    ((IBindingList)_articleView).ListChanged -= OnArticleViewListChanged;
                }
            };
        }

        /// <summary>
        /// Обработчик изменения DataView (для реактивного обновления)
        /// </summary>
        private void OnArticleViewListChanged(object sender, ListChangedEventArgs e)
        {
            if (e.ListChangedType == ListChangedType.Reset || 
                e.ListChangedType == ListChangedType.ItemAdded || 
                e.ListChangedType == ListChangedType.ItemDeleted ||
                e.ListChangedType == ListChangedType.ItemChanged)
            {
                // Пересчитываем правую панель при изменении данных
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    BuildRightPanelFromView();
                    UpdateInfoBlock();
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        /// <summary>
        /// Строит левую панель настроек: выпадающие списки хлыста и пресета, контрол ручного раскроя.
        /// </summary>
        private void BuildLeftPanel()
        {
            // Информация об артикуле
            var articleNameTxt = FindName("articleNameText") as TextBlock;
            if (articleNameTxt != null)
            {
                string displayName = BuildArticleDisplayName();
                articleNameTxt.Text = displayName;
            }
            UpdateArticleColorIndicator();

            // Хлыст
            cbBarLength.Items.Clear();
            foreach (var sl in _stockLengths)
                cbBarLength.Items.Add(sl.Length.ToString());

            bool isDefaultBar = !_settings.BarLength.HasValue;
            if (_settings.BarLength.HasValue)
                cbBarLength.SelectedItem = _settings.BarLength.Value.ToString();
            else
                cbBarLength.SelectedItem = _defaultBarLength.ToString();

            cbBarLength.SelectionChanged += OnBarLengthChanged;

            // Пресет
            cbPreset.Items.Clear();
            string defaultPresetDisplay = _defaultPreset != null
                ? $"{_defaultPreset.Name} {_defaultPreset.TrimStart}-{_defaultPreset.CutWidth}-{_defaultPreset.TrimEnd} (по умолчанию)"
                : "(По умолчанию)";
            cbPreset.Items.Add(defaultPresetDisplay);

            foreach (var p in _presets)
                cbPreset.Items.Add($"{p.Name} {p.TrimStart}-{p.CutWidth}-{p.TrimEnd}");

            if (_settings.Preset != null)
            {
                var idx = _presets.IndexOf(_settings.Preset);
                cbPreset.SelectedIndex = idx >= 0 ? idx + 1 : 0;
            }
            else
                cbPreset.SelectedIndex = 0;

            cbPreset.SelectionChanged += OnPresetChanged;

            // Чекбокс ручного раскроя
            bool hasManualData = _settings.ManualCuts.Any(r => 
                r.BarLength.HasValue || 
                !string.IsNullOrEmpty(r.Size1) || 
                !string.IsNullOrEmpty(r.Size2) || 
                !string.IsNullOrEmpty(r.Size3) || 
                !string.IsNullOrEmpty(r.Size4));
            chkManual.IsChecked = hasManualData;

            _manualCutControl = new ManualCutControl();
            
            // Если коллекция вдруг null (хотя инициализируется в модели), создадим её
            if (_settings.ManualCuts == null)
                _settings.ManualCuts = new ObservableCollection<ManualCutRow>();
                
            var manualCuts = _settings.ManualCuts;
            
            var preset = GetEffectivePreset();

            // Сбор деталей для доступных размеров
            var itemSizes = new List<string>();
            var itemQuantities = new List<int>();

            // Определяем источник данных: DataRow[] или DataView
            IEnumerable<DataRow> rowsToProcess = null;
            
            if (_articleRows != null)
            {
                rowsToProcess = _articleRows;
            }
            else if (_articleView != null)
            {
                var rows = new List<DataRow>();
                foreach (DataRowView rowView in _articleView)
                {
                    rows.Add(rowView.Row);
                }
                rowsToProcess = rows;
            }

            if (rowsToProcess != null && _vals.Any())
            {
                foreach (DataRow row in rowsToProcess)
                {
                    var valObj = row[_vals.First()];
                    if (valObj == DBNull.Value || string.IsNullOrWhiteSpace(valObj?.ToString())) continue;
                    
                    string valStr = valObj.ToString().Replace(" ", "").Replace("\u00A0", "").Replace('.', ',');
                    if (double.TryParse(valStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out double l) && l > 0)
                    {
                        itemSizes.Add(l.ToString());
                        int c = 0;
                        if (!string.IsNullOrEmpty(_qty))
                        {
                            var qtyObj = row[_qty];
                            if (qtyObj != DBNull.Value && !string.IsNullOrWhiteSpace(qtyObj?.ToString()))
                            {
                                string qtyStr = qtyObj.ToString().Replace(" ", "").Replace("\u00A0", "").Replace('.', ',');
                                if (double.TryParse(qtyStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out double q) && q > 0)
                                    c = (int)q;
                            }
                        }
                        itemQuantities.Add(c);
                    }
                }
            }

            _manualCutControl.ViewModel.Initialize(manualCuts,
                GetEffectiveBarLength(), preset,
                new List<string>(), _stockLengths.Select(s => s.Length).ToList(),
                itemSizes, itemQuantities);

            // Подписываемся на изменения коллекции ручного раскроя
            manualCuts.CollectionChanged += OnManualCutsCollectionChanged;

            // Подписываемся на PropertyChanged существующих элементов
            foreach (var row in manualCuts)
                row.PropertyChanged += OnManualCutRowPropertyChanged;
            
            // Отписка при выгрузке (предотвращение утечки памяти)
            this.Unloaded += (s, e) =>
            {
                manualCuts.CollectionChanged -= OnManualCutsCollectionChanged;
                foreach (var row in manualCuts)
                    row.PropertyChanged -= OnManualCutRowPropertyChanged;
            };

            manualCutFrame.Content = _manualCutControl;
            _manualCutControl.Visibility = hasManualData ? Visibility.Visible : Visibility.Collapsed;

            chkManual.Checked += (s, e) => { _manualCutControl.Visibility = Visibility.Visible; UpdateIndicators(); };
            chkManual.Unchecked += (s, e) => { _manualCutControl.Visibility = Visibility.Collapsed; UpdateIndicators(); };

            UpdateInfoBlock();
            UpdateErrorsPanel();
            UpdateIndicators();
        }

        /// <summary>
        /// Обработчик изменения коллекции ручного раскроя.
        /// </summary>
        private void OnManualCutsCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Подписываемся на PropertyChanged новых элементов
            if (e.NewItems != null)
            {
                foreach (ManualCutRow item in e.NewItems)
                    item.PropertyChanged += OnManualCutRowPropertyChanged;
            }
            if (e.OldItems != null)
            {
                foreach (ManualCutRow item in e.OldItems)
                    item.PropertyChanged -= OnManualCutRowPropertyChanged;
            }
            // Обновляем панель ошибок
            UpdateErrorsPanel();
            // Подсвечиваем таб после обновления данных
            SettingsChanged?.Invoke(_articleName);
            UpdateIndicators();
        }

        /// <summary>
        /// Строит правую панель детализации из DataView.
        /// </summary>
        private void BuildRightPanelFromView()
        {
            if (_articleView == null) return;

            var partsList = BuildPartsListFromRows(_articleView.Cast<DataRowView>().Select(dvr => dvr.Row));
            gridDetails.ItemsSource = partsList;
        }

        /// <summary>
        /// Строит правую панель детализации: группирует строки и выводит сводку по деталям артикула.
        /// </summary>
        private void BuildRightPanel(DataRow[] articleRows)
        {
            var partsList = BuildPartsListFromRows(articleRows);
            gridDetails.ItemsSource = partsList;
        }

        /// <summary>
        /// Общий метод: группирует строки по (ключ + длина + углы + цвет),
        /// суммирует количество, возвращает отсортированный список PartItem.
        /// </summary>
        private List<PartItem> BuildPartsListFromRows(IEnumerable<DataRow> rows)
        {
            // Колонки для группировки — все назначенные на вкладке «Данные»
            var groupingCols = new List<string>();
            groupingCols.AddRange(_keys);
            groupingCols.AddRange(_vals);
            groupingCols.AddRange(_getCheckedCols("IsLeftAngle"));
            groupingCols.AddRange(_getCheckedCols("IsRightAngle"));
            groupingCols.AddRange(_getCheckedCols("IsColor"));

            if (!groupingCols.Any()) return new List<PartItem>();

            var partsList = new List<PartItem>();

            foreach (var sg in rows.GroupBy(r => string.Join("\0", groupingCols.Select(c => r[c]?.ToString() ?? ""))))
            {
                var firstRow = sg.First();
                string article = DataHelper.GetArticleName(_keys.Select(k => firstRow[k]?.ToString()));

                double length = 0;
                if (_vals.Any() && firstRow[_vals.First()] != DBNull.Value)
                {
                    string valStr = firstRow[_vals.First()].ToString().Replace(" ", "").Replace("\u00A0", "").Replace('.', ',');
                    double.TryParse(valStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out length);
                }

                int qtySum = 0;
                if (!string.IsNullOrEmpty(_qty))
                {
                    foreach (var r in sg)
                    {
                        var qtyVal = r[_qty];
                        if (qtyVal != DBNull.Value && !string.IsNullOrWhiteSpace(qtyVal?.ToString()))
                        {
                            string qtyStr = qtyVal.ToString().Replace(" ", "").Replace("\u00A0", "").Replace('.', ',');
                            if (double.TryParse(qtyStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out double q) && q > 0)
                                qtySum += (int)q;
                        }
                    }
                }
                else
                {
                    qtySum = sg.Count();
                }

                if (qtySum <= 0) continue;

                // Углы реза
                var leftAngleCols = _getCheckedCols("IsLeftAngle");
                var rightAngleCols = _getCheckedCols("IsRightAngle");
                var colorCols = _getCheckedCols("IsColor");

                string leftAngle = leftAngleCols.Any() ? (firstRow[leftAngleCols.First()]?.ToString() ?? "") : "";
                string rightAngle = rightAngleCols.Any() ? (firstRow[rightAngleCols.First()]?.ToString() ?? "") : "";
                string color = colorCols.Any() ? (firstRow[colorCols.First()]?.ToString() ?? "") : "";

                partsList.Add(new PartItem
                {
                    Article = article,
                    Length = length,
                    Count = qtySum,
                    LeftAngle = leftAngle,
                    RightAngle = rightAngle,
                    Color = color
                });
            }

            return partsList.OrderByDescending(p => p.Length).ToList();
        }

        private void OnBarLengthChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbBarLength.SelectedItem == null) return;
            _settings.BarLength = double.Parse(cbBarLength.SelectedItem.ToString());
            SettingsChanged?.Invoke(_articleName);
            RefreshManualGridValidation();
        }

        private void OnPresetChanged(object sender, SelectionChangedEventArgs e)
        {
            var idx = cbPreset.SelectedIndex;
            _settings.Preset = idx > 0 && idx - 1 < _presets.Count ? _presets[idx - 1] : null;
            SettingsChanged?.Invoke(_articleName);
            RefreshManualGridValidation();
        }

        private double GetEffectiveBarLength()
        {
            return _settings.BarLength.HasValue ? _settings.BarLength.Value : _defaultBarLength;
        }

        private PresetModel GetEffectivePreset()
        {
            return _settings.Preset != null ? _settings.Preset : _defaultPreset;
        }

        private int GetEffectivePresetIndex()
        {
            if (_presets == null) return 0;
            if (_settings.Preset != null)
            {
                int idx = _presets.IndexOf(_settings.Preset);
                return idx >= 0 ? idx + 1 : 0;
            }
            return 0;
        }

        /// <summary>
        /// Проверяет наличие ошибок в ручном раскрое.
        /// </summary>
        /// <returns>True, если есть ошибки, иначе false.</returns>
        public bool HasManualErrors()
        {
            return _manualCutControl != null && _manualCutControl.ViewModel.HasErrorsText;
        }

        /// <summary>
        /// Обновляет параметры валидации в сетке ручного раскроя при изменении хлыста или пресета.
        /// </summary>
        private void RefreshManualGridValidation()
        {
            var preset = GetEffectivePreset();
            if (preset == null) return;

            if (_manualCutControl != null)
            {
                _manualCutControl.ViewModel.BarLength = GetEffectiveBarLength();
                _manualCutControl.ViewModel.CurrentPreset = GetEffectivePreset();
                UpdateErrorsPanel();
            }

            UpdateInfoBlock();
        }

        /// <summary>
        /// Обновляет панель отображения ошибок (видимость и текст).
        /// </summary>
        private void UpdateErrorsPanel()
        {
            if (_manualCutControl == null) return;

            var errorsBorder = FindName("errorsBorder") as Border;
            var txtDetailErrors = FindName("txtDetailErrors") as TextBlock;
            if (errorsBorder == null || txtDetailErrors == null) return;

            if (_manualCutControl.ViewModel.HasErrorsText)
            {
                txtDetailErrors.Text = _manualCutControl.ViewModel.ErrorsText;
                errorsBorder.Visibility = Visibility.Visible;
            }
            else
            {
                txtDetailErrors.Text = "";
                errorsBorder.Visibility = Visibility.Collapsed;
            }
        }

        private void OnManualCutRowPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ManualCutRow.BarLength) ||
                e.PropertyName == nameof(ManualCutRow.Size1) ||
                e.PropertyName == nameof(ManualCutRow.Size2) ||
                e.PropertyName == nameof(ManualCutRow.Size3) ||
                e.PropertyName == nameof(ManualCutRow.Size4) ||
                e.PropertyName == nameof(ManualCutRow.Count))
            {
                SettingsChanged?.Invoke(_articleName);
                UpdateErrorsPanel();
            }
        }

        /// <summary>
        /// Обновляет информационный блок (кол-во деталей, общая длина, настройки).
        /// </summary>
        private void UpdateInfoBlock()
        {
            if (infoLabel == null) return;

            var preset = GetEffectivePreset();
            if (preset == null) { infoLabel.Text = ""; return; }

            double barLen = GetEffectiveBarLength();

            var parts = new List<double>();
            double totalPartsLength = 0;

            // Определяем источник данных: DataRow[] или DataView
            IEnumerable<DataRow> rowsToProcess = null;
            
            if (_articleRows != null)
            {
                rowsToProcess = _articleRows;
            }
            else if (_articleView != null)
            {
                var rows = new List<DataRow>();
                foreach (DataRowView rowView in _articleView)
                {
                    rows.Add(rowView.Row);
                }
                rowsToProcess = rows;
            }

            if (rowsToProcess != null && _vals.Any())
            {
                foreach (DataRow row in rowsToProcess)
                {
                    var valObj = row[_vals.First()];
                    if (valObj == DBNull.Value || string.IsNullOrWhiteSpace(valObj?.ToString())) continue;
                    
                    string valStr = valObj.ToString().Replace(" ", "").Replace("\u00A0", "").Replace('.', ',');
                    if (!double.TryParse(valStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out double l) || l <= 0) continue;
                    
                    double lWithCut = l + preset.CutWidth;
                    int c = 0;
                    if (!string.IsNullOrEmpty(_qty))
                    {
                        var qtyObj = row[_qty];
                        if (qtyObj != DBNull.Value && !string.IsNullOrWhiteSpace(qtyObj?.ToString()))
                        {
                            string qtyStr = qtyObj.ToString().Replace(" ", "").Replace("\u00A0", "").Replace('.', ',');
                            if (double.TryParse(qtyStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out double q) && q > 0)
                                c = (int)q;
                        }
                    }
                    for (int i = 0; i < c; i++)
                    {
                        parts.Add(lWithCut);
                        totalPartsLength += l;
                    }
                }
            }

            int articlePartsCount = parts.Count;

            infoLabel.Text = $"Деталей: {articlePartsCount} шт., общая длина: {totalPartsLength / 1000:F2} м\n" +
                              $"Хлыст: {barLen} мм, пресет: {preset.Name}\n" +
                              $"Отступ: {preset.TrimStart}-{preset.TrimEnd}, рез: {preset.CutWidth} мм\n" +
                              $"\n(Результаты оптимизации — на вкладке «Результаты»)";

            UpdateIndicators();
        }

        /// <summary>
        /// Обновляет индикаторы (цветовые метки) настроек: хлыст, пресет, ручной раскрой.
        /// </summary>
        private void UpdateIndicators()
        {
            bool isDefaultBar = !_settings.BarLength.HasValue;
            bool isDefaultPreset = _settings.Preset == null;
            bool isDefaultManual = !_settings.ManualCuts.Any(r =>
                r.BarLength.HasValue ||
                !string.IsNullOrEmpty(r.Size1) ||
                !string.IsNullOrEmpty(r.Size2) ||
                !string.IsNullOrEmpty(r.Size3) ||
                !string.IsNullOrEmpty(r.Size4));

            var indicatorBar = FindName("indicatorBar") as TextBlock;
            var indicatorPreset = FindName("indicatorPreset") as TextBlock;
            var indicatorManual = FindName("indicatorManual") as TextBlock;

            if (indicatorBar != null)
            {
                indicatorBar.Text = isDefaultBar ? "✓" : "●";
                indicatorBar.Foreground = isDefaultBar ? Brushes.Green : Brushes.Red;
            }
            if (indicatorPreset != null)
            {
                indicatorPreset.Text = isDefaultPreset ? "✓" : "●";
                indicatorPreset.Foreground = isDefaultPreset ? Brushes.Green : Brushes.Red;
            }
            if (indicatorManual != null)
            {
                indicatorManual.Text = isDefaultManual ? "✓" : "●";
                indicatorManual.Foreground = isDefaultManual ? Brushes.Green : Brushes.Red;
            }

            UpdateArticleColorIndicator();
        }

        /// <summary>
        /// Формирует отображаемое имя артикула с данными из столбцов "Наименование" и "Цвет".
        /// </summary>
        private string BuildArticleDisplayName()
        {
            var parts = new List<string>();

            // Базовое имя (ключ артикула)
            if (!string.IsNullOrWhiteSpace(_articleName))
                parts.Add(_articleName);

            // Получаем значения столбцов "Наименование" и "Цвет" из первой строки данных
            var nameCols = _getCheckedCols?.Invoke("IsName") ?? new List<string>();
            var colorCols = _getCheckedCols?.Invoke("IsColor") ?? new List<string>();

            IEnumerable<DataRow> rowsToProcess = null;
            if (_articleView != null)
            {
                var rows = new List<DataRow>();
                foreach (DataRowView rv in _articleView)
                    rows.Add(rv.Row);
                rowsToProcess = rows;
            }
            else if (_articleRows != null)
            {
                rowsToProcess = _articleRows;
            }

            if (rowsToProcess != null)
            {
                var firstRow = rowsToProcess.FirstOrDefault();
                if (firstRow != null)
                {
                    foreach (var nameCol in nameCols)
                    {
                        if (firstRow.Table.Columns.Contains(nameCol))
                        {
                            var val = firstRow[nameCol];
                            if (val != DBNull.Value && !string.IsNullOrWhiteSpace(val?.ToString()))
                                parts.Add(val.ToString());
                        }
                    }

                    foreach (var colorCol in colorCols)
                    {
                        if (firstRow.Table.Columns.Contains(colorCol))
                        {
                            var val = firstRow[colorCol];
                            if (val != DBNull.Value && !string.IsNullOrWhiteSpace(val?.ToString()))
                                parts.Add(val.ToString());
                        }
                    }
                }
            }

            return string.Join(" | ", parts);
        }

        /// <summary>
        /// Обновляет цветовой индикатор артикула в левой панели.
        /// Зелёный — настройки по умолчанию, красный — есть индивидуальные настройки.
        /// </summary>
        private void UpdateArticleColorIndicator()
        {
            var indicator = FindName("articleColorIndicator") as Ellipse;
            if (indicator == null || _settings == null) return;

            bool hasCustom = _settings.HasCustomSettings(_defaultBarLength, _defaultPreset);
            indicator.Fill = hasCustom ? Brushes.Red : Brushes.Green;
        }

        /// <summary>
        /// Обработчик изменения выделенных ячеек в DataGrid.
        /// Рассчитывает сумму значений по каждому столбцу и отображает в строке статуса.
        /// </summary>
        private void OnSelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            var statusBar = FindName("statusBarText") as TextBlock;
            if (statusBar == null) return;

            var selectedCells = gridDetails.SelectedCells;
            if (selectedCells.Count == 0)
            {
                statusBar.Text = "";
                return;
            }

            // Группируем выделенные ячейки по столбцам
            var columnValues = new Dictionary<string, List<object>>();

            foreach (var cellInfo in selectedCells)
            {
                var column = cellInfo.Column;
                if (column == null) continue;

                string header = column.Header?.ToString() ?? "";
                var item = cellInfo.Item;
                if (item == null) continue;

                // Получаем значение через Binding
                var binding = column is DataGridTextColumn textCol ? textCol.Binding as System.Windows.Data.Binding : null;
                object value = null;

                if (binding != null && !string.IsNullOrEmpty(binding.Path?.Path))
                {
                    var prop = item.GetType().GetProperty(binding.Path.Path);
                    if (prop != null)
                    {
                        value = prop.GetValue(item);
                    }
                }

                if (!columnValues.ContainsKey(header))
                    columnValues[header] = new List<object>();

                columnValues[header].Add(value);
            }

            // Формируем текст статуса — всегда в формате "Наименование столбца: ххх"
            var parts = new List<string>();

            foreach (var kvp in columnValues)
            {
                string header = kvp.Key;
                var values = kvp.Value;

                if (header == "Длина")
                {
                    double sum = 0;
                    int count = 0;
                    foreach (var v in values)
                    {
                        if (v != null && double.TryParse(v.ToString(), out double d))
                        {
                            sum += d;
                            count++;
                        }
                    }
                    if (count > 0)
                        parts.Add($"Длина: {sum}");
                }
                else if (header == "Количество")
                {
                    int sum = 0;
                    int count = 0;
                    foreach (var v in values)
                    {
                        if (v != null && int.TryParse(v.ToString(), out int i))
                        {
                            sum += i;
                            count++;
                        }
                    }
                    if (count > 0)
                        parts.Add($"Количество: {sum}");
                }
                else
                {
                    // Для текстовых и прочих столбцов — количество выделенных ячеек
                    int count = values.Count(v => v != null && !string.IsNullOrWhiteSpace(v?.ToString()));
                    if (count > 0)
                        parts.Add($"{header}: {count}");
                }
            }

            statusBar.Text = string.Join("  |  ", parts);
        }
    }
}
