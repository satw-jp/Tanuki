using System;
using Eto.Forms;
using Eto.Drawing;
using Rhino;
using Rhino.UI;
using Tanuki.Data;
using Tanuki.Generators;

namespace Tanuki.UI
{
    [System.Runtime.InteropServices.Guid("B2C3D4E5-F6A7-8901-BCDE-F12345678901")]
    public class TanukiPanel : Panel
    {
        public static Guid PanelId => typeof(TanukiPanel).GUID;

        private readonly ListBox      _viewList;
        private readonly RadioButton  _rbLineType;
        private readonly RadioButton  _rbOriginal;
        private TextBox               _tbRename;
        private TextBox               _tbLabelHeight;

        public TanukiPanel(uint documentSerialNumber)
        {
            var layout = new DynamicLayout { Padding = new Padding(4), Spacing = new Size(0, 2) };

            // ── 横一列ツールバー ──────────────────────────────────────────────
            var toolbar = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 1 };

            // グループ1: パネル
            toolbar.Items.Add(IBtn("⊕",  "通り芯パネル",      () => OpenPanel(TanukiGridPanel.PanelId)));
            toolbar.Items.Add(IBtn("⊟",  "レベルパネル",      () => OpenPanel(TanukiLevelPanel.PanelId)));
            toolbar.Items.Add(IBtn("📑",  "断面/図面パネル",   () => OpenPanel(TanukiSectionPanel.PanelId)));
            toolbar.Items.Add(VSep());

            // グループ2: 図面生成
            toolbar.Items.Add(IBtn("🗺",  "平面図",           () => Run("TanukiFloorPlan")));
            toolbar.Items.Add(IBtn("💡",  "天井伏図 (RCP)",   () => Run("TanukiRCP")));
            toolbar.Items.Add(IBtn("✂",   "断面図",           () => Run("TanukiSection")));
            toolbar.Items.Add(IBtn("🏢",  "立面図",           () => Run("TanukiElevation")));
            toolbar.Items.Add(IBtn("🔄",  "全図面を更新",          () => Run("TanukiUpdateAll")));
            toolbar.Items.Add(IBtn("⬛",  "通り芯基準で自動配置",  () => Run("TanukiAutoLayout")));
            toolbar.Items.Add(VSep());

            // グループ3: 図面管理
            toolbar.Items.Add(IBtn("📌",  "配置位置を変更",   () => Run("TanukiPlaceView")));
            toolbar.Items.Add(IBtn("ℹ",   "プロパティ",       () => Run("TanukiProperties")));
            toolbar.Items.Add(IBtn("📤",  "DXFエクスポート",  () => Run("TanukiExport")));
            toolbar.Items.Add(VSep());

            // グループ4: シート・出力
            toolbar.Items.Add(IBtn("📋",  "新規シート",         () => Run("TanukiSheet")));
            toolbar.Items.Add(IBtn("🖨",  "印刷範囲",           () => Run("TanukiPrint")));
            toolbar.Items.Add(IBtn("🏷",  "タイトルブロック",   () => Run("TanukiTitleBlock")));
            toolbar.Items.Add(IBtn("📄",  "PDFエクスポート",    () => Run("TanukiPDF")));

            layout.AddRow(toolbar);
            layout.AddRow(new Panel { Height = 1, BackgroundColor = Colors.DarkGray });

            // ── ⑥ レイヤー表示トグル ──────────────────────────────────────────
            var toggleRow = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 2 };
            toggleRow.Items.Add(new Label { Text = "表示:", VerticalAlignment = VerticalAlignment.Center });
            toggleRow.Items.Add(IBtn("🔴", "断面線の表示/非表示",    () => ToggleLayerSuffix("断面線")));
            toggleRow.Items.Add(IBtn("⚫", "見え掛かりの表示/非表示", () => ToggleLayerSuffix("見え掛かり")));
            toggleRow.Items.Add(IBtn("⚪", "隠れ線の表示/非表示",    () => ToggleLayerSuffix("隠れ線")));
            layout.AddRow(toggleRow);
            layout.AddRow(new Panel { Height = 1, BackgroundColor = Colors.DarkGray });

            // ── ② 図面リスト（選択でズーム） ─────────────────────────────────
            _viewList = new ListBox { Height = 100 };
            _viewList.SelectedIndexChanged += (s, e) => OnListSelectionChanged();
            layout.AddRow(_viewList);

