using System.Windows.Controls;
using LinearCutWpf.ViewModels;

namespace LinearCutWpf.Controls
{
    /// <summary>
    /// Контрол для ручного управления раскроем.
    /// </summary>
    public partial class ManualCutControl : UserControl
    {
        /// <summary>
        /// Возвращает модель представления для ручного раскроя.
        /// </summary>
        public ManualCutViewModel ViewModel { get; }

        /// <summary>
        /// Инициализирует новый экземпляр класса <see cref="ManualCutControl"/>.
        /// </summary>
        public ManualCutControl()
        {
            InitializeComponent();
            ViewModel = new ManualCutViewModel();
            this.DataContext = ViewModel;
        }
    }
}
