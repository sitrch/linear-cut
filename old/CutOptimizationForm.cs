using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;
using MiniExcelLibs;

namespace LinearCut.old
{
    public class CutOptimizationForm : Form
    {
        private DataGridView _gridInput, _columnConfigGrid;
        private ComboBox _sheetSelector, _presetSelector;
        private TabControl _tabControl, _resultsTabControl, _groupingTabControl;
        private CheckBox _showKeysInResults;
        private string _currentFilePath;
        private DataTable _mainDataTable;

        // Настройки
        private double _trimStart, _trimEnd, _cutWidth;
        private Dictionary<string, List<double>> _itemStocks = new Dictionary<string, List<double>>();
        private HashSet<(int row, string colName)> _autoFilledKeys = new HashSet<(int, string)>();

        public CutOptimizationForm()
        {
            this.Text = "Линейный раскрой PRO: Финальная сборка";
            this.Width = 1500;
            this.Height = 900;
            this.StartPosition = FormStartPosition.CenterScreen;

            InitializeComponents();
            LoadSettings(); // Загружаем настройки и пресеты сразу
        }

        private void LoadSettings()
        {
            string xmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.xml");
            if (!File.Exists(xmlPath)) return;
            try
            {
                var doc = XDocument.Load(xmlPath);
                _trimStart = double.Parse(doc.Root.Element("TrimStart")?.Value ?? "10");
                _trimEnd = double.Parse(doc.Root.Element("TrimEnd")?.Value ?? "10");
                _cutWidth = double.Parse(doc.Root.Element("CutWidth")?.Value ?? "4");

                // Заполнение ComboBox пресетами
                var presetNames = doc.Root.Element("Presets")?.Elements("Preset")
                                   .Select(p => p.Attribute("Name")?.Value).ToArray();
                if (presetNames != null)
                {
                    _presetSelector.Items.Clear();
                    _presetSelector.Items.AddRange(presetNames);
                    if (_presetSelector.Items.Count > 0) _presetSelector.SelectedIndex = 0;
                }
            }
            catch (Exception ex) { MessageBox.Show("Ошибка XML: " + ex.Message); }
        }

        private void InitializeComponents()
        {
            _tabControl = new TabControl { Dock = DockStyle.Fill };
            _tabControl.SelectedIndexChanged += OnTabChanged;

            // --- ТАБ 0: ПРЕСЕТЫ ---
            TabPage tabSet = new TabPage("⚙️ Пресеты");
            FlowLayoutPanel setPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), FlowDirection = FlowDirection.TopDown };
            setPanel.Controls.Add(new Label { Text = "Выберите пресет раскроя (из settings.xml):", AutoSize = true });
            _presetSelector = new ComboBox { Width = 350, DropDownStyle = ComboBoxStyle.DropDownList };
            _presetSelector.SelectedIndexChanged += (s, e) => ApplyPreset(_presetSelector.Text);
            _showKeysInResults = new CheckBox { Text = "Отображать столбцы Key в итоговых таблицах", Checked = true, Margin = new Padding(0, 20, 0, 0) };
            setPanel.Controls.AddRange(new Control[] { _presetSelector, _showKeysInResults });
            tabSet.Controls.Add(setPanel);

