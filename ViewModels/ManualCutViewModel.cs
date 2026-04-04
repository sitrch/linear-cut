using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using LinearCutWpf.Models;

namespace LinearCutWpf.ViewModels
{
    /// <summary>
    /// ViewModel для управления ручным раскроем.
    /// Отвечает за логику добавления/удаления строк ручного раскроя, 
    /// расчет доступного места на хлысте, валидацию введенных данных
    /// и учет использованных деталей из списка доступных.
    /// </summary>
    public class ManualCutViewModel : ViewModelBase
    {
        private ObservableCollection<ManualCutRow> _manualCuts;
        private double _barLength;
        private PresetModel _currentPreset;
        private Dictionary<double, int> _availableParts;
        private Dictionary<double, int> _usedParts;
        private ObservableCollection<PartSizeAvailability> _availableSizes;
        private List<double> _availableBarLengths;
        private string _errorsText;
        private bool _hasErrorsText;

        public ObservableCollection<ManualCutRow> ManualCuts
        {
            get => _manualCuts;
            set
            {
                if (_manualCuts != null)
                {
                    _manualCuts.CollectionChanged -= OnManualCutsCollectionChanged;
                    foreach (var item in _manualCuts)
                        item.PropertyChanged -= OnRowPropertyChanged;
                }
                
                SetProperty(ref _manualCuts, value);
                
                if (_manualCuts != null)
                {
                    _manualCuts.CollectionChanged += OnManualCutsCollectionChanged;
                    foreach (var item in _manualCuts)
                        item.PropertyChanged += OnRowPropertyChanged;
                }
                
                RefreshValidation();
            }
        }

        public ObservableCollection<PartSizeAvailability> AvailableSizes
        {
            get => _availableSizes;
            set => SetProperty(ref _availableSizes, value);
        }

        public List<double> BarLengths
        {
            get => _availableBarLengths;
            set => SetProperty(ref _availableBarLengths, value);
        }

        public PresetModel CurrentPreset
        {
            get => _currentPreset;
            set
            {
                if (SetProperty(ref _currentPreset, value))
                {
                    RefreshValidation();
                }
            }
        }

        public double BarLength
        {
            get => _barLength;
            set
            {
                if (SetProperty(ref _barLength, value))
                {
                    RefreshValidation();
                }
            }
        }

        public string ErrorsText
        {
            get => _errorsText;
            set
            {
                if (SetProperty(ref _errorsText, value))
                {
                    HasErrorsText = !string.IsNullOrEmpty(value);
                }
            }
        }

        public bool HasErrorsText
        {
            get => _hasErrorsText;
            private set => SetProperty(ref _hasErrorsText, value);
        }

        /// <summary>Команда добавления новой строки ручного раскроя.</summary>
        public ICommand AddRowCommand { get; }
        
        /// <summary>Команда удаления строки ручного раскроя.</summary>
        public ICommand DeleteRowCommand { get; }

        /// <summary>
        /// Инициализирует новый экземпляр ManualCutViewModel, подготавливает коллекции и команды.
        /// </summary>
        public ManualCutViewModel()
        {
            _manualCuts = new ObservableCollection<ManualCutRow>();
            _usedParts = new Dictionary<double, int>();
            _availableParts = new Dictionary<double, int>();
            _availableSizes = new ObservableCollection<PartSizeAvailability>();
            _availableBarLengths = new List<double>();
            
            ManualCuts.CollectionChanged += OnManualCutsCollectionChanged;

            AddRowCommand = new RelayCommand(AddRow);
            DeleteRowCommand = new RelayCommand(DeleteRow);
        }

        private void DeleteRow(object parameter)
        {
            if (parameter is ManualCutRow row && ManualCuts != null)
            {
                ManualCuts.Remove(row);
                RefreshValidation();
            }
        }

