using System.Windows;
using LinearCutOptimization.Wpf.ViewModels;

namespace LinearCutOptimization.Wpf
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
    }
}