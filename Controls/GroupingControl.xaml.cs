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
    /// Контрол, управляющий вкладками для группировки артикулов.
    /// Создает и отображает вложенные ArticleGroupingControl для каждого артикула.
    /// </summary>
    public partial class GroupingControl : UserControl, INotifyPropertyChanged
    {
        private TabControl _groupingTabControl;
        private Dictionary<string, ArticleSettings> _articleSettings;
        private Dictionary<TabItem, string> _tabToArticle = new Dictionary<TabItem, string>();
        private Dictionary<string, ArticleGroupingControl> _articleGroupingControls = new Dictionary<string, ArticleGroupingControl>();
        private double _defaultBarLength;
        private PresetModel _defaultPreset;
        private ObservableCollection<StockLengthModel> _stockLengths;
        private List<PresetModel> _presets;
        private DataTable _gridInput;
        private Func<string, List<string>> _getCheckedCols;
        private HashSet<int> _invalidRows;
        private Services.DataStoreService _dataStore => Services.DataStoreService.Instance;

        public DataTable MainDataTable => _dataStore?.ProcessedDataTable;

        /// <summary>
        /// Событие, вызываемое при изменении свойства (реализация INotifyPropertyChanged).
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Возвращает элемент управления вкладками для группировки.
        /// </summary>
        public TabControl GroupingTabControl => _groupingTabControl;

        /// <summary>
        /// Возвращает словарь настроек для каждого артикула.
        /// </summary>
        public Dictionary<string, ArticleSettings> ArticleSettings => _articleSettings;

        private GridLength _leftPanelWidth;

        /// <summary>
        /// Получает или задает ширину левой панели.
        /// При изменении сохраняет значение в настройки.
        /// </summary>
        public GridLength LeftPanelWidth
        {
            get => _leftPanelWidth;
            set
            {
                if (_leftPanelWidth != value)
                {
                    _leftPanelWidth = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LeftPanelWidth)));
                    Services.CutSettingsProvider.SaveLeftPanelWidth(value.Value);
                }
            }
        }

        /// <summary>
        /// Инициализирует новый экземпляр класса <see cref="GroupingControl"/>.
        /// </summary>
        public GroupingControl()
        {
            InitializeComponent();
            _leftPanelWidth = new GridLength(Services.CutSettingsProvider.LoadLeftPanelWidth());
            _groupingTabControl = (TabControl)FindName("groupingTabControl");
            _articleSettings = new Dictionary<string, ArticleSettings>();
            _groupingTabControl.SelectionChanged += OnGroupingTabSelecting;
            UpdateHintVisibility();
            DataContext = this;
        }

        private void UpdateHintVisibility()
        {
            var hint = (TextBlock)FindName("hintText");
            if (hint != null)
                hint.Visibility = _groupingTabControl.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Событие, вызываемое после применения новых настроек.
        /// </summary>
        public event EventHandler<Dictionary<string, ArticleSettings>> SettingsApplied;

        /// <summary>
        /// Применяет новые настройки артикулов и вызывает событие <see cref="SettingsApplied"/>.
        /// </summary>
        /// <param name="newSettings">Словарь новых настроек артикулов.</param>
        public void ApplySettingsAndNotify(Dictionary<string, ArticleSettings> newSettings)
        {
            _articleSettings = newSettings;
            SettingsApplied?.Invoke(this, _articleSettings);
        }

        /// <summary>
        /// Инициализирует контрол общими данными для раскроя.
        /// </summary>
        /// <param name="defaultBarLength">Длина хлыста по умолчанию.</param>
        /// <param name="defaultPreset">Пресет настроек оборудования по умолчанию.</param>
        /// <param name="stockLengths">Коллекция доступных длин хлыстов (складских остатков).</param>
        /// <param name="presets">Список доступных пресетов оборудования.</param>
        /// <param name="gridInput">Входная таблица данных.</param>
        /// <param name="getCheckedCols">Функция для получения выбранных колонок по их роли (IsKey, IsVal, IsQty).</param>
        /// <param name="invalidRows">Набор индексов строк с ошибками валидации.</param>
        public void Initialize(double defaultBarLength, PresetModel defaultPreset,
            ObservableCollection<StockLengthModel> stockLengths, List<PresetModel> presets,
            DataTable gridInput, Func<string, List<string>> getCheckedCols, HashSet<int> invalidRows = null)
        {
            _defaultBarLength = defaultBarLength;
            _defaultPreset = defaultPreset;
            _stockLengths = stockLengths;
            _presets = presets;
            _gridInput = gridInput;
            _getCheckedCols = getCheckedCols;
            _invalidRows = invalidRows ?? new HashSet<int>();
        }

        /// <summary>
        /// Получает существующие настройки для артикула или создает новые, если их нет.
        /// </summary>
        /// <param name="articleName">Имя артикула.</param>
        /// <returns>Объект настроек для указанного артикула.</returns>
        public ArticleSettings GetOrCreateArticleSettings(string articleName)
        {
            if (!_articleSettings.ContainsKey(articleName))
                _articleSettings[articleName] = new ArticleSettings { ArticleName = articleName };
            return _articleSettings[articleName];
        }

        /// <summary>
        /// Возвращает пресет оборудования, который должен применяться для данного артикула 
        /// (индивидуальный или по умолчанию).
        /// </summary>
        /// <param name="articleName">Имя артикула.</param>
        /// <returns>Пресет оборудования.</returns>
        public PresetModel GetEffectivePreset(string articleName)
        {
            if (_articleSettings.TryGetValue(articleName, out var settings) && settings.Preset != null)
                return settings.Preset;
            return _defaultPreset;
        }

        /// <summary>
        /// Возвращает длину хлыста, которая должна применяться для данного артикула 
        /// (индивидуальная или по умолчанию).
        /// </summary>
        /// <param name="articleName">Имя артикула.</param>
        /// <returns>Длина хлыста.</returns>
        public double GetEffectiveBarLength(string articleName)
        {
            if (_articleSettings.TryGetValue(articleName, out var settings) && settings.BarLength.HasValue)
                return settings.BarLength.Value;
            return _defaultBarLength;
        }

        /// <summary>
        /// Возвращает индекс пресета в списке пресетов (сдвиг +1, т.к. 0 - это "По умолчанию").
        /// </summary>
        /// <param name="articleName">Имя артикула.</param>
        /// <returns>Индекс пресета в ComboBox.</returns>
        public int GetEffectivePresetIndex(string articleName)
        {
            if (_presets == null) return 0;

            if (_articleSettings.TryGetValue(articleName, out var settings) && settings.Preset != null)
            {
                int idx = _presets.IndexOf(settings.Preset);
                return idx >= 0 ? idx + 1 : 0;
            }
            return 0;
        }

        /// <summary>
        /// Устанавливает значения длины хлыста и пресета по умолчанию.
        /// </summary>
        /// <param name="barLength">Длина хлыста.</param>
        /// <param name="preset">Пресет оборудования.</param>
        public void SetDefaults(double barLength, PresetModel preset)
        {
            _defaultBarLength = barLength;
            _defaultPreset = preset;
        }

        /// <summary>
        /// Проверяет, есть ли ошибки валидации в ручном раскрое хотя бы на одной из вкладок артикулов.
        /// </summary>
        /// <returns>True, если есть ошибки, иначе False.</returns>
        public bool HasAnyManualErrors()
        {
            foreach (var kvp in _articleGroupingControls)
            {
                if (kvp.Value.HasManualErrors()) return true;
            }
            return false;
        }

        /// <summary>
        /// Проверяет, есть ли ошибки валидации в ручном раскрое для конкретного артикула.
        /// </summary>
        /// <param name="articleName">Имя артикула.</param>
        /// <returns>True, если есть ошибки, иначе False.</returns>
        public bool HasManualErrorsForArticle(string articleName)
        {
            if (_articleGroupingControls.TryGetValue(articleName, out var ctrl))
                return ctrl.HasManualErrors();
            return false;
        }

        /// <summary>
        /// Выполняет группировку входных данных по артикулам и создает вкладки для каждого артикула.
        /// Использует DataStoreService для получения DataView по каждому артикулу.
        /// </summary>
        public void RunGroupingWithTabs()
        {
            // Проверяем, есть ли данные в хранилище
            if (_dataStore.ProcessedDataTable == null)
            {
                // Fallback к старой логике, если хранилище пустое
                RunGroupingWithTabsLegacy();
                return;
            }

            var keys = _getCheckedCols("IsKey");
            var vals = _getCheckedCols("IsVal");

            _groupingTabControl.Items.Clear();
            _tabToArticle.Clear();
            _articleGroupingControls.Clear();
            
            if (!keys.Any() || !vals.Any())
            {
                UpdateHintVisibility();
                return;
            }

            // Получаем уникальные артикулы из хранилища
            var uniqueArticles = _dataStore.GetUniqueArticles();

            foreach (var articleName in uniqueArticles)
            {
                // Игнорируем пустые артикулы
                if (string.IsNullOrWhiteSpace(articleName)) continue;

                var settings = GetOrCreateArticleSettings(articleName);
                bool isCustom = settings.HasCustomSettings(_defaultBarLength, _defaultPreset);

                TabItem tp = new TabItem();
                tp.Header = articleName;
                tp.Background = isCustom ? Brushes.MistyRose : Brushes.White;
                _tabToArticle[tp] = articleName;

                // Получаем DataView для артикула из хранилища
                DataView articleView = _dataStore.GetArticleView(articleName);

                ArticleGroupingControl articleCtrl = new ArticleGroupingControl();
                
                // Привязка ширины левой панели
                System.Windows.Data.Binding binding = new System.Windows.Data.Binding(nameof(LeftPanelWidth))
                {
                    Source = this,
                    Mode = System.Windows.Data.BindingMode.TwoWay
                };
                articleCtrl.SetBinding(ArticleGroupingControl.LeftPanelWidthProperty, binding);

                articleCtrl.Initialize(articleName, settings,
                    _defaultBarLength, _defaultPreset,
                    _stockLengths, _presets,
                    _dataStore.ProcessedDataTable, _getCheckedCols,
                    keys, vals, _getCheckedCols("IsQty").FirstOrDefault(), articleView);

                articleCtrl.SettingsChanged += (art) =>
                {
                    ColorTab(art);
                };

                tp.Content = articleCtrl;
                _groupingTabControl.Items.Add(tp);
                _articleGroupingControls[articleName] = articleCtrl;
            }

            if (_groupingTabControl.Items.Count > 0)
            {
                _groupingTabControl.SelectedIndex = 0;
                RecolorAllTabs();
            }

            UpdateHintVisibility();
        }

        /// <summary>
        /// Старая логика группировки (fallback, если хранилище пустое).
        /// </summary>
        private void RunGroupingWithTabsLegacy()
        {
            if (_gridInput == null) return;

            var keys = _getCheckedCols("IsKey");
            var vals = _getCheckedCols("IsVal");
            var qtyList = _getCheckedCols("IsQty");
            var qty = qtyList.FirstOrDefault();

            _groupingTabControl.Items.Clear();
            _tabToArticle.Clear();
            _articleGroupingControls.Clear();
            if (!keys.Any() || !vals.Any())
            {
                UpdateHintVisibility();
                return;
            }

            var groups = _gridInput.Rows.Cast<DataRow>()
                .Where(r => !_invalidRows.Contains(_gridInput.Rows.IndexOf(r)))
                .GroupBy(r => 
                {
                    bool isError = false;
                    
                    if (vals.Any())
                    {
                        var valObj = r[vals.First()];
                        if (valObj == DBNull.Value || string.IsNullOrWhiteSpace(valObj?.ToString()))
                        {
                            isError = true;
                        }
                        else
                        {
                            string valStr = valObj.ToString().Replace(" ", "").Replace("\u00A0", "").Replace('.', ',');
                            if (!double.TryParse(valStr, out double l) || l <= 0)
                                isError = true;
                        }
                    }

                    if (!string.IsNullOrEmpty(qty))
                    {
                        var qtyObj = r[qty];
                        if (qtyObj != DBNull.Value && !string.IsNullOrWhiteSpace(qtyObj?.ToString()))
                        {
                            string qtyStr = qtyObj.ToString().Replace(" ", "").Replace("\u00A0", "").Replace('.', ',');
                            if (!double.TryParse(qtyStr, out double q) || q <= 0)
                                isError = true;
                        }
                    }

                    if (isError) return "Ошибки данных";

                    return DataHelper.GetArticleName(keys.Select(k => r[k]?.ToString()));
                });

            foreach (var g in groups)
            {
                string articleName = g.Key;
                var settings = GetOrCreateArticleSettings(articleName);
                bool isCustom = settings.HasCustomSettings(_defaultBarLength, _defaultPreset);

                TabItem tp = new TabItem();
                tp.Header = articleName;
                tp.Background = isCustom ? Brushes.MistyRose : Brushes.White;
                _tabToArticle[tp] = articleName;

                DataRow[] articleRows = g.ToArray();

                ArticleGroupingControl articleCtrl = new ArticleGroupingControl();
                
                // Привязка ширины левой панели
                System.Windows.Data.Binding binding = new System.Windows.Data.Binding(nameof(LeftPanelWidth))
                {
                    Source = this,
                    Mode = System.Windows.Data.BindingMode.TwoWay
                };
                articleCtrl.SetBinding(ArticleGroupingControl.LeftPanelWidthProperty, binding);

                articleCtrl.Initialize(articleName, settings,
                    _defaultBarLength, _defaultPreset,
                    _stockLengths, _presets,
                    _gridInput, _getCheckedCols,
                    keys, vals, qty, articleRows);

                articleCtrl.SettingsChanged += (art) =>
                {
                    ColorTab(art);
                };

                tp.Content = articleCtrl;
                _groupingTabControl.Items.Add(tp);
                _articleGroupingControls[articleName] = articleCtrl;
            }

            if (_groupingTabControl.Items.Count > 0)
            {
                _groupingTabControl.SelectedIndex = 0;
                RecolorAllTabs();
            }

            UpdateHintVisibility();
        }

        private void ColorTab(string articleName)
        {
            RecolorAllTabs();
        }

        private void RecolorAllTabs()
        {
            var selectedTab = _groupingTabControl.SelectedItem as TabItem;

            foreach (var kvp in _tabToArticle)
            {
                var tabItem = kvp.Key;
                var articleName = kvp.Value;
                var settings = GetOrCreateArticleSettings(articleName);
                var hasCustom = settings.HasCustomSettings(_defaultBarLength, _defaultPreset);
                bool isSelected = tabItem == selectedTab;

                if (isSelected)
                {
                    tabItem.Background = hasCustom ? Brushes.LightCoral : Brushes.LightBlue;
                    tabItem.Foreground = hasCustom ? Brushes.White : SystemColors.ControlTextBrush;
                }
                else
                {
                    tabItem.Background = hasCustom ? Brushes.MistyRose : Brushes.White;
                    tabItem.Foreground = hasCustom ? Brushes.DarkRed : SystemColors.ControlTextBrush;
                }
                
                tabItem.UpdateLayout();
            }
        }

        private void OnGroupingTabSelecting(object sender, SelectionChangedEventArgs e)
        {
            if (_groupingTabControl.SelectedItem == null || _tabToArticle.Count == 0) return;

            // Проверяем ошибки на вкладке, С КОТОРОЙ мы уходим (если такая есть)
            if (e.RemovedItems.Count > 0 && e.RemovedItems[0] is TabItem oldTab)
            {
                if (_tabToArticle.TryGetValue(oldTab, out string oldArticle))
                {
                    if (HasManualErrorsForArticle(oldArticle))
                    {
                        MessageBox.Show("Есть ошибки в ручном раскрое на текущей вкладке!\r\nДетали не помещаются в хлыст.\r\nИсправьте красные ячейки перед переключением.",
                            "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                        
                        // Возвращаем выделение обратно на старую вкладку
                        _groupingTabControl.SelectedItem = oldTab;
                        e.Handled = true;
                        return;
                    }
                }
            }

            // Перекрашиваем вкладки (выделяем новую активную)
            RecolorAllTabs();
        }
    }
}
