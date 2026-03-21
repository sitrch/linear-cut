using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Collections.ObjectModel;
using System.Linq;
using LinearCutWpf.Controls;
using LinearCutWpf.Models;

namespace LinearCutWpf
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            // Подписываемся на событие изменения настроек
            dataSettingsControl.SettingsChanged += OnSettingsChanged;
            
            // Подписываемся на событие загрузки данных из Excel
            dataSettingsControl.DataLoaded += OnDataLoaded;

            // Подписываемся на событие применения настроек из GroupingControl
            groupingControl.SettingsApplied += OnGroupingSettingsApplied;
        }

        private void OnDataLoaded(object sender, System.EventArgs e)
        {
            // Инициализируем GroupingControl и заполняем вкладки
            if (groupingControl == null || dataSettingsControl.MainDataTable == null) return;

            groupingControl.Initialize(
                dataSettingsControl.DefaultBarLength,
                dataSettingsControl.DefaultPreset,
                new ObservableCollection<StockLengthModel>(dataSettingsControl.StockLengths),
                dataSettingsControl.Presets,
                dataSettingsControl.MainDataTable,
                dataSettingsControl.GetCheckedCols
            );

            groupingControl.RunGroupingWithTabs();
        }

        private void OnSettingsChanged(object sender, System.EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("OnSettingsChanged called");
            
            // Находим TabControl
            var tabControl = (TabControl)FindName("tabControl");
            if (tabControl == null) return;

            // Находим TabItem "Данные" (первый таб)
            if (tabControl.Items.Count > 0 && tabControl.Items[0] is TabItem tabItem)
            {
                // Меняем цвет таба на розовый
                tabItem.Background = new SolidColorBrush(Color.FromRgb(255, 182, 193)); // LightPink
            }

            // Обновляем вкладку группировки при изменении настроек столбцов
            UpdateGrouping();
        }

        private void UpdateGrouping()
        {
            System.Diagnostics.Debug.WriteLine($"UpdateGrouping called: MainDataTable={dataSettingsControl.MainDataTable != null}");
            
            if (groupingControl == null || dataSettingsControl.MainDataTable == null) return;

            groupingControl.Initialize(
                dataSettingsControl.DefaultBarLength,
                dataSettingsControl.DefaultPreset,
                new ObservableCollection<StockLengthModel>(dataSettingsControl.StockLengths),
                dataSettingsControl.Presets,
                dataSettingsControl.MainDataTable,
                dataSettingsControl.GetCheckedCols
            );

            groupingControl.RunGroupingWithTabs();
        }

        private void OnGroupingSettingsApplied(object sender, System.Collections.Generic.Dictionary<string, ArticleSettings> settings)
        {
            // Обновляем настройки в DataSettingsControl
            dataSettingsControl.UpdateArticleSettings(settings);
        }
    }
}
