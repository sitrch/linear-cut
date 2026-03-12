using System.Windows.Input;
using LinearCutOptimization.Wpf.Mvvm;

namespace LinearCutOptimization.Wpf.ViewModels
{
    public partial class MainViewModel
    {
        public ICommand OpenFileCommand { get; private set; }
        public ICommand ImportProjectCommand { get; private set; }
        public ICommand ExportProjectCommand { get; private set; }
        public ICommand RunOptimizationCommand { get; private set; }

        private void InitCommands()
        {
            OpenFileCommand = new RelayCommand(_ => OpenExcelFile());

            // Заглушки (чтобы XAML не падал). Реализацию можно добавить позже.
            ImportProjectCommand = new RelayCommand(_ => { });
            ExportProjectCommand = new RelayCommand(_ => { });
            RunOptimizationCommand = new RelayCommand(_ => RunOptimization());
        }
    }
}