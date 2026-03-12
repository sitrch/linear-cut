using LinearCutOptimization.Wpf.Mvvm;

namespace LinearCutOptimization.Wpf.Models
{
    public class ColumnRoleRow : ViewModelBase
    {
        private string _colName;
        public string ColName { get => _colName; set => Set(ref _colName, value); }

        private bool _isKey;
        public bool IsKey { get => _isKey; set => Set(ref _isKey, value); }

        private bool _isVal;
        public bool IsVal { get => _isVal; set => Set(ref _isVal, value); }

        private bool _isQty;
        public bool IsQty { get => _isQty; set => Set(ref _isQty, value); }
    }
}