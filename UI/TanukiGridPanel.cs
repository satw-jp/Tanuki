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
        private bool     _suppressViewportSelect = false;

        private class GridRow
        {
            public string Group  { get; set; }
            public string Name   { get; set; }
            public string Origin { get; set; }
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
            tb.Items.Add(Btn("+ 描く",   "2点で新規作成",               OnDraw));
            tb.Items.Add(Btn("↩ 選択",  "既存の線から登録",             OnPick));
            tb.Items.Add(Btn("⊞ 一括",  "方向・ピッチ指定で一括作成",   OnBatch));
            tb.Items.Add(Btn("+ ピッチ", "選択行からピッチ追加",         OnAddByPitch));
            tb.Items.Add(Btn("✕",       "選択行を削除",                 OnDelete));
            layout.AddRow(tb);

            // ── GridView ─────────────────────────────────────────
            _grid = new GridView { Height = 160, ShowHeader = true };
            _grid.Columns.Add(new GridColumn
            {
                HeaderText = "軸",
                Width      = 30,
                DataCell   = new TextBoxCell { Binding = Binding.Property<GridRow, string>(r => r.Group) }
            });
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
                HeaderText = "長さ",
                Width      = 60,
                DataCell   = new TextBoxCell { Binding = Binding.Property<GridRow, string>(r => r.Length) }
            });
            _grid.SelectionChanged += (s, e) => OnSelect();
            layout.AddRow(_grid);

            _lblInfo = new Label { Text = "", TextColor = Colors.Gray };
            layout.AddRow(_lblInfo);

            layout.AddRow(new Panel { Height = 1, BackgroundColor = Colors.DarkGray });

            // ── 名前変更 ──────────────────────────────────────────
            var renameRow = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 4 };
            _tbName = new TextBox { PlaceholderText = "名前を変更", Width = 80 };
            renameRow.Items.Add(_tbName);
            renameRow.Items.Add(Btn("変更", "名前を変更", OnRename));
            layout.AddRow(renameRow);

            layout.AddRow(new Panel { Height = 1, BackgroundColor = Colors.DarkGray });

            // ── バルーン径 ────────────────────────────────────────
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
            RhinoDoc.ActiveDocumentChanged      += (s, e) => { try { Refresh(); } catch { } };
            TanukiPlugin.GridLinesChanged        += (s, e) => { try { Refresh(); } catch { } };
            TanukiPlugin.GridLineObjectSelected  += OnGridLineObjectSelected;
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

                var dir = gp2.Point() - gp1.Point(); dir.Unitize();

                project.GridLines.Add(new GridLine
                {
                    Name       = gn.StringResult().Replace("::", "_"),
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
                    Name       = gn.StringResult().Replace("::", "_"),
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

        // ── 一括生成: 方向・ピッチ指定 ───────────────────────────
        private void OnBatch()
        {
            RhinoApp.InvokeOnUiThread(new Action(() =>
            {
                var doc = RhinoDoc.ActiveDoc;
                if (doc == null) return;
                var project = TanukiProject.Load(doc);

                // 1. 方向選択
                var gDir = new GetOption();
                gDir.SetCommandPrompt("通り芯の方向を選択");
                gDir.AddOption("X軸_縦通り芯_横並び");
                gDir.AddOption("Y軸_横通り芯_縦並び");
                gDir.AddOption("カスタム");
                gDir.Get();
                if (gDir.CommandResult() != Rhino.Commands.Result.Success) return;
                int dirChoice = gDir.Option().Index; // 1=X, 2=Y, 3=Custom

                string groupTag = dirChoice == 1 ? "X" : dirChoice == 2 ? "Y" : "";

                // 2. プレフィックスと開始番号
                var gPrefix = new GetString();
                gPrefix.SetCommandPrompt("名前のプレフィックス (例: X → X1, X2...)");
                gPrefix.SetDefaultString(groupTag.Length > 0 ? groupTag : "A");
                gPrefix.Get();
                if (gPrefix.CommandResult() != Rhino.Commands.Result.Success) return;
                string prefix = gPrefix.StringResult();

                var gStart = new GetInteger();
                gStart.SetCommandPrompt("開始番号");
                gStart.SetDefaultInteger(1);
                gStart.SetLowerLimit(1, false);
                gStart.Get();
                if (gStart.CommandResult() != Rhino.Commands.Result.Success) return;
                int startNum = gStart.Number();

                var gCount = new GetInteger();
                gCount.SetCommandPrompt("本数");
                gCount.SetDefaultInteger(5);
                gCount.SetLowerLimit(1, false);
                gCount.Get();
                if (gCount.CommandResult() != Rhino.Commands.Result.Success) return;
                int count = gCount.Number();

                // 3. 第1本目の始点
                var gp1 = new GetPoint();
                gp1.SetCommandPrompt("第1本目の始点をクリック");
                gp1.Get();
                if (gp1.CommandResult() != Rhino.Commands.Result.Success) return;
                var pt1 = gp1.Point();

                // 4. 線の方向と長さ
                Vector3d lineDir;
                double   lineLen;
                if (dirChoice == 1) // X軸通り芯: 通り芯はY方向に走る
                {
                    lineDir = new Vector3d(0, 1, 0);
                    var gLen = new GetNumber();
                    gLen.SetCommandPrompt("通り芯の長さ (mm)");
                    gLen.SetDefaultNumber(20000);
                    gLen.Get();
                    if (gLen.CommandResult() != Rhino.Commands.Result.Success) return;
                    lineLen = gLen.Number();
                }
                else if (dirChoice == 2) // Y軸通り芯: 通り芯はX方向に走る
                {
                    lineDir = new Vector3d(1, 0, 0);
                    var gLen = new GetNumber();
                    gLen.SetCommandPrompt("通り芯の長さ (mm)");
                    gLen.SetDefaultNumber(20000);
                    gLen.Get();
                    if (gLen.CommandResult() != Rhino.Commands.Result.Success) return;
                    lineLen = gLen.Number();
                }
                else // カスタム: 2点目で方向・長さを指定
                {
                    var gp2 = new GetPoint();
                    gp2.SetCommandPrompt("第1本目の終点（方向・長さを定義）");
                    gp2.SetBasePoint(pt1, false);
                    gp2.DrawLineFromPoint(pt1, true);
                    gp2.Get();
                    if (gp2.CommandResult() != Rhino.Commands.Result.Success) return;
                    var vec = gp2.Point() - pt1;
                    lineLen = vec.Length;
                    lineDir = vec; lineDir.Unitize();
                }

                // 5. ピッチ
                var gPitch = new GetNumber();
                gPitch.SetCommandPrompt("ピッチ (mm)");
                gPitch.SetDefaultNumber(8000);
                gPitch.SetLowerLimit(1, false);
                gPitch.Get();
                if (gPitch.CommandResult() != Rhino.Commands.Result.Success) return;
                double pitch = gPitch.Number();

                // スペーシングベクトル（線の方向に垂直）
                Vector3d spacingVec;
                if (dirChoice == 1) // X通り芯 → X方向に並ぶ
                    spacingVec = new Vector3d(pitch, 0, 0);
                else if (dirChoice == 2) // Y通り芯 → Y方向に並ぶ
                    spacingVec = new Vector3d(0, pitch, 0);
                else // カスタム: lineDir に垂直
                {
                    spacingVec = new Vector3d(-lineDir.Y, lineDir.X, 0);
                    spacingVec *= pitch;
                }

                int sortBase = project.GridLines.FindAll(g => g.GroupTag == groupTag).Count;

                for (int i = 0; i < count; i++)
                {
                    project.GridLines.Add(new GridLine
                    {
                        Name       = prefix + (startNum + i).ToString(),
                        OriginX    = pt1.X + i * spacingVec.X,
                        OriginY    = pt1.Y + i * spacingVec.Y,
                        DirectionX = lineDir.X,
                        DirectionY = lineDir.Y,
                        Length     = lineLen,
                        GroupTag   = groupTag,
                        SortIndex  = sortBase + i,
                    });
                }

                GridLineDrawer.SyncAll(doc, project.GridLines);
                project.Save(doc);
                Refresh();
            }));
        }

        // ── 選択行からピッチ追加 ─────────────────────────────────
        private void OnAddByPitch()
        {
            RhinoApp.InvokeOnUiThread(new Action(() =>
            {
                int idx = _grid.SelectedRow;
                var doc = RhinoDoc.ActiveDoc;
                if (doc == null || idx < 0) return;
                var project = TanukiProject.Load(doc);
                if (idx >= project.GridLines.Count) return;
                var last = project.GridLines[idx];

                var gPitch = new GetNumber();
                gPitch.SetCommandPrompt("ピッチ (mm) — 選択した通り芯から");
                gPitch.SetDefaultNumber(8000);
                gPitch.SetLowerLimit(1, false);
                gPitch.Get();
                if (gPitch.CommandResult() != Rhino.Commands.Result.Success) return;
                double pitch = gPitch.Number();

                var gn = new GetString();
                gn.SetCommandPrompt("新しい通り芯の名前");
                gn.SetDefaultString(last.Name + "'");
                gn.Get();
                if (gn.CommandResult() != Rhino.Commands.Result.Success) return;

                // 線の法線方向（線の向きに垂直）にオフセット
                var perpDir = new Vector3d(-last.DirectionY, last.DirectionX, 0);
                perpDir.Unitize();

                project.GridLines.Add(new GridLine
                {
                    Name       = gn.StringResult().Replace("::", "_"),
                    OriginX    = last.OriginX + perpDir.X * pitch,
                    OriginY    = last.OriginY + perpDir.Y * pitch,
                    DirectionX = last.DirectionX,
                    DirectionY = last.DirectionY,
                    Length     = last.Length,
                    GroupTag   = last.GroupTag,
                    SortIndex  = last.SortIndex + 1,
                });

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
                if (doc == null || idx < 0) return;
                string trimmed = _tbName.Text == null ? "" : _tbName.Text.Trim().Replace("::", "_");
                if (trimmed.Length == 0) return;
                var project = TanukiProject.Load(doc);
                if (idx >= project.GridLines.Count) return;
                project.GridLines[idx].Name = trimmed;
                GridLineDrawer.SyncAll(doc, project.GridLines);
                project.Save(doc);
                Refresh();
            });
        }

        private void OnSelect()
        {
            if (_suppressViewportSelect) return;
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

                if (g.LineObjectId != Guid.Empty)
                {
                    doc.Objects.UnselectAll();
                    doc.Objects.Select(g.LineObjectId);
                    doc.Views.Redraw();
                }
            });
        }

        private void OnGridLineObjectSelected(object sender, Guid id)
        {
            Application.Instance.Invoke(() =>
            {
                if (IsDisposed) return;
                var doc = RhinoDoc.ActiveDoc;
                if (doc == null) return;
                var project = TanukiProject.Load(doc);
                int idx = project.GridLines.FindIndex(gl => gl.LineObjectId == id);
                if (idx < 0) return;
                _suppressViewportSelect = true;
                _grid.SelectedRow = idx;
                _suppressViewportSelect = false;
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
                            Group  = g.GroupTag,
                            Name   = g.Name,
                            Origin = $"({g.OriginX:F0},{g.OriginY:F0})",
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
