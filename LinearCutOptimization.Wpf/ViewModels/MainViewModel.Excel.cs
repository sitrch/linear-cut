using System;
using System.Collections.ObjectModel;
using System.Data;
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
                if (!Set(ref _selectedSheet, value)) return;
                if (!string.IsNullOrWhiteSpace(_selectedSheet))
                    LoadDataFromSheet(_selectedSheet);
            }
        }

        private void OpenFile()
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Excel files (*.xlsx)|*.xlsx",
                Title = "Открыть Excel файл"
            };

            if (ofd.ShowDialog() != true) return;

            _currentFilePath = ofd.FileName;

            SheetNames.Clear();
            foreach (var s in MiniExcel.GetSheetNames(_currentFilePath))
                SheetNames.Add(s);

            SelectedSheet = SheetNames.FirstOrDefault();
        }

        private void LoadDataFromSheet(string sheetName)
        {
            if (string.IsNullOrWhiteSpace(_currentFilePath)) return;

            try
            {
                _mainDataTable = MiniExcel.QueryAsDataTable(_currentFilePath, useHeaderRow: true, sheetName: sheetName);
                BuildColumnRolesFromTable(_mainDataTable);
                ProcessDataLogic();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка чтения Excel: " + ex.Message);
            }
        }

        private void ImportProject()
        {
            MessageBox.Show("Импорт проекта пока не реализован (заглушка).");
        }

        private void ExportProject()
        {
            MessageBox.Show("Экспорт пр��екта пока не реализован (заглушка).");
        }
    }
}