            // --- ТАБ 1: ЗАГРУЗКА ---
            TabPage tabMain = new TabPage("1. Загрузка данных");
            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 400));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            FlowLayoutPanel sidePanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(15), WrapContents = false };
            Button btnLoad = new Button { Text = "📂 Открыть .xlsx", Width = 360, Height = 45, BackColor = Color.AliceBlue, FlatStyle = FlatStyle.Flat };
            btnLoad.Click += OnOpenFileClick;
            _sheetSelector = new ComboBox { Width = 360, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 10, 0, 10) };
            _sheetSelector.SelectedIndexChanged += (s, e) => LoadDataFromSheet(_sheetSelector.Text);

            _columnConfigGrid = new DataGridView { Width = 360, Height = 350, AllowUserToAddRows = false, RowHeadersVisible = false, BackgroundColor = Color.White, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
            _columnConfigGrid.Columns.Add("ColName", "Столбец");
            _columnConfigGrid.Columns["ColName"].ReadOnly = true;
            _columnConfigGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "IsKey", HeaderText = "Key" });
            _columnConfigGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "IsVal", HeaderText = "Val" });
            _columnConfigGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "IsQty", HeaderText = "Qty" });
            _columnConfigGrid.CellValueChanged += (s, e) => { ProcessDataLogic(); RefreshColumnsVisuals(); };
            _columnConfigGrid.CurrentCellDirtyStateChanged += (s, e) => { if (_columnConfigGrid.IsCurrentCellDirty) _columnConfigGrid.CommitEdit(DataGridViewDataErrorContexts.Commit); };

            sidePanel.Controls.AddRange(new Control[] { btnLoad, _sheetSelector, _columnConfigGrid });
            _gridInput = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, BackgroundColor = Color.White, EnableHeadersVisualStyles = false };
            _gridInput.CellFormatting += OnGridInputCellFormatting;
            layout.Controls.Add(sidePanel, 0, 0);
            layout.Controls.Add(_gridInput, 1, 0);
            tabMain.Controls.Add(layout);

            // --- ТАБ 2: ГРУППИРОВКА (С ТАБАМИ) ---
            TabPage tabGroup = new TabPage("2. Группировка");
            _groupingTabControl = new TabControl { Dock = DockStyle.Fill };
            tabGroup.Controls.Add(_groupingTabControl);

            // --- ТАБ 3: РЕЗУЛЬТАТЫ ---
            TabPage tabRes = new TabPage("3. Результаты раскроя");
            _resultsTabControl = new TabControl { Dock = DockStyle.Fill };
            tabRes.Controls.Add(_resultsTabControl);

            _tabControl.TabPages.AddRange(new TabPage[] { tabSet, tabMain, tabGroup, tabRes });
            this.Controls.Add(_tabControl);
        }

        private void OnTabChanged(object sender, EventArgs e)
        {
            if (_tabControl.SelectedTab.Text.Contains("Группировка")) RunGroupingWithTabs();
            if (_tabControl.SelectedTab.Text.Contains("Результаты")) RunOptimization();
        }

        private void ApplyPreset(string name)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.xml");
            if (!File.Exists(path)) return;
            var doc = XDocument.Load(path);
            var pr = doc.Root.Element("Presets").Elements("Preset").FirstOrDefault(p => p.Attribute("Name")?.Value == name);
            if (pr != null) _itemStocks = pr.Elements("Stock").ToDictionary(s => s.Attribute("Item").Value, s => s.Attribute("Lengths").Value.Split(',').Select(double.Parse).ToList());
        }

        private void OnOpenFileClick(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = "Excel (.xlsx)|*.xlsx" })
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    _currentFilePath = ofd.FileName;
                    _sheetSelector.Items.Clear();
                    _sheetSelector.Items.AddRange(MiniExcel.GetSheetNames(_currentFilePath).ToArray());
                    if (_sheetSelector.Items.Count > 0) _sheetSelector.SelectedIndex = 0;
                }
        }

        private void LoadDataFromSheet(string sheet)
        {
            _mainDataTable = MiniExcel.QueryAsDataTable(_currentFilePath, useHeaderRow: true, sheetName: sheet);
            _columnConfigGrid.Rows.Clear();
            foreach (DataColumn col in _mainDataTable.Columns) _columnConfigGrid.Rows.Add(col.ColumnName, false, false, false);
            ProcessDataLogic();
        }

        private void ProcessDataLogic()
        {
            if (_mainDataTable == null) return;
            DataTable dt = _mainDataTable.Copy(); _autoFilledKeys.Clear();
            var keys = GetCheckedCols("IsKey"); var vals = GetCheckedCols("IsVal");
            if (vals.Any()) for (int i = dt.Rows.Count - 1; i >= 0; i--) if (vals.All(v => dt.Rows[i][v] == DBNull.Value || string.IsNullOrWhiteSpace(dt.Rows[i][v].ToString()))) dt.Rows.RemoveAt(i);
            foreach (string k in keys) for (int r = 1; r < dt.Rows.Count; r++) if (string.IsNullOrWhiteSpace(dt.Rows[r][k]?.ToString())) { dt.Rows[r][k] = dt.Rows[r - 1][k]; _autoFilledKeys.Add((r, k)); }
            _gridInput.DataSource = dt;
        }

        private void RunGroupingWithTabs()
        {
            DataTable dt = (DataTable)_gridInput.DataSource;
            if (dt == null) return;
            var keys = GetCheckedCols("IsKey"); var vals = GetCheckedCols("IsVal");
            var qty = GetCheckedCols("IsQty").FirstOrDefault();

            _groupingTabControl.TabPages.Clear();
            if (!keys.Any() || !vals.Any()) return;

            var groups = dt.Rows.Cast<DataRow>().GroupBy(r => string.Join("_", keys.Select(k => r[k]?.ToString())));

            foreach (var g in groups)
            {
                TabPage tp = new TabPage(g.Key);
                DataGridView gv = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, BackgroundColor = Color.White };

                DataTable resDt = new DataTable();
                foreach (var k in keys) resDt.Columns.Add(k);
                foreach (var v in vals) resDt.Columns.Add(v);
                resDt.Columns.Add("Количество", typeof(double));

                var subGroups = g.GroupBy(r => string.Join("|", vals.Select(v => r[v]?.ToString())));
                foreach (var sg in subGroups)
                {
                    DataRow nr = resDt.NewRow();
                    foreach (var k in keys) nr[k] = sg.First()[k];
                    foreach (var v in vals) nr[v] = sg.First()[v];
                    nr["Количество"] = sg.Sum(r => !string.IsNullOrEmpty(qty) ? Convert.ToDouble(r[qty] == DBNull.Value ? 0 : r[qty]) : 1.0);
                    resDt.Rows.Add(nr);
                }
                gv.DataSource = resDt;
                tp.Controls.Add(gv);
                _groupingTabControl.TabPages.Add(tp);
            }
        }

        private void RunOptimization()
        {
            if (_groupingTabControl.TabPages.Count == 0) RunGroupingWithTabs();
            _resultsTabControl.TabPages.Clear();

            foreach (TabPage groupPage in _groupingTabControl.TabPages)
            {
                DataGridView sourceGv = groupPage.Controls.OfType<DataGridView>().FirstOrDefault();
                if (sourceGv == null) continue;
                DataTable dt = (DataTable)sourceGv.DataSource;

                var keys = GetCheckedCols("IsKey"); var vals = GetCheckedCols("IsVal");
                var parts = new List<double>(); double totalPartsLen = 0;

                foreach (DataRow row in dt.Rows)
                {
                    double len = Convert.ToDouble(row[vals.First()]) + _cutWidth;
                    int count = Convert.ToInt32(row["Количество"]);
                    for (int i = 0; i < count; i++) { parts.Add(len); totalPartsLen += (len - _cutWidth); }
                }

                var stocks = _itemStocks.ContainsKey(groupPage.Text) ? _itemStocks[groupPage.Text] : new List<double> { 6000 };
                double red = (_trimStart - _cutWidth / 2) + (_trimEnd - _cutWidth / 2);
                var rawResults = PerformOptimization(parts, stocks, red);

                TabPage resPage = new TabPage(groupPage.Text);
                TableLayoutPanel lp = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
                lp.RowStyles.Add(new RowStyle(SizeType.Percent, 80));
                lp.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));

                DataTable resDt = new DataTable();
                resDt.Columns.Add("Кол-во"); if (_showKeysInResults.Checked) foreach (var k in keys) resDt.Columns.Add(k);
                resDt.Columns.Add("Хлыст"); resDt.Columns.Add("Раскрой"); resDt.Columns.Add("Остаток (мм)");

                foreach (var gb in rawResults.GroupBy(r => r.StockLength + r.Parts))
                {
                    DataRow nr = resDt.NewRow();
                    nr["Кол-во"] = gb.Count();
                    if (_showKeysInResults.Checked) foreach (var k in keys) nr[k] = dt.Rows[0][k];
                    nr["Хлыст"] = gb.First().StockLength; nr["Раскрой"] = gb.First().Parts; nr["Остаток (мм)"] = gb.First().Remainder;
                    resDt.Rows.Add(nr);
                }

                DataGridView resGv = new DataGridView { Dock = DockStyle.Fill, DataSource = resDt, ReadOnly = true, BackgroundColor = Color.White, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
                double totStock = rawResults.Sum(r => r.StockLength);
                TextBox stats = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, Text = $"Key: {groupPage.Text}\r\nИспользовано: {rawResults.Count} хлыстов ({totStock} мм)\r\nДеталей: {parts.Count} шт ({totalPartsLen} мм)\r\nКПД: {(totStock > 0 ? (totalPartsLen / totStock * 100) : 0):F2}%", Font = new Font("Consolas", 10) };

                lp.Controls.Add(resGv, 0, 0); lp.Controls.Add(stats, 0, 1);
                resPage.Controls.Add(lp); _resultsTabControl.TabPages.Add(resPage);
            }
        }

        private List<RawBar> PerformOptimization(List<double> parts, List<double> stocks, double red)
        {
            var res = new List<RawBar>(); var rem = parts.OrderByDescending(p => p).ToList();
            while (rem.Any())
            {
                double s = stocks.OrderByDescending(x => x).FirstOrDefault(x => x - red >= rem[0]);
                if (s == 0) { rem.RemoveAt(0); continue; }
                double cap = s - red; var cur = new List<double>();
                if (rem[0] > cap / 2) { cur.Add(rem[0]); rem.RemoveAt(0); }
                for (int i = 0; i < rem.Count; i++) if (rem[i] <= (cap - cur.Sum())) { cur.Add(rem[i]); rem.RemoveAt(i); i--; }
                res.Add(new RawBar { StockLength = s, Parts = string.Join(" + ", cur.Select(p => p - _cutWidth)), Remainder = Math.Round(cap - cur.Sum(), 2) });
            }
            return res;
        }

        private void OnGridInputCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex >= 0 && _autoFilledKeys.Contains((e.RowIndex, _gridInput.Columns[e.ColumnIndex].Name))) e.CellStyle.ForeColor = Color.Red;
        }

        private List<string> GetCheckedCols(string t) => _columnConfigGrid.Rows.Cast<DataGridViewRow>().Where(r => r.Cells[t].Value != null && (bool)r.Cells[t].Value).Select(r => r.Cells["ColName"].Value.ToString()).ToList();

        private void RefreshColumnsVisuals()
        {
            foreach (DataGridViewRow r in _columnConfigGrid.Rows)
            {
                string n = r.Cells["ColName"].Value.ToString(); if (!_gridInput.Columns.Contains(n)) continue;
                var c = _gridInput.Columns[n];
                if ((bool)r.Cells["IsKey"].Value) c.DefaultCellStyle.BackColor = Color.LightGreen;
                else if ((bool)r.Cells["IsVal"].Value) c.DefaultCellStyle.BackColor = Color.LightYellow;
                else if ((bool)r.Cells["IsQty"].Value) c.DefaultCellStyle.BackColor = Color.LightCyan;
                else { c.DefaultCellStyle.BackColor = Color.FromArgb(245, 245, 245); c.DefaultCellStyle.ForeColor = Color.Gray; }
            }
        }
    }
    public class RawBar { public double StockLength { get; set; } public string Parts { get; set; } public double Remainder { get; set; } }
}