        private void AddRow(object parameter)
        {
            var newRow = new ManualCutRow
            {
                BarLength = BarLength > 0 ? BarLength : (double?)null,
                Count = 1
            };
            
            // Инициализируем коллекции доступных размеров
            if (AvailableSizes != null)
            {
                foreach (var size in AvailableSizes)
                {
                    newRow.AvailableSizes1.Add(new PartSizeAvailability { DisplaySize = size.DisplaySize, IsAvailable = size.IsAvailable });
                    newRow.AvailableSizes2.Add(new PartSizeAvailability { DisplaySize = size.DisplaySize, IsAvailable = size.IsAvailable });
                    newRow.AvailableSizes3.Add(new PartSizeAvailability { DisplaySize = size.DisplaySize, IsAvailable = size.IsAvailable });
                    newRow.AvailableSizes4.Add(new PartSizeAvailability { DisplaySize = size.DisplaySize, IsAvailable = size.IsAvailable });
                }
            }

            ManualCuts.Add(newRow);
        }

        /// <summary>
        /// Инициализирует данные ViewModel при открытии окна или смене артикула/пресета.
        /// Загружает список доступных деталей и текущие настройки.
        /// </summary>
        /// <param name="manualCuts">Коллекция существующих строк ручного раскроя.</param>
        /// <param name="barLength">Длина хлыста по умолчанию.</param>
        /// <param name="currentPreset">Текущий выбранный пресет (настройки пропилов/торцевания).</param>
        /// <param name="availableSizes">Список уникальных размеров деталей.</param>
        /// <param name="availableBarLengths">Список доступных длин хлыстов.</param>
        /// <param name="itemSizes">Список всех размеров (из таблицы деталей).</param>
        /// <param name="itemQuantities">Список количеств соответствующих деталей.</param>
        public void Initialize(ObservableCollection<ManualCutRow> manualCuts,
            double barLength, PresetModel currentPreset,
            List<string> availableSizes, List<double> availableBarLengths,
            List<string> itemSizes = null, List<int> itemQuantities = null)
        {
            System.IO.File.AppendAllText("debug_log.txt", $"\n[{DateTime.Now}] Initialize started. itemSizes count: {itemSizes?.Count ?? 0}, itemQuantities count: {itemQuantities?.Count ?? 0}\n");
            BarLength = barLength;
            CurrentPreset = currentPreset;
            BarLengths = availableBarLengths ?? new List<double>();
            
            if (itemSizes != null && itemQuantities != null)
            {
                _availableParts.Clear();
                for (int i = 0; i < itemSizes.Count; i++)
                {
                    if (double.TryParse(itemSizes[i], out double size))
                    {
                        int qty = i < itemQuantities.Count ? itemQuantities[i] : 1;
                        if (_availableParts.ContainsKey(size))
                            _availableParts[size] += qty;
                        else
                            _availableParts[size] = qty;
                    }
                    else
                    {
                        System.IO.File.AppendAllText("debug_log.txt", $"[{DateTime.Now}] Failed to parse itemSize: {itemSizes[i]}\n");
                    }
                }
                System.IO.File.AppendAllText("debug_log.txt", $"[{DateTime.Now}] _availableParts populated with {_availableParts.Count} unique sizes.\n");
            }
            else
            {
                _availableParts.Clear();
                System.IO.File.AppendAllText("debug_log.txt", $"[{DateTime.Now}] itemSizes or itemQuantities is null.\n");
            }

            // Инициализируем глобальную коллекцию перед загрузкой строк
            UpdateAvailableSizesBase();

            ManualCuts = manualCuts;
            
            RefreshValidation();
        }

