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

        private GridView _grid;
        private TextBox  _tbRename;
        private Label    _lblCount;

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
            tb.Items.Add(Btn("🔄", "再生成",   OnRegenerate));
            tb.Items.Add(Btn("🔍", "ズーム",   OnZoom));
            tb.Items.Add(Btn("✕",  "削除",     OnDelete));
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

            layout.Add(null);
            Content = layout;

            Refresh();
            RhinoDoc.ActiveDocumentChanged += (s, e) => { try { Refresh(); } catch { } };
            TanukiPlugin.ViewsChanged      += (s, e) => { try { Refresh(); } catch { } };
        }

        // ── Actions ──────────────────────────────────────────────

        private void OnSelect()
        {
            Application.Instance.Invoke(() =>
            {
                int idx = _grid.SelectedRow;
                var doc = RhinoDoc.ActiveDoc;
                if (doc == null || idx < 0) return;
                var project = TanukiProject.Load(doc);
                if (idx >= project.Views.Count) return;
                _tbRename.Text = project.Views[idx].Name;
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

        private Button Btn(string label, string tip, Action action)
        {
            var b = new Button { Text = label, Height = 26, ToolTip = tip };
            b.Click += (s, e) => action();
            return b;
        }
    }
}
