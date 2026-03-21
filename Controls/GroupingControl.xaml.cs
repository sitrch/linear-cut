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
    public partial class GroupingControl : UserControl
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

        public event EventHandler<Dictionary<string, ArticleSettings>> SettingsApplied;

        public TabControl GroupingTabControl => _groupingTabControl;
        public Dictionary<string, ArticleSettings> ArticleSettings => _articleSettings;

        public GroupingControl()
        {
            InitializeComponent();
            _groupingTabControl = (TabControl)FindName("groupingTabControl");
            _articleSettings = new Dictionary<string, ArticleSettings>();
            _groupingTabControl.SelectionChanged += OnGroupingTabSelecting;
            UpdateHintVisibility();
        }

        private void UpdateHintVisibility()
        {
            var hint = (TextBlock)FindName("hintText");
            if (hint != null)
                hint.Visibility = _groupingTabControl.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        public void Initialize(double defaultBarLength, PresetModel defaultPreset,
            ObservableCollection<StockLengthModel> stockLengths, List<PresetModel> presets,
            DataTable gridInput, Func<string, List<string>> getCheckedCols)
        {
            _defaultBarLength = defaultBarLength;
            _defaultPreset = defaultPreset;
            _stockLengths = stockLengths;
            _presets = presets;
            _gridInput = gridInput;
            _getCheckedCols = getCheckedCols;
        }

        public ArticleSettings GetOrCreateArticleSettings(string articleName)
        {
            if (!_articleSettings.ContainsKey(articleName))
                _articleSettings[articleName] = new ArticleSettings { ArticleName = articleName };
            return _articleSettings[articleName];
        }

        public PresetModel GetEffectivePreset(string articleName)
        {
            if (_articleSettings.TryGetValue(articleName, out var settings) && settings.Preset != null)
                return settings.Preset;
            return _defaultPreset;
        }

        public double GetEffectiveBarLength(string articleName)
        {
            if (_articleSettings.TryGetValue(articleName, out var settings) && settings.BarLength.HasValue)
                return settings.BarLength.Value;
            return _defaultBarLength;
        }

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

        public void SetDefaults(double barLength, PresetModel preset)
        {
            _defaultBarLength = barLength;
            _defaultPreset = preset;
        }

        public bool HasAnyManualErrors()
        {
            foreach (var kvp in _articleGroupingControls)
            {
                if (kvp.Value.HasManualErrors()) return true;
            }
            return false;
        }

        public bool HasManualErrorsForArticle(string articleName)
        {
            if (_articleGroupingControls.TryGetValue(articleName, out var ctrl))
                return ctrl.HasManualErrors();
            return false;
        }

        public void RunGroupingWithTabs()
        {
            if (_gridInput == null) 
            {
                System.Diagnostics.Debug.WriteLine("RunGroupingWithTabs: _gridInput is null");
                return;
            }

            var keys = _getCheckedCols("IsKey");
            var vals = _getCheckedCols("IsVal");
            var qty = _getCheckedCols("IsQty").FirstOrDefault();

            System.Diagnostics.Debug.WriteLine($"RunGroupingWithTabs: keys={string.Join(",", keys)}, vals={string.Join(",", vals)}, qty={qty}");

            _groupingTabControl.Items.Clear();
            _tabToArticle.Clear();
            _articleGroupingControls.Clear();
            if (!keys.Any() || !vals.Any())
            {
                System.Diagnostics.Debug.WriteLine("RunGroupingWithTabs: no keys or vals, showing hint");
                UpdateHintVisibility();
                return;
            }

            var groups = _gridInput.Rows.Cast<DataRow>().GroupBy(r => string.Join("_", keys.Select(k => r[k]?.ToString())));

            foreach (var g in groups)
            {
                string articleName = g.Key;
                var settings = GetOrCreateArticleSettings(articleName);
                bool isCustom = settings.HasCustomSettings(_defaultBarLength, _defaultPreset);

                TabItem tp = new TabItem();
                tp.Header = articleName;
                tp.Background = isCustom ? Brushes.MistyRose : SystemColors.ControlBrush;
                _tabToArticle[tp] = articleName;

                DataRow[] articleRows = g.ToArray();

                ArticleGroupingControl articleCtrl = new ArticleGroupingControl();
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

            UpdateHintVisibility();
        }

        private void ColorTab(string articleName)
        {
            foreach (var kvp in _tabToArticle)
            {
                if (kvp.Value == articleName)
                {
                    var settings = GetOrCreateArticleSettings(articleName);
                    kvp.Key.Background = settings.HasCustomSettings(_defaultBarLength, _defaultPreset)
                        ? Brushes.MistyRose
                        : SystemColors.ControlBrush;
                }
            }
        }

        private void OnGroupingTabSelecting(object sender, SelectionChangedEventArgs e)
        {
            if (_groupingTabControl.SelectedItem == null || _tabToArticle.Count == 0) return;

            if (_tabToArticle.TryGetValue((TabItem)_groupingTabControl.SelectedItem, out string currentArticle))
            {
                if (HasManualErrorsForArticle(currentArticle))
                {
                    // Отмена переключения
                    MessageBox.Show("Есть ошибки в ручном раскрое!\r\nДетали не помещаются в хлыст.\r\nИсправьте красные ячейки.",
                        "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                    e.Handled = true;
                }
            }
        }
    }
}
