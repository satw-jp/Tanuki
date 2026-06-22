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

        private readonly ListBox _viewList;
        private readonly RadioButton _rbLineType;
        private readonly RadioButton _rbOriginal;

        public TanukiPanel(uint documentSerialNumber)
        {
            var layout = new DynamicLayout { Padding = new Padding(4), Spacing = new Size(0, 2) };

            // ── Setup toolbar ──────────────────────────────────────
            var setupRow = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 2
            };
            setupRow.Items.Add(IconBtn("⊕",  "通り芯を設定",   () => Run("TanukiSetupGrid")));
            setupRow.Items.Add(IconBtn("⊟",  "レベルを設定",   () => Run("TanukiSetupLevel")));
            layout.AddRow(setupRow);

            // ── Generate toolbar ───────────────────────────────────
            var genRow = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 2
            };
            genRow.Items.Add(IconBtn("🗺",  "平面図を生成",           () => Run("TanukiFloorPlan")));
            genRow.Items.Add(IconBtn("💡",  "天井伏図を生成 (RCP)",   () => Run("TanukiRCP")));
            genRow.Items.Add(IconBtn("✂",   "断面図を生成",           () => Run("TanukiSection")));
            genRow.Items.Add(IconBtn("🏢",  "立面図を生成",           () => Run("TanukiElevation")));
            genRow.Items.Add(IconBtn("🔄",  "全図面を更新",           () => Run("TanukiUpdateAll")));
            layout.AddRow(genRow);

            // ── View / Sheet toolbar ───────────────────────────────
            var viewRow = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 2
            };
            viewRow.Items.Add(IconBtn("📌",  "図面の配置位置を変更",   () => Run("TanukiPlaceView")));
            viewRow.Items.Add(IconBtn("ℹ",   "Tanukiプロパティを表示", () => Run("TanukiProperties")));
            viewRow.Items.Add(IconBtn("📋",  "新しいシート（Layout）を作成", () => Run("TanukiSheet")));
            viewRow.Items.Add(IconBtn("🖨",  "印刷範囲を可視化",       () => Run("TanukiPrint")));
            layout.AddRow(viewRow);

            // ── 区切り ─────────────────────────────────────────────
            layout.AddRow(new Panel { Height = 1, BackgroundColor = Colors.DarkGray });

            // ── 図面リスト ─────────────────────────────────────────
            _viewList = new ListBox { Height = 120 };
            layout.AddRow(_viewList);

            var listRow = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 2
            };
            listRow.Items.Add(IconBtn("🔃",  "選択した図面を再生成",   RefreshSelected));
            listRow.Items.Add(IconBtn("🗑",   "選択した図面を削除",     DeleteSelected));
            layout.AddRow(listRow);

            // ── 区切り ─────────────────────────────────────────────
            layout.AddRow(new Panel { Height = 1, BackgroundColor = Colors.DarkGray });

            // ── レイヤーモード ─────────────────────────────────────
            _rbLineType = new RadioButton { Text = "≡ 線種", Checked = true };
            _rbOriginal = new RadioButton(_rbLineType) { Text = "≡ 元レイヤー" };
            _rbLineType.CheckedChanged += (s, e) => SaveLayerMode();
            var modeRow = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Items = { _rbLineType, _rbOriginal }
            };
            layout.AddRow(modeRow);

            layout.Add(null);
            Content = layout;

            RefreshViewList();
            RhinoDoc.ActiveDocumentChanged += (s, e) => RefreshViewList();
        }

        // ── UI helpers ──

        private Button IconBtn(string icon, string tip, Action action)
        {
            var b = new Button
            {
                Text    = icon,
                Width   = 30,
                Height  = 30,
                ToolTip = tip
            };
            b.Click += (s, e) => action();
            return b;
        }

        private void Run(string command)
        {
            RhinoApp.InvokeOnUiThread(new Action(() =>
                RhinoApp.RunScript(command, false)));
        }

        // ── Actions ──

        private void RefreshSelected()
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null || _viewList.SelectedIndex < 0) return;
            var project = TanukiProject.Load(doc);
            if (_viewList.SelectedIndex >= project.Views.Count) return;
            var view = project.Views[_viewList.SelectedIndex];
            RhinoApp.InvokeOnUiThread(new Action(() =>
                ViewGenerator.Generate(doc, view, project)));
        }

        private void DeleteSelected()
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null || _viewList.SelectedIndex < 0) return;
            var project = TanukiProject.Load(doc);
            if (_viewList.SelectedIndex >= project.Views.Count) return;
            var view = project.Views[_viewList.SelectedIndex];
            DrawingPlacer.DeleteViewLayers(doc, view.Name);
            project.Views.RemoveAt(_viewList.SelectedIndex);
            project.Save(doc);
            doc.Views.Redraw();
            RefreshViewList();
        }

        private void RefreshViewList()
        {
            Application.Instance.Invoke(() =>
            {
                _viewList.Items.Clear();
                var doc = RhinoDoc.ActiveDoc;
                if (doc == null) return;
                var project = TanukiProject.Load(doc);
                foreach (var v in project.Views)
                {
                    string icon = v.Type == ViewType.FloorPlan ? "🗺" :
                                  v.Type == ViewType.RCP       ? "💡" :
                                  v.Type == ViewType.Section   ? "✂"  : "🏢";
                    _viewList.Items.Add($"{icon}  {v.Name}");
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
    }
}
