using System;
using System.Collections.Generic;
using Eto.Forms;
using Eto.Drawing;
using Rhino;
using Tanuki.Data;
using Tanuki.Generators;

namespace Tanuki.UI
{
    [System.Runtime.InteropServices.Guid("E5F6A7B8-C9D0-1234-EF01-3456789ABCDE")]
    public class TanukiSectionPanel : Panel
    {
        public static Guid PanelId => typeof(TanukiSectionPanel).GUID;

        private GridView    _grid;
        private TextBox     _tbRename;
        private Label       _lblCount;
        private CheckBox    _cbViewMesh;
        private TextBox     _tbViewDepth;
        private TextBox     _tbLabelHeight;
        private TextBox     _tbCutHeight;
        private RadioButton _rbLineType;
        private RadioButton _rbOriginal;
        private bool        _suppressViewportSelect = false;

        private class ViewRow
        {
            public string Icon { get; set; }
            public string Name { get; set; }
            public string Info { get; set; }
        }

        public TanukiSectionPanel(uint documentSerialNumber)
        {
            try { BuildUi(); }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"[Tanuki] SectionPanel error: {ex.Message}");
                Content = new Label { Text = ex.Message };
            }
        }

        private void BuildUi()
        {
            var layout = new DynamicLayout { Padding = new Padding(4), Spacing = new Size(0, 3) };

            layout.AddRow(new Label { Text = "📑 図面一覧" });

            // ── ツールバー ────────────────────────────────────────
            var tb = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 2 };
            tb.Items.Add(Btn("断面+", "新規断面図", OnNewSection));
            tb.Items.Add(Btn("立面+", "新規立面図", OnNewElevation));
            tb.Items.Add(Btn("🔄", "再生成",       OnRegenerate));
            tb.Items.Add(Btn("📍", "マーカー追加", OnAddMarker));
            tb.Items.Add(Btn("🔍", "ズーム",       OnZoom));
            tb.Items.Add(Btn("✕",  "削除",         OnDelete));
            layout.AddRow(tb);

            // ── GridView ─────────────────────────────────────────
            _grid = new GridView { Height = 160, ShowHeader = true };
            _grid.Columns.Add(new GridColumn
            {
                HeaderText = "",
                Width      = 24,
                DataCell   = new TextBoxCell { Binding = Binding.Property<ViewRow, string>(r => r.Icon) }
            });
            _grid.Columns.Add(new GridColumn
            {
                HeaderText = "名前",
                Width      = 110,
                DataCell   = new TextBoxCell { Binding = Binding.Property<ViewRow, string>(r => r.Name) }
            });
            _grid.Columns.Add(new GridColumn
            {
                HeaderText = "情報",
                Width      = 80,
                DataCell   = new TextBoxCell { Binding = Binding.Property<ViewRow, string>(r => r.Info) }
            });
            _grid.SelectionChanged += (s, e) => OnSelect();
            layout.AddRow(_grid);

            _lblCount = new Label { Text = "0 図面", TextColor = Colors.Gray };
            layout.AddRow(_lblCount);

            layout.AddRow(new Panel { Height = 1, BackgroundColor = Colors.DarkGray });

            // ── リネーム ──────────────────────────────────────────
            var renameRow = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 4 };
            _tbRename = new TextBox { PlaceholderText = "新しい名前", Width = 100 };
            renameRow.Items.Add(_tbRename);
            renameRow.Items.Add(Btn("✎ 変更", "リネーム", OnRename));
            layout.AddRow(renameRow);

            layout.AddRow(new Panel { Height = 1, BackgroundColor = Colors.DarkGray });

            // ── 選択図面の設定 ──────────────────────────────────────
            // レイヤーモード
            _rbLineType = new RadioButton { Text = "線種", Checked = true };
            _rbOriginal = new RadioButton(_rbLineType) { Text = "元レイヤー" };
            var modeRow = new StackLayout
            {
                Orientation = Orientation.Horizontal, Spacing = 6,
                Items = { new Label { Text = "レイヤー:", VerticalAlignment = VerticalAlignment.Center },
                          _rbLineType, _rbOriginal }
            };
            layout.AddRow(modeRow);

            // 図面名文字高さ
            var labelRow = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 4 };
            labelRow.Items.Add(new Label { Text = "図面名高さ:", VerticalAlignment = VerticalAlignment.Center });
            _tbLabelHeight = new TextBox { Text = "500", Width = 56 };
            labelRow.Items.Add(_tbLabelHeight);
            labelRow.Items.Add(new Label { Text = "mm", VerticalAlignment = VerticalAlignment.Center });
            layout.AddRow(labelRow);

            // 切断高さ（平面図/天井伏図のみ有効）
            var cutHRow = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 4 };
            cutHRow.Items.Add(new Label { Text = "切断高さ:", VerticalAlignment = VerticalAlignment.Center });
            _tbCutHeight = new TextBox { Text = "1000", Width = 56 };
            cutHRow.Items.Add(_tbCutHeight);
            cutHRow.Items.Add(new Label { Text = "mm", VerticalAlignment = VerticalAlignment.Center });
            layout.AddRow(cutHRow);

            // メッシュ無視
            var perfRow = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 4 };
            _cbViewMesh = new CheckBox { Text = "メッシュ無視" };
            perfRow.Items.Add(_cbViewMesh);
            layout.AddRow(perfRow);

            // 視線奥行き
            var depthRow = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 4 };
            depthRow.Items.Add(new Label { Text = "奥行き:", VerticalAlignment = VerticalAlignment.Center });
            _tbViewDepth = new TextBox { Text = "0", Width = 56 };
            depthRow.Items.Add(_tbViewDepth);
            depthRow.Items.Add(new Label { Text = "mm", VerticalAlignment = VerticalAlignment.Center });
            depthRow.Items.Add(Btn("適用+再生成", "この図面に適用して再生成", OnApplyViewPerf));
            layout.AddRow(depthRow);

            layout.Add(null);
            Content = layout;

            Refresh();
            RhinoDoc.ActiveDocumentChanged    += (s, e) => { try { Refresh(); } catch { } };
            TanukiPlugin.ViewsChanged         += (s, e) => { try { Refresh(); } catch { } };
            TanukiPlugin.MarkerObjectSelected += OnMarkerObjectSelected;
        }

        // ── Actions ──────────────────────────────────────────────

        private void OnNewSection()
        {
            RhinoApp.InvokeOnUiThread(new Action(() => RhinoApp.RunScript("_TanukiSection", false)));
        }

        private void OnNewElevation()
        {
            RhinoApp.InvokeOnUiThread(new Action(() => RhinoApp.RunScript("_TanukiElevation", false)));
        }

        private void OnSelect()
        {
            if (_suppressViewportSelect) return;
            Application.Instance.Invoke(() =>
            {
                int idx = _grid.SelectedRow;
                var doc = RhinoDoc.ActiveDoc;
                if (doc == null || idx < 0) return;
                var project = TanukiProject.Load(doc);
                if (idx >= project.Views.Count) return;
                var view = project.Views[idx];
                _tbRename.Text      = view.Name;
                _cbViewMesh.Checked = !view.IncludeMeshes;
                _tbViewDepth.Text   = view.ViewDepth.ToString("F0");
                _tbLabelHeight.Text = view.LabelTextHeight.ToString("F0");
                _tbCutHeight.Text   = view.CutHeight.ToString("F0");
                _tbCutHeight.Enabled = view.Type == ViewType.FloorPlan || view.Type == ViewType.RCP;
                _rbLineType.Checked = view.LayerMode == LayerMode.LineType;
                _rbOriginal.Checked = view.LayerMode == LayerMode.OriginalLayer;

                // マーカー線 + 生成図面オブジェクトをまとめて選択
                doc.Objects.UnselectAll();
                if (view.MarkerObjectId != Guid.Empty)
                    doc.Objects.Select(view.MarkerObjectId);
                if (view.MarkerIndicatorIds != null)
                    foreach (var id in view.MarkerIndicatorIds)
                        if (id != Guid.Empty) doc.Objects.Select(id);
                SelectDrawingLayer(doc, view.GetLayerKey());
                doc.Views.Redraw();
            });
        }

        // ビューポートでマーカーが選択されたときにパネルの行を反応させる
        private void OnMarkerObjectSelected(object sender, Guid id)
        {
            Application.Instance.Invoke(() =>
            {
                if (IsDisposed) return;
                var doc = RhinoDoc.ActiveDoc;
                if (doc == null) return;
                var project = TanukiProject.Load(doc);
                int idx = project.Views.FindIndex(v => v.MarkerObjectId == id);
                if (idx < 0) return;
                _suppressViewportSelect = true;
                _grid.SelectedRow = idx;
                _suppressViewportSelect = false;
            });
        }

        private void OnRegenerate()
        {
            Application.Instance.Invoke(() =>
            {
                int idx = _grid.SelectedRow;
                var doc = RhinoDoc.ActiveDoc;
                if (doc == null || idx < 0) return;
                var project = TanukiProject.Load(doc);
                if (idx >= project.Views.Count) return;
                var view = project.Views[idx];
                RhinoApp.InvokeOnUiThread(new Action(() => ViewGenerator.Generate(doc, view, project)));
            });
        }

        private void OnZoom()
        {
            Application.Instance.Invoke(() =>
            {
                int idx = _grid.SelectedRow;
                var doc = RhinoDoc.ActiveDoc;
                if (doc == null || idx < 0) return;
                var project = TanukiProject.Load(doc);
                if (idx >= project.Views.Count) return;
                string layerKey = project.Views[idx].GetLayerKey();

                RhinoApp.InvokeOnUiThread(new Action(() =>
                {
                    ZoomToViewLayer(doc, layerKey);
                }));
            });
        }

        private void ZoomToViewLayer(RhinoDoc doc, string layerKey)
        {
            doc.Objects.UnselectAll();
            string layerPath = $"Tanuki::{layerKey}";
            int li = doc.Layers.FindByFullPath(layerPath, RhinoMath.UnsetIntIndex);
            if (li == RhinoMath.UnsetIntIndex) return;
            bool any = false;
            var objs = doc.Objects.FindByLayer(doc.Layers[li]);
            if (objs != null) foreach (var o in objs) { o.Select(true); any = true; }
            var children = doc.Layers[li].GetChildren();
            if (children != null)
                foreach (var child in children)
                {
                    var co = doc.Objects.FindByLayer(child);
                    if (co != null) foreach (var o in co) { o.Select(true); any = true; }
                }
            if (any)
            {
                RhinoApp.RunScript("_Zoom _Selected", false);
                doc.Objects.UnselectAll();
            }
        }

        private void SelectDrawingLayer(RhinoDoc doc, string layerKey)
        {
            string path = $"Tanuki::{layerKey}";
            int li = doc.Layers.FindByFullPath(path, RhinoMath.UnsetIntIndex);
            if (li == RhinoMath.UnsetIntIndex) return;
            Action<int> selectAll = null;
            selectAll = (idx) =>
            {
                var objs = doc.Objects.FindByLayer(doc.Layers[idx]);
                if (objs != null) foreach (var o in objs) o.Select(true);
                var ch = doc.Layers[idx].GetChildren();
                if (ch != null) foreach (var c in ch) selectAll(c.Index);
            };
            selectAll(li);
        }

        private void OnAddMarker()
        {
            Application.Instance.Invoke(() =>
            {
                int idx = _grid.SelectedRow;
                var doc = RhinoDoc.ActiveDoc;
                if (doc == null || idx < 0) return;
                var project = TanukiProject.Load(doc);
                if (idx >= project.Views.Count) return;
                var view = project.Views[idx];

                if (view.Type == ViewType.FloorPlan || view.Type == ViewType.RCP) return;

                // 既存マーカーを削除
                if (view.MarkerObjectId != Guid.Empty)
                    doc.Objects.Delete(view.MarkerObjectId, true);
                Tanuki.Generators.MarkerDrawer.DeleteIndicators(doc, view.MarkerIndicatorIds);

                var markerLine = new Rhino.Geometry.Line(
                    new Rhino.Geometry.Point3d(view.CutStartX, view.CutStartY, 0),
                    new Rhino.Geometry.Point3d(view.CutEndX,   view.CutEndY,   0));
                int layerIdx = Tanuki.Generators.MarkerDrawer.EnsureMarkersLayer(doc);
                var color = view.Type == ViewType.Elevation
                    ? System.Drawing.Color.Cyan
                    : System.Drawing.Color.Magenta;

                var lineAttr = new Rhino.DocObjects.ObjectAttributes
                {
                    LayerIndex  = layerIdx,
                    ColorSource = Rhino.DocObjects.ObjectColorSource.ColorFromObject,
                    ObjectColor = color,
                    Name        = $"[Tanuki Marker] {view.Name}"
                };
                view.MarkerObjectId     = doc.Objects.AddLine(markerLine, lineAttr);
                view.MarkerIndicatorIds = Tanuki.Generators.MarkerDrawer.DrawIndicators(
                    doc, markerLine, view.Name, view.ViewRight, layerIdx, color);

                project.Save(doc);
                doc.Views.Redraw();
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
                if (idx >= project.Views.Count) return;
                DrawingPlacer.DeleteViewLayers(doc, project.Views[idx].GetLayerKey());
                project.Views.RemoveAt(idx);
                project.Save(doc);
                doc.Views.Redraw();
                Refresh();
            });
        }

        private void OnRename()
        {
            Application.Instance.Invoke(() =>
            {
                int idx = _grid.SelectedRow;
                var doc = RhinoDoc.ActiveDoc;
                if (doc == null || idx < 0 || string.IsNullOrWhiteSpace(_tbRename.Text)) return;
                var project = TanukiProject.Load(doc);
                if (idx >= project.Views.Count) return;

                string newName = _tbRename.Text.Trim().Replace("::", "_");
                if (project.Views[idx].Name == newName) return;
                project.Views[idx].Name = newName;
                project.Save(doc);
                Refresh();
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
                var rows = new List<ViewRow>();
                if (project != null)
                {
                    foreach (var v in project.Views)
                    {
                        string icon = v.Type == ViewType.FloorPlan ? "🗺" :
                                      v.Type == ViewType.RCP       ? "💡" :
                                      v.Type == ViewType.Section   ? "✂"  : "🏢";
                        string info = (v.Type == ViewType.FloorPlan || v.Type == ViewType.RCP)
                            ? $"Z={v.CutHeight:F0}"
                            : $"({v.CutStartX:F0},{v.CutStartY:F0})";
                        rows.Add(new ViewRow { Icon = icon, Name = v.Name, Info = info });
                    }
                    _lblCount.Text = $"{project.Views.Count} 図面";
                }
                else
                {
                    _lblCount.Text = "0 図面";
                }
                _grid.DataStore = rows;
            });
        }

        // 選択中の図面だけメッシュ無視/奥行きを上書きして再生成
        private void OnApplyViewPerf()
        {
            Application.Instance.Invoke(() =>
            {
                int idx = _grid.SelectedRow;
                var doc = RhinoDoc.ActiveDoc;
                if (doc == null || idx < 0) return;
                var project = TanukiProject.Load(doc);
                if (idx >= project.Views.Count) return;
                var view = project.Views[idx];
                double depth;
                if (!double.TryParse(_tbViewDepth.Text, out depth) || depth < 0) depth = 0;
                double labelH;
                if (!double.TryParse(_tbLabelHeight.Text, out labelH) || labelH <= 0) labelH = 500;
                double cutH;
                if (!double.TryParse(_tbCutHeight.Text, out cutH)) cutH = view.CutHeight;
                view.IncludeMeshes   = !(_cbViewMesh.Checked ?? false);
                view.ViewDepth       = depth;
                view.LabelTextHeight = labelH;
                view.LayerMode       = _rbLineType.Checked ? LayerMode.LineType : LayerMode.OriginalLayer;
                if (view.Type == ViewType.FloorPlan || view.Type == ViewType.RCP)
                    view.CutHeight = cutH;
                project.Save(doc);
                RhinoApp.InvokeOnUiThread(new Action(() => ViewGenerator.Generate(doc, view, project)));
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