        private void UpdateAvailableSizesBase()
        {
            if (AvailableSizes == null)
            {
                AvailableSizes = new ObservableCollection<PartSizeAvailability>();
            }

            var allSizes = _availableParts.Keys.OrderByDescending(s => s).ToList();

            // Удаляем те, которых больше нет в _availableParts
            for (int i = AvailableSizes.Count - 1; i >= 0; i--)
            {
                if (!double.TryParse(AvailableSizes[i].DisplaySize, out double size) || !_availableParts.ContainsKey(size))
                {
                    AvailableSizes.RemoveAt(i);
                }
            }

            // Добавляем новые и обновляем состояние существующих
            foreach (var size in allSizes)
            {
                string sizeStr = size.ToString();
                int availableCount = _availableParts[size];
                int usedCount = _usedParts.ContainsKey(size) ? _usedParts[size] : 0;
                bool isAvailable = usedCount < availableCount;

                var existingItem = AvailableSizes.FirstOrDefault(x => x.DisplaySize == sizeStr);
                if (existingItem != null)
                {
                    existingItem.IsAvailable = isAvailable;
                }
                else
                {
                    // Вставляем с учетом сортировки по убыванию (просто добавляем в правильное место)
                    var newItem = new PartSizeAvailability
                    {
                        DisplaySize = sizeStr,
                        IsAvailable = isAvailable
                    };
                    
                    int insertIndex = 0;
                    while (insertIndex < AvailableSizes.Count)
                    {
                        if (double.TryParse(AvailableSizes[insertIndex].DisplaySize, out double existingSize) && existingSize < size)
                        {
                            break;
                        }
                        insertIndex++;
                    }
                    AvailableSizes.Insert(insertIndex, newItem);
                }
            }
        }

