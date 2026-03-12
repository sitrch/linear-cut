using System.Collections.ObjectModel;
using System.Linq;
using LinearCutOptimization.Wpf.Mvvm;
using LinearCutOptimization.Wpf.Models;

namespace LinearCutOptimization.Wpf.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        private readonly CuttingService _cuttingService = new CuttingService();

        public ObservableCollection<PresetModel> Presets { get; } = new ObservableCollection<PresetModel>();
        public ObservableCollection<StockModel> Stocks { get; } = new ObservableCollection<StockModel>();
        public ObservableCollection<ManualCutRow> ManualCuts { get; } = new ObservableCollection<ManualCutRow>();

        public ObservableCollection<ColumnRoleRow> ColumnRoles { get; } = new ObservableCollection<ColumnRoleRow>();

        public ObservableCollection<OptimizationTabViewModel> ResultTabs { get; } = new ObservableCollection<OptimizationTabViewModel>();

        private PresetModel _selectedPreset;
        public PresetModel SelectedPreset { get => _selectedPreset; set => Set(ref _selectedPreset, value); }

        public MainViewModel()
        {
            InitCommands();
            LoadInitialData();
        }

        private void LoadInitialData()
        {
            foreach (var p in CutSettingsProvider.LoadAll())
                Presets.Add(p);

            SelectedPreset = Presets.FirstOrDefault();

            Stocks.Add(new StockModel { Length = 6000, IsEnabled = true });
        }
    }
}