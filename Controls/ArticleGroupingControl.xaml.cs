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
    public partial class ArticleGroupingControl : UserControl
    {
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

        private ManualCutControl _manualCutControl;

        public ManualCutControl ManualCutControl => _manualCutControl;
        public DataGrid GridDetails => gridDetails;

        /// <summary>
        /// Событие при изменении настроек (хлыст/пресет) — для перекраски таба
        /// </summary>
        public event Action<string> SettingsChanged;

        public ArticleGroupingControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Инициализация контрола данными артикула
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

            BuildLeftPanel();
            BuildRightPanel(articleRows);
        }

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
            
            _manualCutControl.Initialize(_articleName, manualCuts,
                GetEffectiveBarLength(), GetEffectivePresetIndex(),
                _presets, _vals, _stockLengths.Select(s => s.Length).ToList());

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

        private void BuildRightPanel(DataRow[] articleRows)
        {
            DataTable resDt = new DataTable();
            foreach (var k in _keys) resDt.Columns.Add(k);
            foreach (var v in _vals) resDt.Columns.Add(v, typeof(double));
            resDt.Columns.Add("Количество", typeof(double));

            // Группируем по комбинации ключей и значений, чтобы не терять строки с разными ключами
            var groupKeys = _keys.Concat(_vals).ToList();
            foreach (var sg in articleRows.GroupBy(r => string.Join("|", groupKeys.Select(k => r[k]?.ToString()))))
            {
                DataRow nr = resDt.NewRow();
                foreach (var k in _keys) nr[k] = sg.First()[k];
                foreach (var v in _vals) nr[v] = sg.First()[v];
                
                double qtySum = 0;
                if (!string.IsNullOrEmpty(_qty))
                {
                    foreach (var r in sg)
                    {
                        var qtyVal = r[_qty];
                        if (qtyVal != DBNull.Value && !string.IsNullOrWhiteSpace(qtyVal?.ToString()))
                        {
                            if (double.TryParse(qtyVal.ToString(), out double q))
                            {
                                qtySum += q;
                            }
                        }
                    }
                }
                
                nr["Количество"] = qtySum;
                resDt.Rows.Add(nr);
            }

            resDt.DefaultView.Sort = $"{_vals.First()} DESC";
            gridDetails.ItemsSource = resDt.DefaultView;
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

        public bool HasManualErrors()
        {
            return _manualCutControl != null && _manualCutControl.HasErrors();
        }

        private void RefreshManualGridValidation()
        {
            var preset = GetEffectivePreset();
            if (preset == null) return;

            if (_manualCutControl != null)
            {
                _manualCutControl.UpdateValidationParams(GetEffectiveBarLength(), GetEffectivePresetIndex());
                UpdateErrorsPanel();
            }

            UpdateInfoBlock();
        }

        private void UpdateErrorsPanel()
        {
            if (_manualCutControl == null) return;

            var errorsBorder = FindName("errorsBorder") as Border;
            var txtDetailErrors = FindName("txtDetailErrors") as TextBlock;
            if (errorsBorder == null || txtDetailErrors == null) return;

            var errors = _manualCutControl.GetErrorMessages();
            if (errors.Any())
            {
                txtDetailErrors.Text = string.Join("\n", errors);
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

        private void UpdateInfoBlock()
        {
            if (infoLabel == null) return;

            var preset = GetEffectivePreset();
            if (preset == null) { infoLabel.Text = ""; return; }

            double barLen = GetEffectiveBarLength();

            var parts = new List<double>();
            double totalPartsLength = 0;

            if (_gridInput != null && _vals.Any())
            {
                foreach (DataRow row in _gridInput.Rows)
                {
                    var valObj = row[_vals.First()];
                    if (valObj == DBNull.Value || string.IsNullOrWhiteSpace(valObj?.ToString())) continue;
                    double l = Convert.ToDouble(valObj);
                    double lWithCut = l + preset.CutWidth;
                    int c = 1;
                    if (!string.IsNullOrEmpty(_qty))
                    {
                        var qtyObj = row[_qty];
                        if (qtyObj != DBNull.Value && !string.IsNullOrWhiteSpace(qtyObj?.ToString()))
                        {
                            if (double.TryParse(qtyObj.ToString(), out double q))
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