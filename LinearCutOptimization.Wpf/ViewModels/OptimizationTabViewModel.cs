using System.Data;
using LinearCutOptimization.Wpf.Mvvm;

namespace LinearCutOptimization.Wpf.ViewModels
{
    public class OptimizationTabViewModel : ViewModelBase
    {
        public string Header { get; }
        public DataView ResultView { get; }
        public string Summary { get; }

        public OptimizationTabViewModel(string header, DataTable table, string summary)
        {
            Header = header;
            ResultView = table?.DefaultView;
            Summary = summary;
        }
    }
}