            var listBar = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 2 };
            listBar.Items.Add(IBtn("🔃", "選択を再生成", RefreshSelected));
            listBar.Items.Add(IBtn("🗑",  "選択を削除",   DeleteSelected));
            layout.AddRow(listBar);

            // ── ⑤ リネーム行 ─────────────────────────────────────────────────
            var renameRow = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 2 };
            _tbRename = new TextBox { PlaceholderText = "新しい名前", Width = 90 };
            renameRow.Items.Add(_tbRename);
            renameRow.Items.Add(IBtn("✎", "リネーム", RenameSelected));
            layout.AddRow(renameRow);

            layout.AddRow(new Panel { Height = 1, BackgroundColor = Colors.DarkGray });

            // ── レイヤーモード ────────────────────────────────────────────────
            _rbLineType = new RadioButton { Text = "線種", Checked = true };
            _rbOriginal = new RadioButton(_rbLineType) { Text = "元レイヤー" };
            _rbLineType.CheckedChanged += (s, e) => SaveLayerMode();
            var modeRow = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Items = { _rbLineType, _rbOriginal }
            };
            layout.AddRow(modeRow);

            // ── 図面名文字高さ ────────────────────────────────────────────────
            var labelRow = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 4 };
            labelRow.Items.Add(new Label { Text = "図面名高さ:", VerticalAlignment = VerticalAlignment.Center });
            _tbLabelHeight = new TextBox { Text = "500", Width = 60 };
            labelRow.Items.Add(_tbLabelHeight);
            labelRow.Items.Add(new Label { Text = "mm", VerticalAlignment = VerticalAlignment.Center });
            labelRow.Items.Add(IBtn("適用", "図面タイトル文字高さを変更", OnApplyLabelHeight));
            layout.AddRow(labelRow);
            layout.Add(null);

            Content = layout;
            RefreshViewList();
            RhinoDoc.ActiveDocumentChanged += (s, e) => { try { RefreshViewList(); } catch { } };
            TanukiPlugin.ViewsChanged      += (s, e) => { try { RefreshViewList(); } catch { } };
        }

        // ── UI helpers ───────────────────────────────────────────────────────

        private Button IBtn(string icon, string tip, Action action)
        {
            var b = new Button { Text = icon, Width = 28, Height = 28, ToolTip = tip };
            b.Click += (s, e) => action();
            return b;
        }

        private Panel VSep() => new Panel
        {
            Width           = 1,
            Height          = 24,
            BackgroundColor = Colors.DarkGray
        };

        // ── Actions ──────────────────────────────────────────────────────────

        private void Run(string cmd)
        {
            RhinoApp.InvokeOnUiThread(new Action(() => RhinoApp.RunScript(cmd, false)));
        }

        private void OpenPanel(Guid id)
        {
            RhinoApp.InvokeOnUiThread(new Action(() => Rhino.UI.Panels.OpenPanel(id)));
        }

        // ② リスト選択でズーム
        private void OnListSelectionChanged()
        {
            int idx = _viewList.SelectedIndex;
            if (idx < 0) return;

            var doc = RhinoDoc.ActiveDoc;
            if (doc == null) return;
            var project = TanukiProject.Load(doc);
            if (idx < project.Views.Count)
                _tbRename.Text = project.Views[idx].Name;

            RhinoApp.InvokeOnUiThread(new Action(() =>
            {
                if (idx < 0) return;
                var d = RhinoDoc.ActiveDoc;
                if (d == null) return;
                var p = TanukiProject.Load(d);
                if (idx >= p.Views.Count) return;
                ZoomToViewLayer(d, p.Views[idx].GetLayerKey());
            }));
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

        private void RefreshSelected()
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null || _viewList.SelectedIndex < 0) return;
            var project = TanukiProject.Load(doc);
            if (_viewList.SelectedIndex >= project.Views.Count) return;
            var view = project.Views[_viewList.SelectedIndex];
            RhinoApp.InvokeOnUiThread(new Action(() => ViewGenerator.Generate(doc, view, project)));
        }

        private void DeleteSelected()
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null || _viewList.SelectedIndex < 0) return;
            var project = TanukiProject.Load(doc);
            if (_viewList.SelectedIndex >= project.Views.Count) return;
            var view = project.Views[_viewList.SelectedIndex];
            DrawingPlacer.DeleteViewLayers(doc, view.GetLayerKey());
            project.Views.RemoveAt(_viewList.SelectedIndex);
            project.Save(doc);
            doc.Views.Redraw();
            RefreshViewList();
        }

        // ⑤ リネーム（LayerKey は不変、表示名 Name だけ変更）
        private void RenameSelected()
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null || _viewList.SelectedIndex < 0) return;
            if (string.IsNullOrWhiteSpace(_tbRename.Text)) return;
            var project = TanukiProject.Load(doc);
            int idx = _viewList.SelectedIndex;
            if (idx >= project.Views.Count) return;

            string newName = _tbRename.Text.Trim().Replace("::", "_");
            if (project.Views[idx].Name == newName) return;
            project.Views[idx].Name = newName;
            project.Save(doc);
            RefreshViewList();
        }

        // ⑥ レイヤー表示トグル
        private void ToggleLayerSuffix(string suffix)
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null) return;
            bool anyVisible = false;
            var targets = new System.Collections.Generic.List<Rhino.DocObjects.Layer>();
            foreach (var layer in doc.Layers)
            {
                if (!layer.IsValid || layer.IsDeleted) continue;
                if (layer.FullPath.StartsWith("Tanuki::") && layer.Name == suffix)
                {
                    targets.Add(layer);
                    if (layer.IsVisible) anyVisible = true;
                }
            }
            bool newState = !anyVisible;
            foreach (var layer in targets)
            {
                var copy = layer;
                copy.IsVisible = newState;
                doc.Layers.Modify(copy, layer.Index, false);
            }
            doc.Views.Redraw();
        }

        private void RefreshViewList()
        {
            if (IsDisposed) return;
            Application.Instance.Invoke(() =>
            {
                if (IsDisposed) return;
                _viewList.Items.Clear();
                var doc = RhinoDoc.ActiveDoc;
                if (doc == null) return;
                foreach (var v in TanukiProject.Load(doc).Views)
                {
                    string icon = v.Type == ViewType.FloorPlan ? "🗺" :
                                  v.Type == ViewType.RCP       ? "💡" :
                                  v.Type == ViewType.Section   ? "✂"  : "🏢";
                    _viewList.Items.Add($"{icon} {v.Name}");
                }
            });
        }

        private void SaveLayerMode()
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null) return;
            var project = TanukiProject.Load(doc);
            project.LayerMode = _rbLineType.Checked ? LayerMode.LineType : LayerMode.OriginalLayer;
            project.Save(doc);
        }

        private void OnApplyLabelHeight()
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null) return;
            if (!double.TryParse(_tbLabelHeight.Text, out double h) || h <= 0) return;
            var project = TanukiProject.Load(doc);
            project.LabelTextHeight = h;
            project.Save(doc);
        }
    }
}
