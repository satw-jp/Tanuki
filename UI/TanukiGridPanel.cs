using System;
using System.Linq;
using Eto.Forms;
using Eto.Drawing;
using Rhino;
using Rhino.Input.Custom;
using Rhino.DocObjects;
using Rhino.Geometry;
using Tanuki.Data;

namespace Tanuki.UI
{
    [System.Runtime.InteropServices.Guid("C3D4E5F6-A7B8-9012-CDEF-123456789ABC")]
    public class TanukiGridPanel : Panel
    {
        public static Guid PanelId => typeof(TanukiGridPanel).GUID;

        private ListBox _list;
        private TextBox _tbName;
        private Label   _lblDetail;

        public TanukiGridPanel(uint documentSerialNumber)
        {
            try
            {
            BuildUi();
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"[Tanuki] GridPanel 初期化エラー: {ex.Message}");
                Content = new Label { Text = $"エラー: {ex.Message}" };
            }
        }

        private void BuildUi()
        {
            var layout = new DynamicLayout { Padding = new Padding(4), Spacing = new Size(0, 3) };

            // ── ツールバー ────────────────────────────────────────
            var toolbar = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 2 };
            toolbar.Items.Add(SmallBtn("✎ 描く",    "2点で新規作成",       OnDraw));
            toolbar.Items.Add(SmallBtn("↩ 選択",   "既存の線から登録",     OnPick));
            toolbar.Items.Add(SmallBtn("✕ 削除",   "選択した通り芯を削除", OnDelete));
            layout.AddRow(toolbar);

            // ── リスト ────────────────────────────────────────────
            _list = new ListBox { Height = 140 };
            _list.SelectedIndexChanged += (s, e) => UpdateDetail();
            layout.AddRow(_list);

            // ── 詳細 ──────────────────────────────────────────────
            _lblDetail = new Label { Text = "", TextColor = Colors.Gray };
            layout.AddRow(_lblDetail);

            // ── 名前変更 ──────────────────────────────────────────
            layout.AddRow(new Panel { Height = 1, BackgroundColor = Colors.DarkGray });

            var renameRow = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 4 };
            _tbName = new TextBox { PlaceholderText = "名前を変更", Width = 80 };
            var btnRename = SmallBtn("変更", "名前を変更", OnRename);
            renameRow.Items.Add(_tbName);
            renameRow.Items.Add(btnRename);
            layout.AddRow(renameRow);

            layout.Add(null);
            Content = layout;

            Refresh();
            RhinoDoc.ActiveDocumentChanged += (s, e) => Refresh();
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
                project.Save(doc);
                Refresh();
            }));
        }

        private void OnDelete()
        {
            Application.Instance.Invoke(() =>
            {
                int idx = _list.SelectedIndex;
                var doc = RhinoDoc.ActiveDoc;
                if (doc == null || idx < 0) return;
                var project = TanukiProject.Load(doc);
                if (idx >= project.GridLines.Count) return;
                project.GridLines.RemoveAt(idx);
                project.Save(doc);
                Refresh();
            });
        }

        private void OnRename()
        {
            Application.Instance.Invoke(() =>
            {
                int idx = _list.SelectedIndex;
                var doc = RhinoDoc.ActiveDoc;
                if (doc == null || idx < 0 || string.IsNullOrWhiteSpace(_tbName.Text)) return;
                var project = TanukiProject.Load(doc);
                if (idx >= project.GridLines.Count) return;
                project.GridLines[idx].Name = _tbName.Text.Trim();
                project.Save(doc);
                Refresh();
            });
        }

        private void UpdateDetail()
        {
            Application.Instance.Invoke(() =>
            {
                int idx = _list.SelectedIndex;
                var doc = RhinoDoc.ActiveDoc;
                if (doc == null || idx < 0) { _lblDetail.Text = ""; return; }
                var project = TanukiProject.Load(doc);
                if (idx >= project.GridLines.Count) { _lblDetail.Text = ""; return; }
                var g = project.GridLines[idx];
                _lblDetail.Text = $"始点 ({g.OriginX:F0}, {g.OriginY:F0})  長 {g.Length:F0}mm";
                _tbName.Text    = g.Name;
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
                foreach (var g in project.GridLines)
                    _list.Items.Add($"{g.Name}   ({g.OriginX:F0}, {g.OriginY:F0})  dir({g.DirectionX:F2},{g.DirectionY:F2})");
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
