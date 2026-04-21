using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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

            var partsList = new ObservableCollection<PartItem>();

            // Группируем по комбинации ключей и значений, чтобы схлопнуть одинаковые детали
            var groupKeys = _keys.Concat(_vals).ToList();
            
            // Преобразуем DataView в список DataRow для группировки
            var rows = new List<DataRow>();
            foreach (DataRowView rowView in _articleView)
            {
                rows.Add(rowView.Row);
            }

            foreach (var sg in rows.GroupBy(r => string.Join("|", groupKeys.Select(k => r[k]?.ToString()))))
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
                            {
                                qtySum += (int)q;
                            }
                        }
                    }
                }
                else
                {
                    qtySum = sg.Count(); // Если колонки количества нет, считаем строки
                }

                partsList.Add(new PartItem
                {
                    Article = article,
                    Length = length,
                    Count = qtySum
                });
            }

            // Сортировка по длине по убыванию
            var sortedParts = partsList.OrderByDescending(p => p.Length).ToList();
            gridDetails.ItemsSource = sortedParts;
        }

        /// <summary>
        /// Строит правую панель детализации: группирует строки и выводит сводку по деталям артикула.
        /// </summary>
        private void BuildRightPanel(DataRow[] articleRows)
        {
            var partsList = new ObservableCollection<PartItem>();

            // Группируем по комбинации ключей и значений, чтобы схлопнуть одинаковые детали
            var groupKeys = _keys.Concat(_vals).ToList();
            foreach (var sg in articleRows.GroupBy(r => string.Join("|", groupKeys.Select(k => r[k]?.ToString()))))
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
                            {
                                qtySum += (int)q;
                            }
                        }
                    }
                }
                else
                {
                    qtySum = sg.Count(); // Если колонки количества нет, считаем строки
                }

                partsList.Add(new PartItem
                {
                    Article = article,
                    Length = length,
                    Count = qtySum
                });
            }

            // Сортировка по длине по убыванию
            var sortedParts = partsList.OrderByDescending(p => p.Length).ToList();
            gridDetails.ItemsSource = sortedParts;
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
        }
    }
}