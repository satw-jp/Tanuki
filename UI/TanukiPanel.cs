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

        private readonly RadioButton _rbLineType;
        private readonly RadioButton _rbOriginal;
        private TextBox              _tbLabelHeight;
        private CheckBox             _cbIgnoreMesh;
        private TextBox              _tbViewDepth;

        public TanukiPanel(uint documentSerialNumber)
        {
            var layout = new DynamicLayout { Padding = new Padding(4), Spacing = new Size(0, 2) };

            // ── コマンドランチャーツールバー ──────────────────────────────────
            var toolbar = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 1 };

            toolbar.Items.Add(IBtn("⊕",  "通り芯パネル",      () => OpenPanel(TanukiGridPanel.PanelId)));
            toolbar.Items.Add(IBtn("⊟",  "レベルパネル",      () => OpenPanel(TanukiLevelPanel.PanelId)));
            toolbar.Items.Add(IBtn("📑",  "断面/図面パネル",   () => OpenPanel(TanukiSectionPanel.PanelId)));
            toolbar.Items.Add(VSep());

            toolbar.Items.Add(IBtn("🗺",  "平面図",           () => Run("TanukiFloorPlan")));
            toolbar.Items.Add(IBtn("💡",  "天井伏図 (RCP)",   () => Run("TanukiRCP")));
            toolbar.Items.Add(IBtn("✂",   "断面図",           () => Run("TanukiSection")));
            toolbar.Items.Add(IBtn("🏢",  "立面図",           () => Run("TanukiElevation")));
            toolbar.Items.Add(IBtn("🔄",  "全図面を更新",          () => Run("TanukiUpdateAll")));
            toolbar.Items.Add(IBtn("⬛",  "通り芯基準で自動配置",  () => Run("TanukiAutoLayout")));
            toolbar.Items.Add(VSep());

            toolbar.Items.Add(IBtn("📌",  "配置位置を変更",   () => Run("TanukiPlaceView")));
            toolbar.Items.Add(IBtn("ℹ",   "プロパティ",       () => Run("TanukiProperties")));
            toolbar.Items.Add(IBtn("📤",  "DXFエクスポート",  () => Run("TanukiExport")));
            toolbar.Items.Add(VSep());

            toolbar.Items.Add(IBtn("📋",  "新規シート",         () => Run("TanukiSheet")));
            toolbar.Items.Add(IBtn("🖨",  "印刷範囲",           () => Run("TanukiPrint")));
            toolbar.Items.Add(IBtn("🏷",  "タイトルブロック",   () => Run("TanukiTitleBlock")));
            toolbar.Items.Add(IBtn("📄",  "PDFエクスポート",    () => Run("TanukiPDF")));

            layout.AddRow(toolbar);
            layout.AddRow(new Panel { Height = 1, BackgroundColor = Colors.DarkGray });

            // ── レイヤー表示トグル ────────────────────────────────────────────
            var toggleRow = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 2 };
            toggleRow.Items.Add(new Label { Text = "表示:", VerticalAlignment = VerticalAlignment.Center });
            toggleRow.Items.Add(IBtn("🔴", "断面線の表示/非表示",    () => ToggleLayerSuffix("断面線")));
            toggleRow.Items.Add(IBtn("⚫", "見え掛かりの表示/非表示", () => ToggleLayerSuffix("見え掛かり")));
            toggleRow.Items.Add(IBtn("⚪", "隠れ線の表示/非表示",    () => ToggleLayerSuffix("隠れ線")));
            layout.AddRow(toggleRow);
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

            // ── 断面パフォーマンス既定（全図面に一括適用） ──────────────────
            var perfRow = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 4 };
            _cbIgnoreMesh = new CheckBox { Text = "断面でメッシュ無視" };
            perfRow.Items.Add(_cbIgnoreMesh);
            layout.AddRow(perfRow);

            var depthRow = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 4 };
            depthRow.Items.Add(new Label { Text = "視線奥行き:", VerticalAlignment = VerticalAlignment.Center });
            _tbViewDepth = new TextBox { Text = "0", Width = 60 };
            depthRow.Items.Add(_tbViewDepth);
            depthRow.Items.Add(new Label { Text = "mm(0=無制限)", VerticalAlignment = VerticalAlignment.Center });
            depthRow.Items.Add(IBtn("適用", "メッシュ無視/奥行きを全断面に適用して再生成", OnApplyPerf));
            layout.AddRow(depthRow);

            layout.Add(null);

            Content = layout;
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

        private void OnApplyPerf()
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null) return;
            double depth;
            if (!double.TryParse(_tbViewDepth.Text, out depth) || depth < 0) depth = 0;
            bool ignoreMesh = _cbIgnoreMesh.Checked ?? false;

            var project = TanukiProject.Load(doc);
            project.DefaultIncludeMeshes = !ignoreMesh;
            project.DefaultViewDepth     = depth;
            foreach (var v in project.Views)
            {
                v.IncludeMeshes = !ignoreMesh;
                v.ViewDepth     = depth;
            }
            project.Save(doc);

            var snap = project;
            RhinoApp.InvokeOnUiThread(new Action(() =>
            {
                foreach (var v in snap.Views)
                    ViewGenerator.Generate(doc, v, snap);
                TanukiPlugin.RaiseViewsChanged();
            }));
        }
    }
}
