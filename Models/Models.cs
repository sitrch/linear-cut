using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace LinearCutWpf.Models
{
    public class CutSettings
    {
        public double TrimStart { get; set; }
        public double TrimEnd { get; set; }
        public double CutWidth { get; set; }
        public Dictionary<string, List<double>> ItemStocks { get; set; } = new Dictionary<string, List<double>>();
    }

    public class PresetModel
    {
        public string Name { get; set; }
        public double TrimStart { get; set; }
        public double TrimEnd { get; set; }
        public double CutWidth { get; set; }
    }

    public class CutBar
    {
        public double StockLength { get; set; }
        public string Parts { get; set; }
        public double Remainder { get; set; }
    }

    public class StockLengthModel
    {
        public double Length { get; set; }
        public bool IsEnabled { get; set; } = true;
    }

    public class ManualCutRow : INotifyPropertyChanged
    {
        private double? _barLength;
        private string _size1;
        private string _size2;
        private string _size3;
        private string _size4;
        private int _count = 1;
        private bool _hasLengthError;
        private bool _hasCountError;

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

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ArticleSettings
    {
        public string ArticleName { get; set; }
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
    /// Модель строки для группировки по артикулам
    /// </summary>
    public class ArticleGroupingRow : INotifyPropertyChanged
    {
        private string _articleName;
        private int _totalCount;
        private double _totalLength;
        private double? _selectedBarLength;
        private PresetModel _selectedPreset;

        public string ArticleName
        {
            get => _articleName;
            set { _articleName = value; OnPropertyChanged(nameof(ArticleName)); }
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

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}