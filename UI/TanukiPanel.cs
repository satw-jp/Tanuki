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

        public TanukiPanel(uint documentSerialNumber)
        {
            var layout = new DynamicLayout { Padding = new Padding(4), Spacing = new Size(0, 2) };

            // ── 横一列ツールバー ──────────────────────────────────────────────
            var toolbar = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 1 };

            // グループ1: パネル
            toolbar.Items.Add(IBtn("⊕",  "通り芯パネル",     () => OpenPanel(TanukiGridPanel.PanelId)));
            toolbar.Items.Add(IBtn("⊟",  "レベルパネル",     () => OpenPanel(TanukiLevelPanel.PanelId)));
            toolbar.Items.Add(VSep());

            // グループ2: 図面生成
            toolbar.Items.Add(IBtn("🗺",  "平面図",          () => Run("TanukiFloorPlan")));
            toolbar.Items.Add(IBtn("💡",  "天井伏図 (RCP)",  () => Run("TanukiRCP")));
            toolbar.Items.Add(IBtn("✂",   "断面図",          () => Run("TanukiSection")));
            toolbar.Items.Add(IBtn("🏢",  "立面図",          () => Run("TanukiElevation")));
            toolbar.Items.Add(IBtn("🔄",  "全図面を更新",    () => Run("TanukiUpdateAll")));
            toolbar.Items.Add(VSep());

            // グループ3: 図面管理
            toolbar.Items.Add(IBtn("📌",  "配置位置を変更",  () => Run("TanukiPlaceView")));
            toolbar.Items.Add(IBtn("ℹ",   "プロパティ",      () => Run("TanukiProperties")));
            toolbar.Items.Add(VSep());

            // グループ4: シート
            toolbar.Items.Add(IBtn("📋",  "新規シート",      () => Run("TanukiSheet")));
            toolbar.Items.Add(IBtn("🖨",  "印刷範囲",        () => Run("TanukiPrint")));

            layout.AddRow(toolbar);
            layout.AddRow(new Panel { Height = 1, BackgroundColor = Colors.DarkGray });

            // ── 図面リスト ────────────────────────────────────────────────────
            _viewList = new ListBox { Height = 100 };
            layout.AddRow(_viewList);

            var listBar = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 2 };
            listBar.Items.Add(IBtn("🔃", "選択を再生成", RefreshSelected));
            listBar.Items.Add(IBtn("🗑",  "選択を削除",   DeleteSelected));
            layout.AddRow(listBar);

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
            layout.Add(null);

            Content = layout;
            RefreshViewList();
            RhinoDoc.ActiveDocumentChanged += (s, e) => RefreshViewList();
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
    }
}
