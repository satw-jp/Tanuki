using System;
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

        private readonly ListBox _list;
        private readonly TextBox _tbName;
        private readonly TextBox _tbElev;

        public TanukiLevelPanel(uint documentSerialNumber)
        {
            try
            {
                var layout = new DynamicLayout { Padding = new Padding(4), Spacing = new Size(0, 3) };

                // ── ツールバー ────────────────────────────────────────
                var toolbar = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 2 };
                toolbar.Items.Add(SmallBtn("↑", "上に移動",  OnMoveUp));
                toolbar.Items.Add(SmallBtn("↓", "下に移動",  OnMoveDown));
                toolbar.Items.Add(SmallBtn("✕", "削除",      OnDelete));
                layout.AddRow(toolbar);

                // ── リスト ────────────────────────────────────────────
                _list = new ListBox { Height = 130 };
                _list.SelectedIndexChanged += (s, e) => OnSelectChange();
                layout.AddRow(_list);

                // ── 区切り ────────────────────────────────────────────
                layout.AddRow(new Panel { Height = 1, BackgroundColor = Colors.DarkGray });

                // ── 入力フォーム ──────────────────────────────────────
                var nameRow = new TableLayout { Spacing = new Size(4, 0) };
                _tbName = new TextBox { PlaceholderText = "名前 (例: 1FL)", Width = 80 };
                _tbElev = new TextBox { PlaceholderText = "高さ mm",        Width = 70 };
                nameRow.Rows.Add(new TableRow(
                    new TableCell(_tbName, true),
                    new TableCell(_tbElev)));
                layout.AddRow(nameRow);

                var btnRow = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 4 };
                btnRow.Items.Add(SmallBtn("+ 追加",  "新規追加",   OnAdd));
                btnRow.Items.Add(SmallBtn("✎ 更新",  "選択を更新", OnUpdate));
                layout.AddRow(btnRow);

                layout.Add(null);
                Content = layout;

                Refresh();
                RhinoDoc.ActiveDocumentChanged += (s, e) => Refresh();
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"[Tanuki] LevelPanel 初期化エラー: {ex.Message}");
                Content = new Label { Text = $"エラー: {ex.Message}" };
            }
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
                int idx = _list.SelectedIndex;
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
                int idx = _list.SelectedIndex;
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
                int idx = _list.SelectedIndex;
                var doc = RhinoDoc.ActiveDoc;
                if (doc == null || idx <= 0) return;
                var project = TanukiProject.Load(doc);
                var tmp = project.Levels[idx - 1];
                project.Levels[idx - 1] = project.Levels[idx];
                project.Levels[idx] = tmp;
                project.Save(doc);
                Refresh();
                _list.SelectedIndex = idx - 1;
            });
        }

        private void OnMoveDown()
        {
            Application.Instance.Invoke(() =>
            {
                int idx = _list.SelectedIndex;
                var doc = RhinoDoc.ActiveDoc;
                if (doc == null || idx < 0) return;
                var project = TanukiProject.Load(doc);
                if (idx >= project.Levels.Count - 1) return;
                var tmp = project.Levels[idx + 1];
                project.Levels[idx + 1] = project.Levels[idx];
                project.Levels[idx] = tmp;
                project.Save(doc);
                Refresh();
                _list.SelectedIndex = idx + 1;
            });
        }

        private void OnSelectChange()
        {
            Application.Instance.Invoke(() =>
            {
                int idx = _list.SelectedIndex;
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
                _list.Items.Clear();
                var doc = RhinoDoc.ActiveDoc;
                if (doc == null) return;
                var project = TanukiProject.Load(doc);
                foreach (var l in project.Levels)
                    _list.Items.Add($"{l.Name}   {l.Elevation:F0} mm");
            });
        }

        private Button SmallBtn(string label, string tip, Action action)
        {
            var b = new Button { Text = label, Height = 26, ToolTip = tip };
            b.Click += (s, e) => action();
            return b;
        }
    }
}
