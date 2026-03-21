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
            bool hasManualData = _settings.ManualCuts.Any();
            chkManual.IsChecked = hasManualData;

            _manualCutControl = new ManualCutControl();
            var manualCuts = new ObservableCollection<ManualCutRow>(_settings.ManualCuts);
            _manualCutControl.Initialize(_articleName, manualCuts,
                GetEffectiveBarLength(), GetEffectivePresetIndex(),
                _presets, _vals, _stockLengths.Select(s => s.Length).ToList());

            manualCutFrame.Content = _manualCutControl;
            _manualCutControl.Visibility = hasManualData ? Visibility.Visible : Visibility.Collapsed;

            chkManual.Checked += (s, e) => { _manualCutControl.Visibility = Visibility.Visible; };
            chkManual.Unchecked += (s, e) => { _manualCutControl.Visibility = Visibility.Collapsed; };

            UpdateInfoBlock();
        }

        private void BuildRightPanel(DataRow[] articleRows)
        {
            DataTable resDt = new DataTable();
            foreach (var k in _keys) resDt.Columns.Add(k);
            foreach (var v in _vals) resDt.Columns.Add(v, typeof(double));
            resDt.Columns.Add("Количество", typeof(double));

            foreach (var sg in articleRows.GroupBy(r => string.Join("|", _vals.Select(v => r[v]?.ToString()))))
            {
                DataRow nr = resDt.NewRow();
                foreach (var k in _keys) nr[k] = sg.First()[k];
                foreach (var v in _vals) nr[v] = sg.First()[v];
                nr["Количество"] = sg.Sum(r => !string.IsNullOrEmpty(_qty) ? Convert.ToDouble(r[_qty] == DBNull.Value ? 0 : r[_qty]) : 1.0);
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
                    int c = string.IsNullOrEmpty(_qty) ? 1 : Convert.ToInt32(row[_qty] == DBNull.Value ? 1 : row[_qty]);
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
        }
    }
}