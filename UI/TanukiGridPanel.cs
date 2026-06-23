using System;
using System.Collections.Generic;
using Eto.Forms;
using Eto.Drawing;
using Rhino;
using Rhino.Input.Custom;
using Rhino.DocObjects;
using Rhino.Geometry;
using Tanuki.Data;
using Tanuki.Generators;

namespace Tanuki.UI
{
    [System.Runtime.InteropServices.Guid("C3D4E5F6-A7B8-9012-CDEF-123456789ABC")]
    public class TanukiGridPanel : Panel
    {
        public static Guid PanelId => typeof(TanukiGridPanel).GUID;

        private GridView _grid;
        private TextBox  _tbName;
        private TextBox  _tbBubble;
        private Label    _lblInfo;

        // データモデル
        private class GridRow
        {
            public string Name   { get; set; }
            public string Origin { get; set; }
            public string Dir    { get; set; }
            public string Length { get; set; }
        }

        public TanukiGridPanel(uint documentSerialNumber)
        {
            try { BuildUi(); }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"[Tanuki] GridPanel error: {ex.Message}");
                Content = new Label { Text = ex.Message };
            }
        }

        private void BuildUi()
        {
            var layout = new DynamicLayout { Padding = new Padding(4), Spacing = new Size(0, 3) };

            layout.AddRow(new Label { Text = "⊕ 通り芯" });

            // ── ツールバー ────────────────────────────────────────
            var tb = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 2 };
            tb.Items.Add(Btn("+ 描く",   "2点で新規作成",     OnDraw));
            tb.Items.Add(Btn("↩ 選択",  "既存の線から登録",   OnPick));
            tb.Items.Add(Btn("⊞ 均等",  "等間隔に一括作成",   OnBatch));
            tb.Items.Add(Btn("✕",       "選択行を削除",       OnDelete));
            layout.AddRow(tb);

            // ── GridView ─────────────────────────────────────────
            _grid = new GridView { Height = 160, ShowHeader = true };
            _grid.Columns.Add(new GridColumn
            {
                HeaderText = "名前",
                Width      = 50,
                DataCell   = new TextBoxCell { Binding = Binding.Property<GridRow, string>(r => r.Name) }
            });
            _grid.Columns.Add(new GridColumn
            {
                HeaderText = "始点",
                Width      = 100,
                DataCell   = new TextBoxCell { Binding = Binding.Property<GridRow, string>(r => r.Origin) }
            });
            _grid.Columns.Add(new GridColumn
            {
                HeaderText = "方向",
                Width      = 80,
                DataCell   = new TextBoxCell { Binding = Binding.Property<GridRow, string>(r => r.Dir) }
            });
            _grid.Columns.Add(new GridColumn
            {
                HeaderText = "長さ",
                Width      = 60,
                DataCell   = new TextBoxCell { Binding = Binding.Property<GridRow, string>(r => r.Length) }
            });
            _grid.SelectionChanged += (s, e) => OnSelect();
            layout.AddRow(_grid);

            // ── 詳細/名前変更 ─────────────────────────────────────
            _lblInfo = new Label { Text = "", TextColor = Colors.Gray };
            layout.AddRow(_lblInfo);

            layout.AddRow(new Panel { Height = 1, BackgroundColor = Colors.DarkGray });

            var renameRow = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 4 };
            _tbName = new TextBox { PlaceholderText = "名前を変更", Width = 80 };
            renameRow.Items.Add(_tbName);
            renameRow.Items.Add(Btn("変更", "名前を変更", OnRename));
            layout.AddRow(renameRow);

            layout.AddRow(new Panel { Height = 1, BackgroundColor = Colors.DarkGray });

            // バルーン径
            var bubbleRow = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 4 };
            bubbleRow.Items.Add(new Label { Text = "バルーン径:", VerticalAlignment = VerticalAlignment.Center });
            _tbBubble = new TextBox { Text = "400", Width = 60 };
            bubbleRow.Items.Add(_tbBubble);
            bubbleRow.Items.Add(new Label { Text = "mm", VerticalAlignment = VerticalAlignment.Center });
            bubbleRow.Items.Add(Btn("適用", "バルーン径を変更して再描画", OnApplyBubble));
            layout.AddRow(bubbleRow);

            layout.Add(null);
            Content = layout;

            Refresh();
            RhinoDoc.ActiveDocumentChanged += (s, e) => { try { Refresh(); } catch { } };
            TanukiPlugin.GridLinesChanged  += (s, e) => { try { Refresh(); } catch { } };
        }

        // ── Actions ──────────────────────────────────────────────

        private void OnDraw()
        {
            RhinoApp.InvokeOnUiThread(new Action(() =>
            {
                var doc = RhinoDoc.ActiveDoc;
                if (doc == null) return;
                var project = TanukiProject.Load(doc);

                var gn = new GetString(); gn.SetCommandPrompt("通り芯の名前"); gn.Get();
                if (gn.CommandResult() != Rhino.Commands.Result.Success) return;

                var gp1 = new GetPoint(); gp1.SetCommandPrompt("始点"); gp1.Get();
                if (gp1.CommandResult() != Rhino.Commands.Result.Success) return;

                var gp2 = new GetPoint();
                gp2.SetCommandPrompt("終点");
                gp2.SetBasePoint(gp1.Point(), false);
                gp2.DrawLineFromPoint(gp1.Point(), true);
                gp2.Get();
                if (gp2.CommandResult() != Rhino.Commands.Result.Success) return;

                var dir = gp2.Point() - gp1.Point();
                dir.Unitize();

                project.GridLines.Add(new GridLine
                {
                    Name       = gn.StringResult(),
                    OriginX    = gp1.Point().X,
                    OriginY    = gp1.Point().Y,
                    DirectionX = dir.X,
                    DirectionY = dir.Y,
                    Length     = gp1.Point().DistanceTo(gp2.Point())
                });
                GridLineDrawer.SyncAll(doc, project.GridLines);
                project.Save(doc);
                Refresh();
            }));
        }

        private void OnPick()
        {
            RhinoApp.InvokeOnUiThread(new Action(() =>
            {
                var doc = RhinoDoc.ActiveDoc;
                if (doc == null) return;
                var project = TanukiProject.Load(doc);

                var gobj = new GetObject();
                gobj.SetCommandPrompt("通り芯にする線を選択");
                gobj.GeometryFilter = ObjectType.Curve;
                gobj.Get();
                if (gobj.CommandResult() != Rhino.Commands.Result.Success) return;

                var curve = gobj.Object(0).Curve();
                if (curve == null) return;

                var gn = new GetString(); gn.SetCommandPrompt("通り芯の名前"); gn.Get();
                if (gn.CommandResult() != Rhino.Commands.Result.Success) return;

                var start = curve.PointAtStart;
                var end   = curve.PointAtEnd;
                var dir   = end - start; dir.Unitize();

                project.GridLines.Add(new GridLine
                {
                    Name       = gn.StringResult(),
                    OriginX    = start.X,
                    OriginY    = start.Y,
                    DirectionX = dir.X,
                    DirectionY = dir.Y,
                    Length     = start.DistanceTo(end)
                });
                GridLineDrawer.SyncAll(doc, project.GridLines);
                project.Save(doc);
                Refresh();
            }));
        }

        private void OnBatch()
        {
            RhinoApp.InvokeOnUiThread(new Action(() =>
            {
                var doc = RhinoDoc.ActiveDoc;
                if (doc == null) return;
                var project = TanukiProject.Load(doc);

                var gPrefix = new GetString();
                gPrefix.SetCommandPrompt("名前のプレフィックス (例: X → X1,X2...)");
                gPrefix.SetDefaultString("A");
                gPrefix.Get();
                if (gPrefix.CommandResult() != Rhino.Commands.Result.Success) return;
                string prefix = gPrefix.StringResult();

                var gCount = new GetInteger();
                gCount.SetCommandPrompt("本数");
                gCount.SetDefaultInteger(4);
                gCount.SetLowerLimit(2, false);
                gCount.Get();
                if (gCount.CommandResult() != Rhino.Commands.Result.Success) return;
                int count = gCount.Number();

                var gp1 = new GetPoint();
                gp1.SetCommandPrompt("第1本目の始点");
                gp1.Get();
                if (gp1.CommandResult() != Rhino.Commands.Result.Success) return;
                var pt1 = gp1.Point();

                var gp2 = new GetPoint();
                gp2.SetCommandPrompt("第1本目の終点（線の方向と長さを定義）");
                gp2.SetBasePoint(pt1, false);
                gp2.DrawLineFromPoint(pt1, true);
                gp2.Get();
                if (gp2.CommandResult() != Rhino.Commands.Result.Success) return;
                var pt2 = gp2.Point();

                var gp3 = new GetPoint();
                gp3.SetCommandPrompt("第2本目の始点（間隔と方向を定義）");
                gp3.Get();
                if (gp3.CommandResult() != Rhino.Commands.Result.Success) return;
                var pt3 = gp3.Point();

                var lineVec    = pt2 - pt1;
                var spacingVec = pt3 - pt1;

                for (int i = 0; i < count; i++)
                {
                    var origin = new Rhino.Geometry.Point3d(
                        pt1.X + i * spacingVec.X,
                        pt1.Y + i * spacingVec.Y,
                        0);
                    var end = new Rhino.Geometry.Point3d(
                        pt2.X + i * spacingVec.X,
                        pt2.Y + i * spacingVec.Y,
                        0);

                    var dir = end - origin;
                    dir.Unitize();

                    project.GridLines.Add(new GridLine
                    {
                        Name       = prefix + (i + 1).ToString(),
                        OriginX    = origin.X,
                        OriginY    = origin.Y,
                        DirectionX = dir.X,
                        DirectionY = dir.Y,
                        Length     = origin.DistanceTo(end)
                    });
                }

                GridLineDrawer.SyncAll(doc, project.GridLines);
                project.Save(doc);
                Refresh();
            }));
        }

        private void OnDelete()
        {
            Application.Instance.Invoke(() =>
            {
                int idx = _grid.SelectedRow;
                var doc = RhinoDoc.ActiveDoc;
                if (doc == null || idx < 0) return;
                var project = TanukiProject.Load(doc);
                if (idx >= project.GridLines.Count) return;
                project.GridLines.RemoveAt(idx);
                GridLineDrawer.SyncAll(doc, project.GridLines);
                project.Save(doc);
                Refresh();
            });
        }

        private void OnRename()
        {
            Application.Instance.Invoke(() =>
            {
                int idx = _grid.SelectedRow;
                var doc = RhinoDoc.ActiveDoc;
                if (doc == null || idx < 0 || string.IsNullOrWhiteSpace(_tbName.Text)) return;
                var project = TanukiProject.Load(doc);
                if (idx >= project.GridLines.Count) return;
                project.GridLines[idx].Name = _tbName.Text.Trim();
                GridLineDrawer.SyncAll(doc, project.GridLines);
                project.Save(doc);
                Refresh();
            });
        }

        private void OnSelect()
        {
            Application.Instance.Invoke(() =>
            {
                int idx = _grid.SelectedRow;
                var doc = RhinoDoc.ActiveDoc;
                if (doc == null || idx < 0) { _lblInfo.Text = ""; return; }
                var project = TanukiProject.Load(doc);
                if (idx >= project.GridLines.Count) { _lblInfo.Text = ""; return; }
                var g = project.GridLines[idx];
                _lblInfo.Text = $"始点 ({g.OriginX:F0}, {g.OriginY:F0})  長さ {g.Length:F0} mm";
                _tbName.Text  = g.Name;
            });
        }

        private void Refresh()
        {
            if (IsDisposed) return;
            Application.Instance.Invoke(() =>
            {
                if (IsDisposed) return;
                var doc = RhinoDoc.ActiveDoc;
                var project = doc != null ? TanukiProject.Load(doc) : null;
                var rows = new List<GridRow>();
                if (project != null)
                {
                    foreach (var g in project.GridLines)
                        rows.Add(new GridRow
                        {
                            Name   = g.Name,
                            Origin = $"({g.OriginX:F0},{g.OriginY:F0})",
                            Dir    = $"({g.DirectionX:F2},{g.DirectionY:F2})",
                            Length = $"{g.Length:F0}"
                        });
                    if (_tbBubble != null)
                        _tbBubble.Text = project.BubbleRadius.ToString("F0");
                }
                _grid.DataStore = rows;
            });
        }

        private void OnApplyBubble()
        {
            Application.Instance.Invoke(() =>
            {
                var doc = RhinoDoc.ActiveDoc;
                if (doc == null) return;
                if (!double.TryParse(_tbBubble.Text, out double radius) || radius <= 0) return;
                var project = TanukiProject.Load(doc);
                project.BubbleRadius = radius;
                GridLineDrawer.SyncAll(doc, project.GridLines, radius);
                project.Save(doc);
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
