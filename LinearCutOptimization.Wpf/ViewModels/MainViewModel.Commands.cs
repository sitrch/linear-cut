using LinearCutOptimization.Wpf.Mvvm;

namespace LinearCutOptimization.Wpf.ViewModels
{
    public partial class MainViewModel
    {
        public RelayCommand OpenFileCommand { get; private set; }
        public RelayCommand ImportProjectCommand { get; private set; }
        public RelayCommand ExportProjectCommand { get; private set; }
        public RelayCommand RunOptimizationCommand { get; private set; }

        private void InitCommands()
        {
            OpenFileCommand = new RelayCommand(OpenFile);
            ImportProjectCommand = new RelayCommand(ImportProject);
            ExportProjectCommand = new RelayCommand(ExportProject);
            RunOptimizationCommand = new RelayCommand(RunOptimization);
        }
    }
}