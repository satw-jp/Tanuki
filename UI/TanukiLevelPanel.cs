using System;
using System.Collections.Generic;
using Eto.Forms;
using Eto.Drawing;
using Rhino;
using Tanuki.Data;

namespace Tanuki.UI
{
    [System.Runtime.InteropServices.Guid("D4E5F6A7-B8C9-0123-DEF0-23456789ABCD")]
    public class TanukiLevelPanel : Panel
    {
        public static Guid PanelId => typeof(TanukiLevelPanel).GUID;

        private GridView _grid;
        private TextBox  _tbName;
        private TextBox  _tbElev;

        private class LevelRow
        {
            public string Name { get; set; }
            public string Elev { get; set; }
        }

        public TanukiLevelPanel(uint documentSerialNumber)
        {
            try { BuildUi(); }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"[Tanuki] LevelPanel error: {ex.Message}");
                Content = new Label { Text = ex.Message };
            }
        }

        private void BuildUi()
        {
            var layout = new DynamicLayout { Padding = new Padding(4), Spacing = new Size(0, 3) };

            // ── ツールバー ────────────────────────────────────────
            var tb = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 2 };
            tb.Items.Add(Btn("↑", "上に移動",  OnMoveUp));
            tb.Items.Add(Btn("↓", "下に移動",  OnMoveDown));
            tb.Items.Add(Btn("✕", "削除",      OnDelete));
            layout.AddRow(tb);

            // ── GridView ─────────────────────────────────────────
            _grid = new GridView { Height = 160, ShowHeader = true };
            _grid.Columns.Add(new GridColumn
            {
                HeaderText = "名前",
                Width      = 80,
                DataCell   = new TextBoxCell { Binding = Binding.Property<LevelRow, string>(r => r.Name) }
            });
            _grid.Columns.Add(new GridColumn
            {
                HeaderText = "高さ (mm)",
                Width      = 90,
                DataCell   = new TextBoxCell { Binding = Binding.Property<LevelRow, string>(r => r.Elev) }
            });
            _grid.SelectionChanged += (s, e) => OnSelect();
            layout.AddRow(_grid);

            // ── 入力フォーム ──────────────────────────────────────
            layout.AddRow(new Panel { Height = 1, BackgroundColor = Colors.DarkGray });

            var inputRow = new TableLayout { Spacing = new Size(4, 0) };
            _tbName = new TextBox { PlaceholderText = "名前 (例: 1FL)", Width = 90 };
            _tbElev = new TextBox { PlaceholderText = "高さ mm",        Width = 80 };
            inputRow.Rows.Add(new TableRow(
                new TableCell(_tbName, true),
                new TableCell(_tbElev)));
            layout.AddRow(inputRow);

            var btnRow = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 4 };
            btnRow.Items.Add(Btn("+ 追加",  "新規追加",   OnAdd));
            btnRow.Items.Add(Btn("✎ 更新",  "選択を更新", OnUpdate));
            layout.AddRow(btnRow);

            layout.Add(null);
            Content = layout;

            Refresh();
            RhinoDoc.ActiveDocumentChanged += (s, e) => Refresh();
        }

        // ── Actions ──────────────────────────────────────────────

        private void OnAdd()
        {
            Application.Instance.Invoke(() =>
            {
                var doc = RhinoDoc.ActiveDoc;
                if (doc == null || string.IsNullOrWhiteSpace(_tbName.Text)) return;
                if (!double.TryParse(_tbElev.Text, out double elev)) elev = 0;
                var project = TanukiProject.Load(doc);
                project.Levels.Add(new Level { Name = _tbName.Text.Trim(), Elevation = elev });
                project.Save(doc);
                Refresh();
            });
        }

        private void OnUpdate()
        {
            Application.Instance.Invoke(() =>
            {
                int idx = _grid.SelectedRow;
                var doc = RhinoDoc.ActiveDoc;
                if (doc == null || idx < 0 || string.IsNullOrWhiteSpace(_tbName.Text)) return;
                if (!double.TryParse(_tbElev.Text, out double elev)) elev = 0;
                var project = TanukiProject.Load(doc);
                if (idx >= project.Levels.Count) return;
                project.Levels[idx].Name      = _tbName.Text.Trim();
                project.Levels[idx].Elevation = elev;
                project.Save(doc);
                Refresh();
            });
        }

        private void OnDelete()
        {
            Application.Instance.Invoke(() =>
            {
                int idx = _grid.SelectedRow;
                var doc = RhinoDoc.ActiveDoc;
                if (doc == null || idx < 0) return;
                var project = TanukiProject.Load(doc);
                if (idx >= project.Levels.Count) return;
                project.Levels.RemoveAt(idx);
                project.Save(doc);
                Refresh();
            });
        }

        private void OnMoveUp()
        {
            Application.Instance.Invoke(() =>
            {
                int idx = _grid.SelectedRow;
                var doc = RhinoDoc.ActiveDoc;
                if (doc == null || idx <= 0) return;
                var project = TanukiProject.Load(doc);
                var tmp = project.Levels[idx - 1];
                project.Levels[idx - 1] = project.Levels[idx];
                project.Levels[idx] = tmp;
                project.Save(doc);
                Refresh();
                _grid.SelectedRow = idx - 1;
            });
        }

        private void OnMoveDown()
        {
            Application.Instance.Invoke(() =>
            {
                int idx = _grid.SelectedRow;
                var doc = RhinoDoc.ActiveDoc;
                if (doc == null || idx < 0) return;
                var project = TanukiProject.Load(doc);
                if (idx >= project.Levels.Count - 1) return;
                var tmp = project.Levels[idx + 1];
                project.Levels[idx + 1] = project.Levels[idx];
                project.Levels[idx] = tmp;
                project.Save(doc);
                Refresh();
                _grid.SelectedRow = idx + 1;
            });
        }

        private void OnSelect()
        {
            Application.Instance.Invoke(() =>
            {
                int idx = _grid.SelectedRow;
                var doc = RhinoDoc.ActiveDoc;
                if (doc == null || idx < 0) return;
                var project = TanukiProject.Load(doc);
                if (idx >= project.Levels.Count) return;
                _tbName.Text = project.Levels[idx].Name;
                _tbElev.Text = project.Levels[idx].Elevation.ToString("F0");
            });
        }

        private void Refresh()
        {
            Application.Instance.Invoke(() =>
            {
                var doc = RhinoDoc.ActiveDoc;
                var project = doc != null ? TanukiProject.Load(doc) : null;
                var rows = new List<LevelRow>();
                if (project != null)
                    foreach (var l in project.Levels)
                        rows.Add(new LevelRow { Name = l.Name, Elev = $"{l.Elevation:F0}" });
                _grid.DataStore = rows;
            });
        }

        private Button Btn(string label, string tip, Action action)
        {
            var b = new Button { Text = label, Height = 26, ToolTip = tip };
            b.Click += (s, e) => action();
            return b;
        }
    }
}
