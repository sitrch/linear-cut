using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows;
using LinearCutWpf.Models;
using LinearCutWpf.Services;

namespace LinearCutWpf.Controls
{
    /// <summary>
    /// Контрол для настройки параметров раскроя по артикулам (материалам).
    /// Позволяет задать специфичную длину хлыста и пресет настроек для каждого артикула.
    /// </summary>
    public partial class ArticleSettingsControl : UserControl
    {
        /// <summary>
        /// Коллекция строк (артикулов) для отображения в таблице настроек.
        /// </summary>
        public ObservableCollection<ArticleGroupingRow> Articles { get; set; } = new ObservableCollection<ArticleGroupingRow>();
        
        /// <summary>
        /// Высота по умолчанию для артикулов.
        /// </summary>
        public double? DefaultVisibleHeight { get; set; }
        public double DefaultBarLength { get; set; }
        public PresetModel DefaultPreset { get; set; }

        // Словарь для отслеживания оригинальных значений высот
        private Dictionary<string, double?> _originalHeights = new Dictionary<string, double?>();
        
        /// <summary>
        /// Коллекция доступных длин хлыстов (базовые настройки).
        /// </summary>
        public ObservableCollection<StockLengthModel> AvailableStockLengths { get; set; } = new ObservableCollection<StockLengthModel>();
        
        /// <summary>
        /// Коллекция доступных пресетов настроек.
        /// </summary>
        public ObservableCollection<PresetModel> AvailablePresets { get; set; } = new ObservableCollection<PresetModel>();

        /// <summary>
        /// Инициализирует новый экземпляр <see cref="ArticleSettingsControl"/>.
        /// </summary>
        public ArticleSettingsControl()
        {
            InitializeComponent();
            dgArticles.ItemsSource = Articles;
        }

        /// <summary>
        /// Обработчик изменения значения высоты по умолчанию.
        /// </summary>
        private void OnDefaultHeightChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                if (string.IsNullOrWhiteSpace(textBox.Text))
                {
                    DefaultVisibleHeight = null;
                    UpdateDefaultHeights();
                    return;
                }

