using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Windows;

namespace LinearCutOptimization.Wpf.ViewModels
{
    public partial class MainViewModel
    {
        private DataTable _mainDataTable;
        private DataTable _gridInputTable;

        public System.Data.DataView GridInputView => _gridInputTable?.DefaultView;

        private bool _suppressRoleRecalc;

        private void BuildColumnRolesFromTable(DataTable dt)
        {
            _suppressRoleRecalc = true;
            try
            {
                ColumnRoles.Clear();
                if (dt == null) return;

                foreach (DataColumn c in dt.Columns)
                {
                    var row = new Models.ColumnRoleRow { ColName = c.ColumnName };
                    row.PropertyChanged += OnColumnRoleRowChanged;
                    ColumnRoles.Add(row);
                }
            }
            finally
            {
                _suppressRoleRecalc = false;
            }
        }

        private void OnColumnRoleRowChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_suppressRoleRecalc) return;

            if (e.PropertyName == nameof(Models.ColumnRoleRow.IsKey) ||
                e.PropertyName == nameof(Models.ColumnRoleRow.IsVal) ||
                e.PropertyName == nameof(Models.ColumnRoleRow.IsQty))
            {
                var changed = sender as Models.ColumnRoleRow;

                _suppressRoleRecalc = true;
                try
                {
                    if (changed != null && changed.IsVal)
                        foreach (var r in ColumnRoles.Where(r => !ReferenceEquals(r, changed))) r.IsVal = false;

                    if (changed != null && changed.IsQty)
                        foreach (var r in ColumnRoles.Where(r => !ReferenceEquals(r, changed))) r.IsQty = false;

                    if (changed != null && changed.IsKey)
                    {
                        changed.IsVal = false;
                        changed.IsQty = false;
                    }
                }
                finally
                {
                    _suppressRoleRecalc = false;
                }

                ProcessDataLogic();
            }
        }

        private string GetValueColumn() => ColumnRoles.FirstOrDefault(r => r.IsVal)?.ColName;
        private string GetQtyColumn() => ColumnRoles.FirstOrDefault(r => r.IsQty)?.ColName;

        private void ProcessDataLogic()
        {
            if (_mainDataTable == null) return;

            var keys = ColumnRoles.Where(r => r.IsKey).Select(r => r.ColName).ToList();
            var val = GetValueColumn();

            var dt = _mainDataTable.Copy();

            if (!string.IsNullOrWhiteSpace(val))
            {
                for (int i = dt.Rows.Count - 1; i >= 0; i--)
                {
                    bool empty = dt.Rows[i][val] == System.DBNull.Value ||
                                 string.IsNullOrWhiteSpace(dt.Rows[i][val]?.ToString());
                    if (empty) dt.Rows.RemoveAt(i);
                }
            }

            foreach (string k in keys)
            {
                for (int r = 1; r < dt.Rows.Count; r++)
                {
                    if (string.IsNullOrWhiteSpace(dt.Rows[r][k]?.ToString()))
                        dt.Rows[r][k] = dt.Rows[r - 1][k];
                }
            }

            _gridInputTable = dt;
            RaisePropertyChanged(nameof(GridInputView));
        }

        private void RunOptimization()
        {
            if (SelectedPreset == null)
            {
                MessageBox.Show("Выбери пресет.");
                return;
            }

            // Заглушка результата: чтобы UI показывал вкладку
            ResultTabs.Clear();
            ResultTabs.Add(new OptimizationTabViewModel("Результат", new DataTable(), "Пока без расчёта"));
        }
    }
}