using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace LinearCutWpf.Models
{
    /// <summary>
    /// Глобальные настройки раскроя.
    /// </summary>
    public class CutSettings
    {
        public double TrimStart { get; set; }
        public double TrimEnd { get; set; }
        public double CutWidth { get; set; }
        public Dictionary<string, List<double>> ItemStocks { get; set; } = new Dictionary<string, List<double>>();
    }

    /// <summary>
    /// Модель пресета (набора параметров обрезки и толщины реза).
    /// </summary>
    public class PresetModel
    {
        public string Name { get; set; }
        public double TrimStart { get; set; }
        public double TrimEnd { get; set; }
        public double CutWidth { get; set; }
    }

    /// <summary>
    /// Модель результата распила одного хлыста.
    /// </summary>
    public class CutBar
    {
        public double StockLength { get; set; }
        public string Parts { get; set; }
        public double Remainder { get; set; }
    }

    /// <summary>
    /// Модель доступной длины хлыста (с флагом включения).
    /// </summary>
    public class StockLengthModel
    {
        public double Length { get; set; }
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Отображаемое значение длины. Возвращает пустую строку для значения по умолчанию (0).
        /// </summary>
        public string DisplayLength => Length == 0 ? "" : Length.ToString();
    }

    /// <summary>
    /// Строка ручного ввода деталей для определенного хлыста.
    /// Поддерживает валидацию и расчет доступного места.
    /// </summary>
    public class ManualCutRow : INotifyPropertyChanged
    {
        private double? _barLength;
        private System.Collections.ObjectModel.ObservableCollection<PartSizeAvailability> _availableSizes1 = new System.Collections.ObjectModel.ObservableCollection<PartSizeAvailability>();
        private System.Collections.ObjectModel.ObservableCollection<PartSizeAvailability> _availableSizes2 = new System.Collections.ObjectModel.ObservableCollection<PartSizeAvailability>();
        private System.Collections.ObjectModel.ObservableCollection<PartSizeAvailability> _availableSizes3 = new System.Collections.ObjectModel.ObservableCollection<PartSizeAvailability>();
        private System.Collections.ObjectModel.ObservableCollection<PartSizeAvailability> _availableSizes4 = new System.Collections.ObjectModel.ObservableCollection<PartSizeAvailability>();
        private string _size1;
        private string _size2;
        private string _size3;
        private string _size4;
        private int _count = 1;
        private bool _hasLengthError;
        private bool _hasCountError;
        private bool _hasLengthErrorBarLength;
        private bool _hasCountErrorCount;
        private bool _hasLengthErrorSize1;
        private bool _hasLengthErrorSize2;
        private bool _hasLengthErrorSize3;
        private bool _hasLengthErrorSize4;
        private bool _hasCountErrorSize1;
        private bool _hasCountErrorSize2;
        private bool _hasCountErrorSize3;
        private bool _hasCountErrorSize4;
        private bool _useRemainders = true;
        
        private double _availableSpaceForSize1 = double.MaxValue;
        private double _availableSpaceForSize2 = double.MaxValue;
        private double _availableSpaceForSize3 = double.MaxValue;
        private double _availableSpaceForSize4 = double.MaxValue;

        public double? BarLength
        {
            get => _barLength;
            set { _barLength = value; OnPropertyChanged(nameof(BarLength)); }
        }

        public string Size1
        {
            get => _size1;
            set { _size1 = value; OnPropertyChanged(nameof(Size1)); }
        }

        public string Size2
        {
            get => _size2;
            set { _size2 = value; OnPropertyChanged(nameof(Size2)); }
        }

        public string Size3
        {
            get => _size3;
            set { _size3 = value; OnPropertyChanged(nameof(Size3)); }
        }

        public string Size4
        {
            get => _size4;
            set { _size4 = value; OnPropertyChanged(nameof(Size4)); }
        }

        public int Count
        {
            get => _count;
            set { _count = value; OnPropertyChanged(nameof(Count)); }
        }

        public bool UseRemainders
        {
            get => _useRemainders;
            set { _useRemainders = value; OnPropertyChanged(nameof(UseRemainders)); }
        }

        public bool HasLengthError
        {
            get => _hasLengthError;
            set { _hasLengthError = value; OnPropertyChanged(nameof(HasLengthError)); }
        }

        public bool HasCountError
        {
            get => _hasCountError;
            set { _hasCountError = value; OnPropertyChanged(nameof(HasCountError)); }
        }

        public bool HasLengthErrorBarLength
        {
            get => _hasLengthErrorBarLength;
            set { _hasLengthErrorBarLength = value; OnPropertyChanged(nameof(HasLengthErrorBarLength)); }
        }

        public bool HasCountErrorCount
        {
            get => _hasCountErrorCount;
            set { _hasCountErrorCount = value; OnPropertyChanged(nameof(HasCountErrorCount)); }
        }

        public bool HasLengthErrorSize1
        {
            get => _hasLengthErrorSize1;
            set { _hasLengthErrorSize1 = value; OnPropertyChanged(nameof(HasLengthErrorSize1)); }
        }

        public bool HasLengthErrorSize2
        {
            get => _hasLengthErrorSize2;
            set { _hasLengthErrorSize2 = value; OnPropertyChanged(nameof(HasLengthErrorSize2)); }
        }

        public bool HasLengthErrorSize3
        {
            get => _hasLengthErrorSize3;
            set { _hasLengthErrorSize3 = value; OnPropertyChanged(nameof(HasLengthErrorSize3)); }
        }

        public bool HasLengthErrorSize4
        {
            get => _hasLengthErrorSize4;
            set { _hasLengthErrorSize4 = value; OnPropertyChanged(nameof(HasLengthErrorSize4)); }
        }

        public bool HasCountErrorSize1
        {
            get => _hasCountErrorSize1;
            set { _hasCountErrorSize1 = value; OnPropertyChanged(nameof(HasCountErrorSize1)); }
        }

        public bool HasCountErrorSize2
        {
            get => _hasCountErrorSize2;
            set { _hasCountErrorSize2 = value; OnPropertyChanged(nameof(HasCountErrorSize2)); }
        }

        public bool HasCountErrorSize3
        {
            get => _hasCountErrorSize3;
            set { _hasCountErrorSize3 = value; OnPropertyChanged(nameof(HasCountErrorSize3)); }
        }

        public bool HasCountErrorSize4
        {
            get => _hasCountErrorSize4;
            set { _hasCountErrorSize4 = value; OnPropertyChanged(nameof(HasCountErrorSize4)); }
        }

        public double AvailableSpaceForSize1
        {
            get => _availableSpaceForSize1;
            set { _availableSpaceForSize1 = value; OnPropertyChanged(nameof(AvailableSpaceForSize1)); }
        }

        public double AvailableSpaceForSize2
        {
            get => _availableSpaceForSize2;
            set { _availableSpaceForSize2 = value; OnPropertyChanged(nameof(AvailableSpaceForSize2)); }
        }

        public double AvailableSpaceForSize3
        {
            get => _availableSpaceForSize3;
            set { _availableSpaceForSize3 = value; OnPropertyChanged(nameof(AvailableSpaceForSize3)); }
        }

        public double AvailableSpaceForSize4
        {
            get => _availableSpaceForSize4;
            set { _availableSpaceForSize4 = value; OnPropertyChanged(nameof(AvailableSpaceForSize4)); }
        }

        public System.Collections.ObjectModel.ObservableCollection<PartSizeAvailability> AvailableSizes1
        {
            get => _availableSizes1;
            set { _availableSizes1 = value; OnPropertyChanged(nameof(AvailableSizes1)); }
        }

        public System.Collections.ObjectModel.ObservableCollection<PartSizeAvailability> AvailableSizes2
        {
            get => _availableSizes2;
            set { _availableSizes2 = value; OnPropertyChanged(nameof(AvailableSizes2)); }
        }

        public System.Collections.ObjectModel.ObservableCollection<PartSizeAvailability> AvailableSizes3
        {
            get => _availableSizes3;
            set { _availableSizes3 = value; OnPropertyChanged(nameof(AvailableSizes3)); }
        }

        public System.Collections.ObjectModel.ObservableCollection<PartSizeAvailability> AvailableSizes4
        {
            get => _availableSizes4;
            set { _availableSizes4 = value; OnPropertyChanged(nameof(AvailableSizes4)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Индивидуальные настройки для конкретного артикула (переопределение глобальных настроек).
    /// </summary>
    public class ArticleSettings
    {
        public string ArticleName { get; set; }
        public string ArticleDescription { get; set; }
        public double? VisibleHeight { get; set; }
        public double? BarLength { get; set; }
        public PresetModel Preset { get; set; }
        public System.Collections.ObjectModel.ObservableCollection<ManualCutRow> ManualCuts { get; set; } = new System.Collections.ObjectModel.ObservableCollection<ManualCutRow>();

        public bool HasCustomSettings(double defaultBar, PresetModel defaultPreset)
        {
            bool barChanged = BarLength.HasValue && BarLength.Value != defaultBar;
            bool presetChanged = Preset != null && defaultPreset != null && Preset.Name != defaultPreset.Name;
            bool presetSet = Preset != null && defaultPreset == null;
            // Проверяем, что ручной раскрой не пустой (есть хотя бы одна заполненная строка)
            bool hasManualCuts = ManualCuts != null && ManualCuts.Any(r => 
                r.BarLength.HasValue || 
                !string.IsNullOrEmpty(r.Size1) || 
                !string.IsNullOrEmpty(r.Size2) || 
                !string.IsNullOrEmpty(r.Size3) || 
                !string.IsNullOrEmpty(r.Size4));
            return barChanged || presetChanged || presetSet || hasManualCuts;
        }
    }

    /// <summary>
    /// Доступность размера детали для автодополнения и валидации в ручном раскрое.
    /// </summary>
    public class PartSizeAvailability : INotifyPropertyChanged
    {
        private string _displaySize;
        private bool _isAvailable;
        private bool _fitsInSpace = true;

        public string DisplaySize
        {
            get => _displaySize;
            set { _displaySize = value; OnPropertyChanged(nameof(DisplaySize)); }
        }

        public bool IsAvailable
        {
            get => _isAvailable;
            set { _isAvailable = value; OnPropertyChanged(nameof(IsAvailable)); }
        }

        public bool FitsInSpace
        {
            get => _fitsInSpace;
            set { _fitsInSpace = value; OnPropertyChanged(nameof(FitsInSpace)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Модель строки для группировки по артикулам
    /// </summary>
    public static class DataHelper
    {
        public static string GetArticleName(IEnumerable<string> keys)
        {
            return string.Join(" | ", keys);
        }
    }

    /// <summary>
    /// Модель детали для детализированного вывода результатов.
    /// </summary>
    public class PartItem
    {
        public string Article { get; set; }
        public double Length { get; set; }
        public int Count { get; set; }
        public int OriginalRowIndex { get; set; } = -1;
    }

    /// <summary>
    /// Детализированная модель распила хлыста (с разбивкой на отдельные детали).
    /// </summary>
    public class CutBarDetailed
    {
        public double StockLength { get; set; }
        public double Remainder { get; set; }
        public List<PartItem> Parts { get; set; } = new List<PartItem>();
    }

    /// <summary>
    /// Строка группировки деталей по артикулу для отображения в UI и задания индивидуальных настроек.
    /// </summary>
    public class ArticleGroupingRow : INotifyPropertyChanged
    {
        private string _articleName;
        private string _articleDescription;
        private int _totalCount;
        private double _totalLength;
        private double? _selectedBarLength;
        private PresetModel _selectedPreset;
        private double? _selectedVisibleHeight;
        private bool _isDefaultValue;
        private bool _isManuallyChanged;
        private bool _isBarLengthDefaultValue;
        private bool _isPresetDefaultValue;

        public string ArticleName
        {
            get => _articleName;
            set { _articleName = value; OnPropertyChanged(nameof(ArticleName)); }
        }

        public string ArticleDescription
        {
            get => _articleDescription;
            set { _articleDescription = value; OnPropertyChanged(nameof(ArticleDescription)); }
        }

        public int TotalCount
        {
            get => _totalCount;
            set { _totalCount = value; OnPropertyChanged(nameof(TotalCount)); }
        }

        public double TotalLength
        {
            get => _totalLength;
            set { _totalLength = value; OnPropertyChanged(nameof(TotalLength)); }
        }

        public double? SelectedBarLength
        {
            get => _selectedBarLength;
            set { _selectedBarLength = value; OnPropertyChanged(nameof(SelectedBarLength)); }
        }

        public PresetModel SelectedPreset
        {
            get => _selectedPreset;
            set { _selectedPreset = value; OnPropertyChanged(nameof(SelectedPreset)); }
        }

        public double? SelectedVisibleHeight
        {
            get => _selectedVisibleHeight;
            set { _selectedVisibleHeight = value; OnPropertyChanged(nameof(SelectedVisibleHeight)); }
        }

        public bool IsDefaultValue
        {
            get => _isDefaultValue;
            set { _isDefaultValue = value; OnPropertyChanged(nameof(IsDefaultValue)); }
        }

        public bool IsManuallyChanged
        {
            get => _isManuallyChanged;
            set { _isManuallyChanged = value; OnPropertyChanged(nameof(IsManuallyChanged)); }
        }

        public bool IsBarLengthDefaultValue
        {
            get => _isBarLengthDefaultValue;
            set { _isBarLengthDefaultValue = value; OnPropertyChanged(nameof(IsBarLengthDefaultValue)); }
        }

        public bool IsPresetDefaultValue
        {
            get => _isPresetDefaultValue;
            set { _isPresetDefaultValue = value; OnPropertyChanged(nameof(IsPresetDefaultValue)); }
        }

        private double? _displayDefaultBarLength;
        private string _displayDefaultPresetName;

        public double? DisplayDefaultBarLength
        {
            get => _displayDefaultBarLength;
            set { _displayDefaultBarLength = value; OnPropertyChanged(nameof(DisplayDefaultBarLength)); }
        }

        public string DisplayDefaultPresetName
        {
            get => _displayDefaultPresetName;
            set { _displayDefaultPresetName = value; OnPropertyChanged(nameof(DisplayDefaultPresetName)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}