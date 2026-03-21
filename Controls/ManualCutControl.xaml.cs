using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using LinearCutWpf.Models;

namespace LinearCutWpf.Controls
{
    public partial class ManualCutControl : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        private string _articleName;
        private ObservableCollection<ManualCutRow> _manualCuts;
        private double _barLength;
        private int _presetIndex;
        private List<PresetModel> _presets;
        private Dictionary<double, int> _availableParts;
        private Dictionary<double, int> _usedParts;
        private List<string> _availableSizes;
        private List<double> _availableBarLengths;

        public ObservableCollection<ManualCutRow> ManualCuts => _manualCuts;
        
        // Свойства для привязки из XAML
        public List<string> AvailableSizes => _availableSizes;
        public List<string> BarLengths => _availableBarLengths.Select(b => b.ToString()).ToList();

        private PresetModel CurrentPreset => _presets != null && _presetIndex >= 0 && _presetIndex < _presets.Count 
            ? _presets[_presetIndex] 
            : null;

        public ManualCutControl()
        {
            InitializeComponent();
            _manualCuts = new ObservableCollection<ManualCutRow>();
            _usedParts = new Dictionary<double, int>();
            _availableParts = new Dictionary<double, int>();
            _availableSizes = new List<string>();
            _availableBarLengths = new List<double>();
            
            dgManualCuts.ItemsSource = _manualCuts;
            
            // Подписываемся на изменения коллекции
            _manualCuts.CollectionChanged += (s, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (ManualCutRow item in e.NewItems)
                        item.PropertyChanged += OnRowPropertyChanged;
                }
                if (e.OldItems != null)
                {
                    foreach (ManualCutRow item in e.OldItems)
                        item.PropertyChanged -= OnRowPropertyChanged;
                }
                RefreshValidation();
            };
        }

        /// <summary>
        /// Инициализация контрола данными
        /// </summary>
        public void Initialize(string articleName, ObservableCollection<ManualCutRow> manualCuts,
            double barLength, int presetIndex, List<PresetModel> presets,
            List<string> availableSizes, List<double> availableBarLengths)
        {
            _articleName = articleName;
            _manualCuts = manualCuts;
            _barLength = barLength;
            _presetIndex = presetIndex;
            _presets = presets;
            _availableSizes = availableSizes ?? new List<string>();
            _availableBarLengths = availableBarLengths ?? new List<double>();

            dgManualCuts.ItemsSource = _manualCuts;
            
            UpdateDropdowns();
            CalculateAvailableParts();
            RefreshValidation();
        }

        /// <summary>
        /// Обновить параметры валидации (длина хлыста, пресет)
        /// </summary>
        public void UpdateValidationParams(double barLength, int presetIndex)
        {
            _barLength = barLength;
            _presetIndex = presetIndex;
            RefreshValidation();
        }

        /// <summary>
        /// Обновить выпадающие списки размеров (вызывается при изменении данных)
        /// </summary>
        public void UpdateDropdowns()
        {
            // Теперь списки обновляются автоматически через привязку данных
            // Этот метод оставлен для совместимости с существующим кодом
            OnPropertyChanged(nameof(AvailableSizes));
            OnPropertyChanged(nameof(BarLengths));
        }

        /// <summary>
        /// Подсчитать доступное количество деталей по размерам
        /// </summary>
        public void CalculateAvailableParts(List<string> sizes, List<int> quantities)
        {
            _availableParts.Clear();
            
            for (int i = 0; i < sizes.Count; i++)
            {
                if (double.TryParse(sizes[i], out double size))
                {
                    int qty = i < quantities.Count ? quantities[i] : 1;
                    if (_availableParts.ContainsKey(size))
                        _availableParts[size] += qty;
                    else
                        _availableParts[size] = qty;
                }
            }
        }

        private void CalculateAvailableParts()
        {
            // Этот метод будет вызываться извне с правильными данными
            // Здесь просто инициализируем пустой словарь
        }

        /// <summary>
        /// Подсчитать использованные детали из ручного раскроя
        /// </summary>
        private void CalculateUsedParts()
        {
            _usedParts.Clear();

            if (CurrentPreset == null || _manualCuts == null) return;

            foreach (var row in _manualCuts)
            {
                int count = row.Count;
                string[] sizes = { row.Size1, row.Size2, row.Size3, row.Size4 };
                
                foreach (var sizeStr in sizes)
                {
                    if (!string.IsNullOrEmpty(sizeStr) && double.TryParse(sizeStr, out double size) && size > 0)
                    {
                        if (_usedParts.ContainsKey(size))
                            _usedParts[size] += count;
                        else
                            _usedParts[size] = count;
                    }
                }
            }
        }

        /// <summary>
        /// Проверить наличие ошибок валидации
        /// </summary>
        public bool HasErrors()
        {
            if (CurrentPreset == null || _manualCuts == null) return false;

            foreach (var row in _manualCuts)
            {
                if (!row.BarLength.HasValue) continue;
                
                double available = row.BarLength.Value - CurrentPreset.TrimStart - CurrentPreset.TrimEnd;
                double used = 0;
                string[] sizes = { row.Size1, row.Size2, row.Size3, row.Size4 };
                
                foreach (var sizeStr in sizes)
                {
                    double val = 0;
                    if (!string.IsNullOrEmpty(sizeStr))
                        double.TryParse(sizeStr, out val);
                        
                    if (val > 0)
                    {
                        // Проверка превышения количества деталей
                        if (_availableParts.ContainsKey(val))
                        {
                            int availableCount = _availableParts[val];
                            int usedCount = _usedParts.ContainsKey(val) ? _usedParts[val] : 0;
                            if (usedCount > availableCount) return true;
                        }

                        double needed = val + CurrentPreset.CutWidth;
                        if (used + needed > available) return true;
                        used += needed;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Получить подробное описание ошибок
        /// </summary>
        public List<string> GetErrorMessages()
        {
            var errors = new List<string>();
            if (CurrentPreset == null || _manualCuts == null) return errors;

            int rowNum = 0;
            foreach (var row in _manualCuts)
            {
                rowNum++;
                if (!row.BarLength.HasValue) continue;

                double stock = row.BarLength.Value;
                double available = stock - CurrentPreset.TrimStart - CurrentPreset.TrimEnd;
                double used = 0;

                string[] sizes = { row.Size1, row.Size2, row.Size3, row.Size4 };

                foreach (var sizeStr in sizes)
                {
                    if (string.IsNullOrEmpty(sizeStr)) continue;

                    if (double.TryParse(sizeStr, out double val) && val > 0)
                    {
                        // Проверка превышения количества деталей
                        if (_availableParts.ContainsKey(val))
                        {
                            int availableCount = _availableParts[val];
                            int usedCount = _usedParts.ContainsKey(val) ? _usedParts[val] : 0;
                            if (usedCount > availableCount)
                                errors.Add($"Строка {rowNum}: деталей {val} мм в раскрое больше ({usedCount}), чем доступно ({availableCount})");
                        }

                        double needed = val + CurrentPreset.CutWidth;
                        if (used + needed > available)
                        {
                            errors.Add($"Строка {rowNum}: детали {val} мм не помещаются на хлыст {stock} мм (отступ {CurrentPreset.TrimStart}-{CurrentPreset.TrimEnd}, рез {CurrentPreset.CutWidth} мм, занято {used:F1} мм)");
                        }
                        else
                        {
                            used += needed;
                        }
                    }
                }
            }
            return errors;
        }

        /// <summary>
        /// Проверить наличие только ошибок количества
        /// </summary>
        public bool HasCountErrors()
        {
            if (CurrentPreset == null || _manualCuts == null) return false;

            foreach (var row in _manualCuts)
            {
                if (!row.BarLength.HasValue) continue;
                
                string[] sizes = { row.Size1, row.Size2, row.Size3, row.Size4 };
                foreach (var sizeStr in sizes)
                {
                    if (!string.IsNullOrEmpty(sizeStr) && double.TryParse(sizeStr, out double val) && val > 0)
                    {
                        if (_availableParts.ContainsKey(val))
                        {
                            int availableCount = _availableParts[val];
                            int usedCount = _usedParts.ContainsKey(val) ? _usedParts[val] : 0;
                            if (usedCount > availableCount) return true;
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Обновить валидацию (подсветка ошибок)
        /// </summary>
        public void RefreshValidation()
        {
            if (CurrentPreset == null) return;

            // Пересчитываем использованные детали
            CalculateUsedParts();

            // Сбрасываем ошибки
            foreach (var row in _manualCuts)
            {
                row.HasLengthError = false;
                row.HasCountError = false;
            }

            foreach (var row in _manualCuts)
            {
                if (!row.BarLength.HasValue) continue;
                
                double stock = row.BarLength.Value;
                double available = stock - CurrentPreset.TrimStart - CurrentPreset.TrimEnd;
                double used = 0;
                
                string[] sizes = { row.Size1, row.Size2, row.Size3, row.Size4 };
                
                foreach (var sizeStr in sizes)
                {
                    if (string.IsNullOrEmpty(sizeStr)) continue;
                    
                    if (double.TryParse(sizeStr, out double val) && val > 0)
                    {
                        // Проверка: не превышает ли количество размещенных деталей доступное количество
                        if (_availableParts.ContainsKey(val))
                        {
                            int availableCount = _availableParts[val];
                            int usedCount = _usedParts.ContainsKey(val) ? _usedParts[val] : 0;
                            if (usedCount > availableCount)
                                row.HasCountError = true;
                        }

                        double needed = val + CurrentPreset.CutWidth;
                        if (used + needed > available)
                            row.HasLengthError = true;
                        else
                            used += needed;
                    }
                }
            }

            // Обновляем отображение
            dgManualCuts.Items.Refresh();

            // Показываем/скрываем сообщение об ошибках
            bool hasAnyErrors = _manualCuts.Any(r => r.HasLengthError || r.HasCountError);
            txtErrors.Visibility = hasAnyErrors ? Visibility.Visible : Visibility.Collapsed;
            
            if (hasAnyErrors)
            {
                var errorMessages = new List<string>();
                if (_manualCuts.Any(r => r.HasLengthError))
                    errorMessages.Add("Превышена длина хлыста");
                if (_manualCuts.Any(r => r.HasCountError))
                    errorMessages.Add("Превышено количество деталей");
                txtErrors.Text = string.Join("; ", errorMessages);
            }
        }

        private void OnRowPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ManualCutRow.BarLength) ||
                e.PropertyName == nameof(ManualCutRow.Size1) ||
                e.PropertyName == nameof(ManualCutRow.Size2) ||
                e.PropertyName == nameof(ManualCutRow.Size3) ||
                e.PropertyName == nameof(ManualCutRow.Size4) ||
                e.PropertyName == nameof(ManualCutRow.Count))
            {
                RefreshValidation();
            }
        }

        private void dgManualCuts_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            // Отложенная валидация после завершения редактирования
            Dispatcher.BeginInvoke(new Action(() => RefreshValidation()), System.Windows.Threading.DispatcherPriority.Background);
        }
    }
}