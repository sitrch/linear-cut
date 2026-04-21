using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace LinearCutWpf.Controls
{
    /// <summary>
    /// Аргументы события изменения роли столбца.
    /// </summary>
    public class RoleChangedEventArgs : EventArgs
    {
        public string ColumnName { get; }
        public string RoleKey { get; }
        public bool IsChecked { get; }

        public RoleChangedEventArgs(string columnName, string roleKey, bool isChecked)
        {
            ColumnName = columnName;
            RoleKey = roleKey;
            IsChecked = isChecked;
        }
    }

    /// <summary>
    /// Выпадающий селектор ролей столбца с чекбоксами внутри.
    /// Поведение по умолчанию — RadioButton (одна роль на столбец), но Key может быть назначен нескольким столбцам независимо.
    /// </summary>
    public partial class ColumnRoleSelector : UserControl
    {
        public static readonly DependencyProperty DisplayTextProperty =
            DependencyProperty.Register("DisplayText", typeof(string), typeof(ColumnRoleSelector),
                new PropertyMetadata("Выбрать..."));

        public string DisplayText
        {
            get => (string)GetValue(DisplayTextProperty);
            set => SetValue(DisplayTextProperty, value);
        }

        private string _columnName;
        /// <summary>
        /// Имя столбца, к которому привязан селектор.
        /// </summary>
        public string ColumnName
        {
            get => _columnName;
            set
            {
                _columnName = value;
                if (tbColumnName != null)
                    tbColumnName.Text = value ?? string.Empty;
            }
        }

        private readonly Dictionary<string, CheckBox> _roleCheckboxes = new Dictionary<string, CheckBox>();
        private readonly HashSet<CheckBox> _suppressEvents = new HashSet<CheckBox>();

        /// <summary>
        /// Событие, вызываемое при изменении выбора роли.
        /// </summary>
        public event EventHandler<RoleChangedEventArgs> RoleChanged;

        public ColumnRoleSelector()
        {
            InitializeComponent();
            BuildRoleItems();
            btnToggle.Click += OnToggleClick;
            popup.Closed += OnPopupClosed;
            // Кнопка всегда показывает ▼, имя колонки — в tbColumnName
            tbColumnName.Text = _columnName ?? string.Empty;
            btnToggle.Content = "▼";
        }

        private void BuildRoleItems()
        {
            var roles = new[]
            {
                ("Key", "IsKey"),
                ("Наименование", "IsName"),
                ("Value", "IsVal"),
                ("Quantity", "IsQty"),
                ("Левый угол", "IsLeftAngle"),
                ("Правый угол", "IsRightAngle"),
                ("Цвет", "IsColor")
            };

            foreach (var (display, key) in roles)
            {
                var cb = new CheckBox { Content = display, Tag = key, Margin = new Thickness(4, 2, 4, 2) };
                cb.Checked += OnRoleChecked;
                cb.Unchecked += OnRoleUnchecked;
                _roleCheckboxes[key] = cb;
                itemsPanel.Children.Add(cb);
            }
        }

        /// <summary>
        /// Устанавливает состояние чекбокса для указанной роли.
        /// </summary>
        public void SetRoleChecked(string roleKey, bool isChecked)
        {
            if (_roleCheckboxes.TryGetValue(roleKey, out var cb))
            {
                _suppressEvents.Add(cb);
                cb.IsChecked = isChecked;
                _suppressEvents.Remove(cb);
            }
            UpdateDisplayText();
        }

        /// <summary>
        /// Возвращает ключ текущей выбранной роли или null, если ничего не выбрано.
        /// </summary>
        public string GetCheckedRole()
        {
            var kvp = _roleCheckboxes.FirstOrDefault(x => x.Value.IsChecked == true);
            return kvp.Key;
        }

        private void OnToggleClick(object sender, RoutedEventArgs e)
        {
            popup.IsOpen = !popup.IsOpen;
        }

        private void OnPopupClosed(object sender, EventArgs e)
        {
            btnToggle.IsChecked = false;
        }

        private void OnRoleChecked(object sender, RoutedEventArgs e)
        {
            var cb = (CheckBox)sender;
            if (_suppressEvents.Contains(cb)) return;

            string roleKey = (string)cb.Tag;

            // RadioButton-поведение: снимаем остальные НЕ-Key роли у этого столбца
            // IsKey может сосуществовать с любой другой ролью
            if (roleKey != "IsKey")
            {
                var uncheckedRoles = new List<string>();
                foreach (var kvp in _roleCheckboxes)
                {
                    if (kvp.Key != roleKey && kvp.Key != "IsKey" && kvp.Value.IsChecked == true)
                    {
                        _suppressEvents.Add(kvp.Value);
                        kvp.Value.IsChecked = false;
                        _suppressEvents.Remove(kvp.Value);
                        uncheckedRoles.Add(kvp.Key);
                    }
                }

                UpdateDisplayText();

                foreach (var ur in uncheckedRoles)
                {
                    RoleChanged?.Invoke(this, new RoleChangedEventArgs(ColumnName, ur, false));
                }
            }
            else
            {
                UpdateDisplayText();
            }

            RoleChanged?.Invoke(this, new RoleChangedEventArgs(ColumnName, roleKey, true));
            popup.IsOpen = false;
        }

        private void OnRoleUnchecked(object sender, RoutedEventArgs e)
        {
            var cb = (CheckBox)sender;
            if (_suppressEvents.Contains(cb)) return;

            string roleKey = (string)cb.Tag;
            UpdateDisplayText();
            RoleChanged?.Invoke(this, new RoleChangedEventArgs(ColumnName, roleKey, false));
            popup.IsOpen = false;
        }

        private void UpdateDisplayText()
        {
            var firstChecked = _roleCheckboxes.FirstOrDefault(x => x.Value.IsChecked == true);
            DisplayText = firstChecked.Value?.Content?.ToString() ?? "Выбрать...";
            // Кнопка всегда показывает ▼, текст роли — в DisplayText для привязок если нужно
        }
    }
}