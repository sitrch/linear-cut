using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.ComponentModel;
using MiniExcelLibs;

namespace LinearCutOptimization
{
    public partial class MainForm : Form
    {
        private DataGridView _gridInput, _columnConfigGrid, _gridPresets, _gridStocks, _gridManual;
        private ComboBox _sheetSelector, _activePresetCombo;
        private TabControl _tabControl, _resultsTabControl, _groupingTabControl;

        private BindingSource _presetBinding = new BindingSource();
        private BindingList<StockModel> _stockList = new BindingList<StockModel>();
        private BindingList<ManualCutRow> _manualCuts = new BindingList<ManualCutRow>();

        private string _currentFilePath;
        private DataTable _mainDataTable;
        private HashSet<(int row, string colName)> _autoFilledKeys = new HashSet<(int, string)>();
        private bool _hasManualErrors = false;

        public MainForm()
        {
            this.Text = "Linear Cut Optimizer PRO v5.5 (Full Fix)";
            this.Width = 1600;
            this.Height = 950;
            this.StartPosition = FormStartPosition.CenterScreen;

            InitializeComponents();
            LoadInitialData();
        }

        private void InitializeComponents()
        {
            _tabControl = new TabControl { Dock = DockStyle.Fill };
            _tabControl.SelectedIndexChanged += OnTabChanged;

            // --- ТАБ 1: ДАННЫЕ И НАСТРОЙКИ ---
            TabPage tabMain = new TabPage("1. Данные и Настройки");
            TableLayoutPanel mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 450));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            FlowLayoutPanel sidePanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(10), WrapContents = false, AutoScroll = true };

            Button btnLoad = new Button { Text = "📂 Открыть .xlsx", Width = 400, Height = 40, BackColor = Color.AliceBlue, FlatStyle = FlatStyle.Flat };
            btnLoad.Click += OnOpenFileClick;

            _sheetSelector = new ComboBox { Width = 400, DropDownStyle = ComboBoxStyle.DropDownList };
            _sheetSelector.SelectedIndexChanged += (s, e) => LoadDataFromSheet(_sheetSelector.Text);

            _columnConfigGrid = CreateSmallGrid(400, 200);
            _columnConfigGrid.Columns.Add("ColName", "Столбец");
            _columnConfigGrid.Columns["ColName"].ReadOnly = true;
            _columnConfigGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "IsKey", HeaderText = "Key", Width = 50 });
            _columnConfigGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "IsVal", HeaderText = "Value", Width = 50 });
            _columnConfigGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "IsQty", HeaderText = "Quantity", Width = 60 });

            _columnConfigGrid.CellValueChanged += (s, e) => { ProcessDataLogic(); RefreshColumnsVisuals(); };
            _columnConfigGrid.CurrentCellDirtyStateChanged += (s, e) => { if (_columnConfigGrid.IsCurrentCellDirty) _columnConfigGrid.CommitEdit(DataGridViewDataErrorContexts.Commit); };

            _gridPresets = CreateSmallGrid(400, 150);
            _gridStocks = CreateSmallGrid(400, 150);

            sidePanel.Controls.AddRange(new Control[] { btnLoad, _sheetSelector, new Label { Text = "Настройка ролей столбцов:" }, _columnConfigGrid, new Label { Text = "Пресеты:" }, _gridPresets, new Label { Text = "Склад (Stock):" }, _gridStocks });

            _gridInput = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, BackgroundColor = Color.White, EnableHeadersVisualStyles = false };
            _gridInput.CellFormatting += OnGridInputCellFormatting;

            mainLayout.Controls.Add(sidePanel, 0, 0);
            mainLayout.Controls.Add(_gridInput, 1, 0);
            tabMain.Controls.Add(mainLayout);

            // --- ТАБ 2: ГРУППИРОВКА ---
            TabPage tabGroup = new TabPage("2. Группировка");
            TableLayoutPanel groupLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            groupLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 450));

            FlowLayoutPanel groupSide = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(10), WrapContents = false };
            _activePresetCombo = new ComboBox { Width = 400, DropDownStyle = ComboBoxStyle.DropDownList };

            _gridManual = CreateSmallGrid(400, 350);
            _gridManual.DataError += (s, e) => { e.ThrowException = false; };
            _gridManual.DataSource = _manualCuts;
            _gridManual.CellValueChanged += (s, e) => UpdateManualValidation();

            Button btnImport = new Button { Text = "📥 Загрузить старый раскрой", Width = 400, Height = 40, BackColor = Color.LightYellow };
            btnImport.Click += (s, e) => { using (var ofd = new OpenFileDialog { Filter = "Excel|*.xlsx" }) if (ofd.ShowDialog() == DialogResult.OK) LoadSavedProject(ofd.FileName); };

            Button btnExport = new Button { Text = "💾 Сохранить проект (Excel)", Width = 400, Height = 45, BackColor = Color.LightGreen, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 20, 0, 0) };
            btnExport.Click += (s, e) => { using (var sfd = new SaveFileDialog { Filter = "Excel|*.xlsx" }) if (sfd.ShowDialog() == DialogResult.OK) ExportToExcel(sfd.FileName); };

            groupSide.Controls.AddRange(new Control[] { new Label { Text = "Активный пресет:" }, _activePresetCombo, new Label { Text = "Ручной раскрой:" }, _gridManual, btnImport, btnExport });

            _groupingTabControl = new TabControl { Dock = DockStyle.Fill };
            groupLayout.Controls.Add(groupSide, 0, 0);
            groupLayout.Controls.Add(_groupingTabControl, 1, 0);
            tabGroup.Controls.Add(groupLayout);

            // --- ТАБ 3: РЕЗУЛЬТАТЫ ---
            TabPage tabRes = new TabPage("3. Результаты");
            _resultsTabControl = new TabControl { Dock = DockStyle.Fill };
            tabRes.Controls.Add(_resultsTabControl);

            _tabControl.TabPages.AddRange(new TabPage[] { tabMain, tabGroup, tabRes });
            this.Controls.Add(_tabControl);
        }

        private DataGridView CreateSmallGrid(int w, int h) => new DataGridView { Width = w, Height = h, BackgroundColor = Color.White, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, AllowUserToAddRows = true };

        private void LoadInitialData()
        {
            _presetBinding.DataSource = CutSettingsProvider.LoadAll();
            _gridPresets.DataSource = _presetBinding;
            _stockList.Add(new StockModel { Length = 6000, IsEnabled = true });
            _gridStocks.DataSource = _stockList;
            _activePresetCombo.DataSource = _presetBinding;
            _activePresetCombo.DisplayMember = "Name";
            _manualCuts.Clear();
        }

        private void OnTabChanged(object sender, EventArgs e)
        {
            if (_tabControl.SelectedTab.Text.Contains("Результаты") && _hasManualErrors)
            {
                MessageBox.Show("Ошибка в ручном раскрое!"); _tabControl.SelectedTab = _tabControl.TabPages["2. Группировка"];
                return;
            }
            if (_tabControl.SelectedTab.Text.Contains("Группировка")) { RunGroupingWithTabs(); UpdateManualCutDropdowns(); }
            if (_tabControl.SelectedTab.Text.Contains("Результаты")) RunOptimization();
        }

        private void LoadSavedProject(string path)
        {
            try
            {
                DataTable dt = MiniExcel.QueryAsDataTable(path, sheetName: "Ручной_Раскрой");
                _manualCuts.Clear();
                foreach (DataRow dr in dt.Rows)
                {
                    _manualCuts.Add(new ManualCutRow
                    {
                        StockLength = dr["StockLength"] != DBNull.Value ? (double?)Convert.ToDouble(dr["StockLength"]) : null,
                        Size1 = dr["Size1"]?.ToString(),
                        Size2 = dr["Size2"]?.ToString(),
                        Size3 = dr["Size3"]?.ToString(),
                        Size4 = dr["Size4"]?.ToString()
                    });
                }
                UpdateManualValidation();
            }
            catch { MessageBox.Show("Лист 'Ручной_Раскрой' не найден."); }
        }

        private void UpdateManualValidation()
        {
            var preset = (PresetModel)_activePresetCombo.SelectedItem;
            if (preset == null) return;
            _hasManualErrors = false;
            double red = (preset.TrimStart - preset.CutWidth / 2) + (preset.TrimEnd - preset.CutWidth / 2);

            foreach (DataGridViewRow r in _gridManual.Rows)
            {
                if (r.IsNewRow || r.Cells["StockLength"].Value == null) continue;
                double stock = Convert.ToDouble(r.Cells["StockLength"].Value);
                double used = 0;
                string[] sz = { "Size1", "Size2", "Size3", "Size4" };
                foreach (var sCol in sz)
                {
                    var cell = r.Cells[sCol];
                    if (cell.Value == null || string.IsNullOrEmpty(cell.Value.ToString())) continue;
                    double val = double.TryParse(cell.Value.ToString(), out var d) ? d : 0;
                    if (val > 0)
                    {
                        if (used + val + preset.CutWidth > (stock - red)) { cell.Style.BackColor = Color.Red; _hasManualErrors = true; }
                        else { cell.Style.BackColor = Color.White; used += (val + preset.CutWidth); }
                    }
                }
            }
        }

        private void RunOptimization()
        {
            if (_groupingTabControl.TabPages.Count == 0) RunGroupingWithTabs();
            var preset = (PresetModel)_activePresetCombo.SelectedItem; if (preset == null) return;
            _resultsTabControl.TabPages.Clear();

            var manualParts = new List<double>();
            foreach (var mr in _manualCuts)
            {
                if (double.TryParse(mr.Size1, out var s1) && s1 > 0) manualParts.Add(s1 + preset.CutWidth);
                if (double.TryParse(mr.Size2, out var s2) && s2 > 0) manualParts.Add(s2 + preset.CutWidth);
                if (double.TryParse(mr.Size3, out var s3) && s3 > 0) manualParts.Add(s3 + preset.CutWidth);
                if (double.TryParse(mr.Size4, out var s4) && s4 > 0) manualParts.Add(s4 + preset.CutWidth);
            }

            foreach (TabPage groupPage in _groupingTabControl.TabPages)
            {
                DataGridView sourceGv = groupPage.Controls.OfType<DataGridView>().FirstOrDefault();
                DataTable dt = (DataTable)sourceGv?.DataSource; if (dt == null) continue;

                var pts = new List<double>(); double totP = 0;
                foreach (DataRow row in dt.Rows)
                {
                    double l = Convert.ToDouble(row[GetCheckedCols("IsVal").First()]);
                    double lWithCut = l + preset.CutWidth;
                    int c = Convert.ToInt32(row["Количество"]);
                    for (int i = 0; i < c; i++)
                    {
                        var mIdx = manualParts.FindIndex(mp => Math.Abs(mp - lWithCut) < 0.1);
                        if (mIdx >= 0) manualParts.RemoveAt(mIdx); else { pts.Add(lWithCut); totP += l; }
                    }
                }

                var st = _stockList.Where(x => x.IsEnabled).Select(x => x.Length).ToList();
                var res = CutOptimizer.Optimize(pts, st, preset.TrimStart, preset.TrimEnd, preset.CutWidth);

                TabPage rp = new TabPage(groupPage.Text); TableLayoutPanel lp = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
                lp.RowStyles.Add(new RowStyle(SizeType.Percent, 80)); lp.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
                DataTable rd = new DataTable(); rd.Columns.Add("Кол-во"); rd.Columns.Add("Хлыст"); rd.Columns.Add("Раскрой"); rd.Columns.Add("Остаток");
                foreach (var gb in res.GroupBy(r => r.StockLength + r.Parts)) { DataRow nr = rd.NewRow(); nr["Кол-во"] = gb.Count(); nr["Хлыст"] = gb.First().StockLength; nr["Раскрой"] = gb.First().Parts; nr["Остаток"] = gb.First().Remainder; rd.Rows.Add(nr); }
                DataGridView rgv = new DataGridView { Dock = DockStyle.Fill, DataSource = rd, ReadOnly = true, BackgroundColor = Color.White, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells };
                double totS = res.Sum(r => r.StockLength);
                TextBox s = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, Text = $"КПД: {(totS > 0 ? (totP / totS * 100) : 0):F2}%", Font = new Font("Consolas", 10), BackColor = Color.WhiteSmoke, BorderStyle = BorderStyle.None };
                lp.Controls.Add(rgv, 0, 0); lp.Controls.Add(s, 0, 1); rp.Controls.Add(lp); _resultsTabControl.TabPages.Add(rp);
            }
        }

        private void UpdateManualCutDropdowns()
        {
            var vals = GetCheckedCols("IsVal");
            if (!vals.Any() || _gridInput.DataSource == null) return;
            var lengths = ((DataTable)_gridInput.DataSource).Rows.Cast<DataRow>().Select(r => r[vals.First()]?.ToString()).Where(s => !string.IsNullOrEmpty(s)).Distinct().OrderByDescending(s => double.Parse(s)).ToList();
            string[] cb = { "Size1", "Size2", "Size3", "Size4" };
            foreach (var c in cb)
            {
                if (_gridManual.Columns.Contains(c) && !(_gridManual.Columns[c] is DataGridViewComboBoxColumn)) _gridManual.Columns.Remove(c);
                if (!_gridManual.Columns.Contains(c)) _gridManual.Columns.Add(new DataGridViewComboBoxColumn { Name = c, DataPropertyName = c, HeaderText = c, DataSource = lengths, FlatStyle = FlatStyle.Flat });
                else ((DataGridViewComboBoxColumn)_gridManual.Columns[c]).DataSource = lengths;
            }
        }

        private void OnOpenFileClick(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = "Excel|*.xlsx" }) if (ofd.ShowDialog() == DialogResult.OK) { _currentFilePath = ofd.FileName; _sheetSelector.Items.Clear(); _sheetSelector.Items.AddRange(MiniExcel.GetSheetNames(_currentFilePath).ToArray()); if (_sheetSelector.Items.Count > 0) _sheetSelector.SelectedIndex = 0; }
        }

        private void LoadDataFromSheet(string s) { _mainDataTable = MiniExcel.QueryAsDataTable(_currentFilePath, useHeaderRow: true, sheetName: s); _columnConfigGrid.Rows.Clear(); foreach (DataColumn c in _mainDataTable.Columns) _columnConfigGrid.Rows.Add(c.ColumnName, false, false, false); ProcessDataLogic(); }

        private void ProcessDataLogic()
        {
            if (_mainDataTable == null) return;
            DataTable dt = _mainDataTable.Copy(); _autoFilledKeys.Clear();
            var keys = GetCheckedCols("IsKey"); var vals = GetCheckedCols("IsVal");
            if (vals.Any()) for (int i = dt.Rows.Count - 1; i >= 0; i--) if (vals.All(v => dt.Rows[i][v] == DBNull.Value || string.IsNullOrWhiteSpace(dt.Rows[i][v].ToString()))) dt.Rows.RemoveAt(i);
            foreach (string k in keys) for (int r = 1; r < dt.Rows.Count; r++) if (string.IsNullOrWhiteSpace(dt.Rows[r][k]?.ToString())) { dt.Rows[r][k] = dt.Rows[r - 1][k]; _autoFilledKeys.Add((r, k)); }
            _gridInput.DataSource = dt; RefreshColumnsVisuals();
        }

        private void RunGroupingWithTabs()
        {
            DataTable dt = (DataTable)_gridInput.DataSource; if (dt == null) return;
            var keys = GetCheckedCols("IsKey"); var vals = GetCheckedCols("IsVal"); var qty = GetCheckedCols("IsQty").FirstOrDefault();
            _groupingTabControl.TabPages.Clear(); if (!keys.Any() || !vals.Any()) return;
            var groups = dt.Rows.Cast<DataRow>().GroupBy(r => string.Join("_", keys.Select(k => r[k]?.ToString())));
            foreach (var g in groups)
            {
                TabPage tp = new TabPage(g.Key); DataGridView gv = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, BackgroundColor = Color.White, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells };
                DataTable resDt = new DataTable(); foreach (var k in keys) resDt.Columns.Add(k); foreach (var v in vals) resDt.Columns.Add(v); resDt.Columns.Add("Количество", typeof(double));
                foreach (var sg in g.GroupBy(r => string.Join("|", vals.Select(v => r[v]?.ToString()))))
                {
                    DataRow nr = resDt.NewRow(); foreach (var k in keys) nr[k] = sg.First()[k]; foreach (var v in vals) nr[v] = sg.First()[v];
                    nr["Количество"] = sg.Sum(r => !string.IsNullOrEmpty(qty) ? Convert.ToDouble(r[qty] == DBNull.Value ? 0 : r[qty]) : 1.0); resDt.Rows.Add(nr);
                }
                gv.DataSource = resDt; tp.Controls.Add(gv); _groupingTabControl.TabPages.Add(tp);
            }
        }

        private void ExportToExcel(string path)
        {
            var sheets = new Dictionary<string, object> { { "Пресеты", _presetBinding.DataSource }, { "Ручной_Раскрой", _manualCuts.ToList() }, { "Склад", _stockList.ToList() } };
            MiniExcel.SaveAs(path, sheets); MessageBox.Show("Экспорт завершен.");
        }

        private void RefreshColumnsVisuals()
        {
            if (_gridInput.Columns.Count == 0) return;
            foreach (DataGridViewRow r in _columnConfigGrid.Rows)
            {
                bool k = (bool)(r.Cells["IsKey"].Value ?? false); bool v = (bool)(r.Cells["IsVal"].Value ?? false); bool q = (bool)(r.Cells["IsQty"].Value ?? false);
                r.Cells["IsKey"].Style.BackColor = k ? Color.LightGreen : Color.White; r.Cells["IsVal"].Style.BackColor = v ? Color.LightYellow : Color.White; r.Cells["IsQty"].Style.BackColor = q ? Color.LightCyan : Color.White;
                string n = r.Cells["ColName"].Value?.ToString(); if (string.IsNullOrEmpty(n) || !_gridInput.Columns.Contains(n)) continue;
                var c = _gridInput.Columns[n]; if (k) c.DefaultCellStyle.BackColor = Color.LightGreen; else if (v) c.DefaultCellStyle.BackColor = Color.LightYellow; else if (q) c.DefaultCellStyle.BackColor = Color.LightCyan; else c.DefaultCellStyle.BackColor = Color.White;
            }
        }

        private void OnGridInputCellFormatting(object sender, DataGridViewCellFormattingEventArgs e) { if (e.RowIndex >= 0 && _autoFilledKeys.Contains((e.RowIndex, _gridInput.Columns[e.ColumnIndex].Name))) e.CellStyle.ForeColor = Color.Red; }

        private List<string> GetCheckedCols(string t) => _columnConfigGrid.Rows.Cast<DataGridViewRow>().Where(r => r.Cells[t].Value != null && (bool)r.Cells[t].Value).Select(r => r.Cells["ColName"].Value.ToString()).ToList();
    }
}