        private void OnManualCutsCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (ManualCutRow item in e.NewItems)
                    item.PropertyChanged += OnRowPropertyChanged;
            }
            if (e.OldItems != null)
            {
                foreach (ManualCutRow item in e.OldItems)
                    item.PropertyChanged -= OnRowPropertyChanged;
            }
            RefreshValidation();
        }

        private void OnRowPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ManualCutRow.BarLength) ||
                e.PropertyName == nameof(ManualCutRow.Size1) ||
                e.PropertyName == nameof(ManualCutRow.Size2) ||
                e.PropertyName == nameof(ManualCutRow.Size3) ||
                e.PropertyName == nameof(ManualCutRow.Size4) ||
                e.PropertyName == nameof(ManualCutRow.Count) ||
                e.PropertyName == nameof(ManualCutRow.UseRemainders))
            {
                RefreshValidation();
            }
        }

        /// <summary>
        /// Пересчитывает общее количество доступных деталей на основе входных списков размеров и количеств.
        /// </summary>
        /// <param name="sizes">Список размеров деталей.</param>
        /// <param name="quantities">Список количеств деталей.</param>
        public void CalculateAvailableParts(List<string> sizes, List<int> quantities)
        {
            _availableParts.Clear();
            
            for (int i = 0; i < sizes.Count; i++)
            {
                if (double.TryParse(sizes[i], out double size))
                {
                    int qty = i < quantities.Count ? quantities[i] : 1;
                    if (_availableParts.ContainsKey(size))
                        _availableParts[size] += qty;
                    else
                        _availableParts[size] = qty;
                }
            }
            RefreshValidation();
        }

        private void UpdateAvailableSizes()
        {
            UpdateAvailableSizesBase();
        }

        private void CalculateUsedParts()
        {
            _usedParts.Clear();

            if (CurrentPreset == null || ManualCuts == null) return;

            foreach (var row in ManualCuts)
            {
                int count = row.Count;
                string[] sizes = { row.Size1, row.Size2, row.Size3, row.Size4 };
                
                foreach (var sizeStr in sizes)
                {
                    if (!string.IsNullOrEmpty(sizeStr) && double.TryParse(sizeStr, out double size) && size > 0)
                    {
                        if (_usedParts.ContainsKey(size))
                            _usedParts[size] += count;
                        else
                            _usedParts[size] = count;
                    }
                }
            }
        }

        /// <summary>
        /// Подготавливает список доступных размеров для конкретного выпадающего списка в строке.
        /// Рассчитывает оставшееся место на хлысте с учетом уже выбранных в этой строке деталей и пропилов.
        /// </summary>
        /// <param name="row">Текущая строка ручного раскроя.</param>
        /// <param name="columnIndex">Индекс колонки (1-4), для которой открывается список.</param>
        public void PrepareAvailableSizesForDropdown(ManualCutRow row, int columnIndex)
        {
            System.IO.File.AppendAllText("debug_log.txt", $"[{DateTime.Now}] PrepareAvailableSizesForDropdown started for column {columnIndex}. CurrentPreset is null: {CurrentPreset == null}\n");
            
            if (CurrentPreset == null) return;

            if (_availableParts.Count == 0)
            {
                System.IO.File.AppendAllText("debug_log.txt", $"[{DateTime.Now}] PrepareAvailableSizesForDropdown: _availableParts is EMPTY.\n");
            }
            else
            {
                System.IO.File.AppendAllText("debug_log.txt", $"[{DateTime.Now}] PrepareAvailableSizesForDropdown: _availableParts has {_availableParts.Count} keys.\n");
            }

            // 1. Актуализируем глобальную занятость (все строки)
            CalculateUsedParts();
            UpdateAvailableSizesBase();

            // 2. Считаем, сколько места доступно в текущем хлысте
            double availableSpace = double.MaxValue;
            if (row.BarLength.HasValue)
            {
                double stock = row.BarLength.Value;
                double maxAvailable = stock - CurrentPreset.TrimStart - CurrentPreset.TrimEnd;

                string[] sizes = { row.Size1, row.Size2, row.Size3, row.Size4 };
                double usedByOthers = 0;
                int othersCount = 0;

                for (int j = 0; j < 4; j++)
                {
                    if (j == columnIndex - 1) continue; // Пропускаем текущую (открытую) колонку

                    if (!string.IsNullOrEmpty(sizes[j]) && double.TryParse(sizes[j], out double val) && val > 0)
                    {
                        usedByOthers += val;
                        othersCount++;
                    }
                }

                int assumedCuts = othersCount > 0 ? othersCount : 0;
                
                // Место, оставшееся под эту деталь
                double spaceForThis = maxAvailable - usedByOthers - (othersCount * CurrentPreset.CutWidth);
                spaceForThis -= CurrentPreset.CutWidth; // Вычитаем пропил для добавляемой детали
                
                availableSpace = spaceForThis > 0 ? spaceForThis : 0;
            }

            // 3. Обновляем конкретную коллекцию, к которой сейчас идет привязка
            ObservableCollection<PartSizeAvailability> targetCollection = null;
            switch (columnIndex)
            {
                case 1: targetCollection = row.AvailableSizes1; row.AvailableSpaceForSize1 = availableSpace; break;
                case 2: targetCollection = row.AvailableSizes2; row.AvailableSpaceForSize2 = availableSpace; break;
                case 3: targetCollection = row.AvailableSizes3; row.AvailableSpaceForSize3 = availableSpace; break;
                case 4: targetCollection = row.AvailableSizes4; row.AvailableSpaceForSize4 = availableSpace; break;
            }

            if (targetCollection != null)
            {
                UpdateSpecificCollection(targetCollection, availableSpace);
            }
        }

        /// <summary>
        /// Выполняет полную валидацию всех строк ручного раскроя.
        /// Проверяет превышение длины хлыста и превышение доступного количества деталей.
        /// Обновляет флаги ошибок в моделях строк и формирует общее сообщение об ошибке.
        /// </summary>
        public void RefreshValidation()
        {
            if (CurrentPreset == null || ManualCuts == null) return;

            CalculateUsedParts();
            UpdateAvailableSizes();

            foreach (var row in ManualCuts)
            {
                row.HasLengthError = false;
                row.HasCountError = false;
                row.HasLengthErrorBarLength = false;
                row.HasCountErrorCount = false;
                row.HasLengthErrorSize1 = false;
                row.HasLengthErrorSize2 = false;
                row.HasLengthErrorSize3 = false;
                row.HasLengthErrorSize4 = false;
                row.HasCountErrorSize1 = false;
                row.HasCountErrorSize2 = false;
                row.HasCountErrorSize3 = false;
                row.HasCountErrorSize4 = false;
            }

            foreach (var row in ManualCuts)
            {
                if (!row.BarLength.HasValue) 
                {
                    // Если хлыст не задан, доступно всё пространство (бесконечность)
                    row.AvailableSpaceForSize1 = double.MaxValue;
                    row.AvailableSpaceForSize2 = double.MaxValue;
                    row.AvailableSpaceForSize3 = double.MaxValue;
                    row.AvailableSpaceForSize4 = double.MaxValue;
                    UpdateRowAvailableSizes(row);
                    continue;
                }
                
                double stock = row.BarLength.Value;
                double maxAvailable = stock - CurrentPreset.TrimStart - CurrentPreset.TrimEnd;
                
                string[] sizes = { row.Size1, row.Size2, row.Size3, row.Size4 };
                double[] parsedSizes = new double[4];
                int validSizesCount = 0;
                
                // Сначала парсим все размеры и считаем общую длину занятого места без пропилов
                for (int i = 0; i < sizes.Length; i++)
                {
                    if (!string.IsNullOrEmpty(sizes[i]) && double.TryParse(sizes[i], out double val) && val > 0)
                    {
                        parsedSizes[i] = val;
                        validSizesCount++;
                    }
                }

                bool rowHasLengthError = false;
                bool rowHasCountError = false;
                double currentUsedSpace = 0;
                
                for (int i = 0; i < sizes.Length; i++)
                {
                    // Рассчитываем доступное место ДЛЯ ЭТОГО комбобокса.
                    // Считаем сумму остальных УЖЕ введенных деталей.
                    double usedByOthers = 0;
                    int othersCount = 0;
                    for (int j = 0; j < parsedSizes.Length; j++)
                    {
                        if (i != j && parsedSizes[j] > 0)
                        {
                            usedByOthers += parsedSizes[j];
                            othersCount++;
                        }
                    }
                    
                    // Сколько пропилов понадобится, если мы добавим еще одну деталь (текущую)?
                    // Это количество других деталей + 1 (для этой)
                    int assumedCuts = othersCount > 0 ? othersCount : 0; // если деталь одна, пропил перед ней не нужен, но логика раскроя требует 1 пропил на КАЖДУЮ деталь, как реализовано ниже: used + needed > available
                    
                    // Рассчитываем сколько места останется для текущей ячейки
                    // В старом коде на КАЖДУЮ деталь накидывался CutWidth.
                    double spaceForThis = maxAvailable - usedByOthers - (othersCount * CurrentPreset.CutWidth);
                    // Вычитаем пропил для самой этой детали (т.к. старый код проверяет used + (val + CutWidth) > available)
                    spaceForThis -= CurrentPreset.CutWidth;
                    
                    switch (i)
                    {
                        case 0: row.AvailableSpaceForSize1 = spaceForThis > 0 ? spaceForThis : 0; break;
                        case 1: row.AvailableSpaceForSize2 = spaceForThis > 0 ? spaceForThis : 0; break;
                        case 2: row.AvailableSpaceForSize3 = spaceForThis > 0 ? spaceForThis : 0; break;
                        case 3: row.AvailableSpaceForSize4 = spaceForThis > 0 ? spaceForThis : 0; break;
                    }

                    // Старая логика проверки ошибок
                    if (parsedSizes[i] > 0)
                    {
                        double val = parsedSizes[i];
                        if (_availableParts.ContainsKey(val))
                        {
                            int availableCount = _availableParts[val];
                            int usedCount = _usedParts.ContainsKey(val) ? _usedParts[val] : 0;
                            if (usedCount > availableCount)
                            {
                                row.HasCountError = true;
                                rowHasCountError = true;
                                SetCountErrorSize(row, i, true);
                            }
                        }

                        double needed = val + CurrentPreset.CutWidth;
                        if (currentUsedSpace + needed > maxAvailable)
                        {
                            row.HasLengthError = true;
                            rowHasLengthError = true;
                            SetLengthErrorSize(row, i, true);
                        }
                        else
                        {
                            currentUsedSpace += needed;
                        }
                    }
                }

                if (rowHasLengthError)
                    row.HasLengthErrorBarLength = true;
                if (rowHasCountError)
                    row.HasCountErrorCount = true;
                
                // Больше не заполняем списки заранее (это теперь делается по клику `PrepareAvailableSizesForDropdown`),
                // но нам надо проверить, если уже что-то выбрано, покрасить фон если нет в наличии.
                // Для простоты, мы можем оставить UpdateRowAvailableSizes чтобы он покрасил уже выбранные элементы, 
                // если выпадающий список закрыт.
                UpdateRowAvailableSizes(row);
            }

            bool hasAnyErrors = ManualCuts.Any(r => r.HasLengthError || r.HasCountError);
            
            if (hasAnyErrors)
            {
                var errorMessages = new List<string>();
                if (ManualCuts.Any(r => r.HasLengthError))
                    errorMessages.Add("Превышена длина хлыста");
                if (ManualCuts.Any(r => r.HasCountError))
                    errorMessages.Add("Превышено количество деталей");
                ErrorsText = string.Join("; ", errorMessages);
            }
            else
            {
                ErrorsText = string.Empty;
            }
        }

        private void SetLengthErrorSize(ManualCutRow row, int index, bool value)
        {
            switch (index)
            {
                case 0: row.HasLengthErrorSize1 = value; break;
                case 1: row.HasLengthErrorSize2 = value; break;
                case 2: row.HasLengthErrorSize3 = value; break;
                case 3: row.HasLengthErrorSize4 = value; break;
            }
        }

        private void SetCountErrorSize(ManualCutRow row, int index, bool value)
        {
            switch (index)
            {
                case 0: row.HasCountErrorSize1 = value; break;
                case 1: row.HasCountErrorSize2 = value; break;
                case 2: row.HasCountErrorSize3 = value; break;
                case 3: row.HasCountErrorSize4 = value; break;
            }
        }

        private void UpdateRowAvailableSizes(ManualCutRow row)
        {
            if (AvailableSizes == null) return;

            UpdateSpecificCollection(row.AvailableSizes1, row.AvailableSpaceForSize1);
            UpdateSpecificCollection(row.AvailableSizes2, row.AvailableSpaceForSize2);
            UpdateSpecificCollection(row.AvailableSizes3, row.AvailableSpaceForSize3);
            UpdateSpecificCollection(row.AvailableSizes4, row.AvailableSpaceForSize4);
        }

        private void UpdateSpecificCollection(ObservableCollection<PartSizeAvailability> collection, double availableSpace)
        {
            if (collection == null) return;

            var allSizes = _availableParts.Keys.OrderByDescending(s => s).ToList();

            // Удаляем те, которых больше нет в _availableParts
            for (int i = collection.Count - 1; i >= 0; i--)
            {
                if (!double.TryParse(collection[i].DisplaySize, out double size) || !_availableParts.ContainsKey(size))
                {
                    collection.RemoveAt(i);
                }
            }

            // Добавляем новые и обновляем существующие
            foreach (var size in allSizes)
            {
                string sizeStr = size.ToString();
                int availableCount = _availableParts[size];
                int usedCount = _usedParts.ContainsKey(size) ? _usedParts[size] : 0;
                bool isAvailable = usedCount < availableCount;
                bool fits = size <= availableSpace;

                var existingItem = collection.FirstOrDefault(x => x.DisplaySize == sizeStr);
                
                if (existingItem != null)
                {
                    existingItem.IsAvailable = isAvailable;
                    existingItem.FitsInSpace = fits;
                }
                else
                {
                    var newItem = new PartSizeAvailability
                    {
                        DisplaySize = sizeStr,
                        IsAvailable = isAvailable,
                        FitsInSpace = fits
                    };
                    
                    int insertIndex = 0;
                    while (insertIndex < collection.Count)
                    {
                        if (double.TryParse(collection[insertIndex].DisplaySize, out double existingSize) && existingSize < size)
                        {
                            break;
                        }
                        insertIndex++;
                    }
                    collection.Insert(insertIndex, newItem);
                }
            }
        }
    }
}