                if (double.TryParse(textBox.Text, out double height) && height >= 0)
                {
                    DefaultVisibleHeight = height;
                    UpdateDefaultHeights();
                }
                else
                {
                    // Восстанавливаем предыдущее значение, если ввод некорректный
                    if (DefaultVisibleHeight.HasValue)
                    {
                        textBox.Text = DefaultVisibleHeight.Value.ToString();
                        textBox.CaretIndex = textBox.Text.Length;
                    }
                }
            }
        }

        /// <summary>
        /// Обработчик получения фокуса ячейкой высоты.
        /// </summary>
        private void OnHeightCellGotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.DataContext is ArticleGroupingRow row)
            {
                // Сохраняем оригинальное значение при первом фокусе
                if (!_originalHeights.ContainsKey(row.ArticleName))
                {
                    _originalHeights[row.ArticleName] = row.SelectedVisibleHeight;
                }
            }
        }

        /// <summary>
        /// Обработчик потери фокуса ячейкой высоты.
        /// </summary>
        private void OnHeightCellLostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.DataContext is ArticleGroupingRow row)
            {
                // ВСЕГДА помечаем как измененное вручную при потере фокуса
                // Это важно для случаев, когда пользователь вручную вводит значение,
                // даже если оно совпадает с оригинальным или значением по умолчанию
                row.IsManuallyChanged = true;
                row.IsDefaultValue = false;
                
                // Старая логика была:
                // if (_originalHeights.TryGetValue(row.ArticleName, out double? originalHeight))
                // {
                //     if (row.SelectedVisibleHeight != originalHeight)
                //     {
                //         row.IsManuallyChanged = true;
                //         row.IsDefaultValue = false;
                //     }
                //     // Не сбрасываем IsManuallyChanged в false, если оно уже было установлено в true
                //     // Это важно для отслеживания ручных изменений, даже если пользователь вернул значение
                // }
                // else
                // {
                //     // Если это новое значение, помечаем как измененное вручную
                //     row.IsManuallyChanged = true;
                //     row.IsDefaultValue = false;
                // }
            }
        }

        /// <summary>
        /// Обработчик изменения текста в ячейке высоты.
        /// </summary>
        private void OnHeightCellTextChanged(object sender, TextChangedEventArgs e)
        {
            // Цвета будут обновляться автоматически через механизм привязки данных
            // OnPropertyChanged вызывается автоматически при изменении SelectedVisibleHeight
        }

        /// <summary>
        /// Обработчик изменения выбора длины хлыста.
        /// </summary>
        private void OnBarLengthCellSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.DataContext is ArticleGroupingRow row)
            {
                if (comboBox.SelectedValue is double length && length > 0)
                {
                    row.IsBarLengthDefaultValue = false;
                    row.IsBarLengthManuallyChanged = true;
                }
                else
                {
                    row.IsBarLengthDefaultValue = true;
                    row.IsBarLengthManuallyChanged = false;
                }
            }
        }

        /// <summary>
        /// Обработчик изменения выбора пресета.
        /// </summary>
        private void OnPresetCellSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.DataContext is ArticleGroupingRow row)
            {
                if (comboBox.SelectedItem is PresetModel preset && !string.IsNullOrEmpty(preset.Name))
                {
                    row.IsPresetDefaultValue = false;
                }
                else
                {
                    row.IsPresetDefaultValue = true;
                }
            }
        }


        /// <summary>
        /// Обновляет значения высот по умолчанию для всех ячеек, которые не были изменены вручную.
        /// </summary>
        private void UpdateDefaultHeights()
        {
            foreach (var row in Articles)
            {
                // Обновляем только те ячейки, которые являются значениями по умолчанию и не изменены вручную
                if (row.IsDefaultValue && !row.IsManuallyChanged)
                {
                    row.SelectedVisibleHeight = DefaultVisibleHeight;
                }
            }
        }

        /// <summary>
        /// Инициализирует таблицу настроек артикулов на основе загруженных данных и глобальных параметров.
        /// Группирует данные по артикулам, подсчитывает общее количество и длину.
        /// </summary>
        /// <param name="dataTable">Таблица с импортированными данными деталей.</param>
        /// <param name="keyCols">Имена столбцов, используемых в качестве ключа (артикула).</param>
        /// <param name="nameCols">Имена столбцов, содержащих описание артикула.</param>
        /// <param name="qtyCols">Имена столбцов, содержащих количество.</param>
        /// <param name="valCols">Имена столбцов, содержащих длину детали.</param>
        /// <param name="stockLengths">Список доступных длин хлыстов.</param>
        /// <param name="presets">Список доступных пресетов настроек раскроя.</param>
        /// <param name="existingSettings">Словарь с ранее заданными настройками артикулов, чтобы восстановить выбор пользователя.</param>
        public void Initialize(DataTable dataTable, List<string> keyCols, List<string> nameCols, List<string> qtyCols, List<string> valCols, ObservableCollection<StockLengthModel> stockLengths, ObservableCollection<PresetModel> presets, Dictionary<string, ArticleSettings> existingSettings, double defaultBarLengthValue = 0, PresetModel defaultPreset = null)
        {
            DefaultBarLength = defaultBarLengthValue;
            DefaultPreset = defaultPreset;

            AvailableStockLengths.Clear();
            AvailableStockLengths.Add(new StockLengthModel { Length = 0 }); // Для пустого значения (по умолчанию)
            foreach (var stock in stockLengths)
            {
                AvailableStockLengths.Add(stock);
            }

            AvailablePresets.Clear();
            AvailablePresets.Add(new PresetModel { Name = "" }); // Для пустого значения (по умолчанию)
            foreach (var preset in presets)
            {
                AvailablePresets.Add(preset);
            }

            Articles.Clear();
            _originalHeights.Clear();

            // Загружаем сохраненные данные о высотах
            var savedHeights = ProfileHeightService.LoadProfileHeightsWithMetadata();
            var defaultHeight = ProfileHeightService.LoadDefaultHeight();
            
            // Устанавливаем высоту по умолчанию
            DefaultVisibleHeight = defaultHeight;
            if (defaultHeight.HasValue)
            {
                txtDefaultHeight.Text = defaultHeight.Value.ToString();
            }

            if (dataTable == null || !keyCols.Any()) return;

            string nameCol = nameCols.FirstOrDefault();
            string qtyCol = qtyCols.FirstOrDefault();
            string valCol = valCols.FirstOrDefault();

            var groups = dataTable.Rows.Cast<DataRow>()
                .GroupBy(r => DataHelper.GetArticleName(keyCols.Select(k => r[k]?.ToString())));

            foreach (var group in groups)
            {
                string articleName = group.Key;
                if (string.IsNullOrWhiteSpace(articleName)) continue;

                string articleDesc = nameCol != null ? group.First()[nameCol]?.ToString() : "";
                
                int totalCount = 0;
                double totalLength = 0;

                foreach (var row in group)
                {
                    int qty = 0;
                    if (qtyCol != null && row[qtyCol] != DBNull.Value && int.TryParse(row[qtyCol].ToString(), out int parsedQty))
                    {
                        qty = parsedQty;
                    }

                    double len = 0;
                    if (valCol != null && row[valCol] != DBNull.Value && double.TryParse(row[valCol].ToString(), out double parsedLen))
                    {
                        len = parsedLen;
                    }

                    totalCount += qty;
                    totalLength += len * qty;
                }

                var rowModel = new ArticleGroupingRow
                {
                    ArticleName = articleName,
                    ArticleDescription = articleDesc,
                    TotalCount = totalCount,
                    TotalLength = totalLength
                };

                // Проверяем, есть ли сохраненные данные для этого артикула
                if (savedHeights.TryGetValue(articleName, out var heightData))
                {
                    rowModel.SelectedVisibleHeight = heightData.height;
                    rowModel.IsDefaultValue = heightData.isDefaultValue;
                    rowModel.IsManuallyChanged = heightData.isManuallyChanged;

                    // Загружаем сохранённую длину хлыста
                    if (heightData.barLength.HasValue && heightData.barLength.Value > 0)
                    {
                        rowModel.SelectedBarLength = heightData.barLength;
                        rowModel.IsBarLengthDefaultValue = false;
                        rowModel.IsBarLengthManuallyChanged = heightData.isBarLengthManuallyChanged;
                    }
                    else
                    {
                        rowModel.IsBarLengthDefaultValue = true;
                        rowModel.IsBarLengthManuallyChanged = false;
                    }
                }
                else
                {
                    // Если данных нет, используем значение по умолчанию
                    rowModel.SelectedVisibleHeight = DefaultVisibleHeight;
                    rowModel.IsDefaultValue = true;
                    rowModel.IsManuallyChanged = false;
                    rowModel.IsBarLengthDefaultValue = true;
                    rowModel.IsBarLengthManuallyChanged = false;
                }

                if (existingSettings != null && existingSettings.TryGetValue(articleName, out var settings))
                {
                    // Если в existingSettings есть BarLength, он имеет приоритет (текущая сессия)
                    if (settings.BarLength.HasValue && settings.BarLength.Value > 0)
                    {
                        rowModel.SelectedBarLength = settings.BarLength;
                        rowModel.IsBarLengthDefaultValue = false;
                        rowModel.IsBarLengthManuallyChanged = true;
                    }
                    if (settings.Preset != null)
                    {
                        rowModel.SelectedPreset = AvailablePresets.FirstOrDefault(p => p.Name == settings.Preset.Name);
                        rowModel.IsPresetDefaultValue = rowModel.SelectedPreset == null || string.IsNullOrEmpty(rowModel.SelectedPreset.Name);
                    }
                    else
                    {
                        rowModel.IsPresetDefaultValue = true;
                    }
                }
                else if (!rowModel.SelectedBarLength.HasValue || rowModel.SelectedBarLength.Value == 0)
                {
                    rowModel.IsBarLengthDefaultValue = true;
                    rowModel.IsPresetDefaultValue = true;
                }

                rowModel.DisplayDefaultBarLength = DefaultBarLength;
                rowModel.DisplayDefaultPresetName = DefaultPreset != null ? DefaultPreset.Name : "";

                Articles.Add(rowModel);
            }
        }

        /// <summary>
        /// Возвращает словарь с настройками, заданными пользователем для артикулов.
        /// Включает только те артикулы, для которых была переопределена длина хлыста или выбран пресет.
        /// </summary>
        /// <returns>Словарь, где ключ - название артикула, значение - объект <see cref="ArticleSettings"/>.</returns>
        public Dictionary<string, ArticleSettings> GetSettings()
        {
            var settings = new Dictionary<string, ArticleSettings>();
            foreach (var item in Articles)
            {
                settings[item.ArticleName] = new ArticleSettings
                {
                    ArticleName = item.ArticleName,
                    ArticleDescription = item.ArticleDescription,
                    VisibleHeight = item.SelectedVisibleHeight,
                    BarLength = item.SelectedBarLength > 0 ? item.SelectedBarLength : null,
                    Preset = !string.IsNullOrEmpty(item.SelectedPreset?.Name) ? item.SelectedPreset : null
                };
            }
            
            // Сохраняем данные о высотах через ProfileHeightService
            ProfileHeightService.SaveProfileHeightsWithMetadata(Articles, DefaultVisibleHeight);
            
            return settings;
        }
    }
}