using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using MiniExcelLibs;

namespace LinearCutOptimization.Wpf.ViewModels
{
    public partial class MainViewModel
    {
        private string _currentFilePath;

        public ObservableCollection<string> SheetNames { get; } = new ObservableCollection<string>();

        private string _selectedSheet;
        public string SelectedSheet
        {
            get => _selectedSheet;
            set
            {
                if (Set(ref _selectedSheet, value))
                {
                    if (!string.IsNullOrWhiteSpace(_selectedSheet))
                        LoadDataFromSheet(_selectedSheet);
                }
            }
        }

        private void OpenExcelFile()
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Excel (*.xlsx)|*.xlsx",
                CheckFileExists = true
            };

            if (ofd.ShowDialog() != true)
                return;

            _currentFilePath = ofd.FileName;

            SheetNames.Clear();
            foreach (var s in MiniExcel.GetSheetNames(_currentFilePath))
                SheetNames.Add(s);

            SelectedSheet = SheetNames.FirstOrDefault();
        }

        private void LoadDataFromSheet(string sheetName)
        {
            if (string.IsNullOrWhiteSpace(_currentFilePath))
                return;

            // 1) загрузили исходную таблицу
            _mainDataTable = MiniExcel.QueryAsDataTable(_currentFilePath, useHeaderRow: true, sheetName: sheetName);

            if (_mainDataTable == null)
            {
                MessageBox.Show("Не удалось прочитать Excel.");
                return;
            }

            // 2) построили список ролей колонок
            BuildColumnRolesFromTable(_mainDataTable);

            // 3) посчитали “после фильтра”
            ProcessDataLogic();
        }
    }
}