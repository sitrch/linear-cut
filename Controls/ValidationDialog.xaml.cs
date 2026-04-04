using System.Data;
using System.Windows;

namespace LinearCutWpf.Controls
{
    /// <summary>
    /// Диалоговое окно для отображения ошибок валидации данных.
    /// </summary>
    public partial class ValidationDialog : Window
    {
        /// <summary>
        /// Возвращает значение, указывающее, решил ли пользователь проигнорировать ошибки.
        /// </summary>
        public bool Ignored { get; private set; }

        /// <summary>
        /// Инициализирует новый экземпляр класса <see cref="ValidationDialog"/>.
        /// </summary>
        /// <param name="errorsTable">Таблица, содержащая информацию об ошибках.</param>
        public ValidationDialog(DataTable errorsTable)
        {
            InitializeComponent();
            dgErrors.ItemsSource = errorsTable.DefaultView;
            Ignored = false;
        }

        /// <summary>
        /// Обработчик события нажатия кнопки "Игнорировать".
        /// </summary>
        private void BtnIgnore_Click(object sender, RoutedEventArgs e)
        {
            Ignored = true;
            DialogResult = true;
            Close();
        }

        /// <summary>
        /// Обработчик события нажатия кнопки "Отмена".
        /// </summary>
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Ignored = false;
            DialogResult = false;
            Close();
        }
    }
}