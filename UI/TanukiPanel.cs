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
            var layout = new DynamicLayout { Padding = new Padding(6), Spacing = new Size(2, 3) };

            // ── Setup ──
            layout.AddRow(SectionLabel("Setup"));
            layout.AddRow(
                Btn("Grid Lines",  () => Run("TanukiSetupGrid")),
                Btn("Levels",      () => Run("TanukiSetupLevel"))
            );

            // ── 図面生成 ──
            layout.AddRow(SectionLabel("Generate"));
            layout.AddRow(
                Btn("Floor Plan",  () => Run("TanukiFloorPlan")),
                Btn("RCP",         () => Run("TanukiRCP"))
            );
            layout.AddRow(
                Btn("Section",     () => Run("TanukiSection")),
                Btn("Elevation",   () => Run("TanukiElevation"))
            );
            layout.AddRow(Btn("↻ Update All", () => Run("TanukiUpdateAll")));

            // ── Viewリスト ──
            layout.AddRow(SectionLabel("Views"));
            _viewList = new ListBox { Height = 100 };
            layout.AddRow(_viewList);
            layout.AddRow(
                Btn("↻ Selected", RefreshSelected),
                Btn("✕ Delete",   DeleteSelected)
            );

            // ── Layer Mode ──
            layout.AddRow(SectionLabel("Layer Mode"));
            _rbLineType = new RadioButton { Text = "Line type",      Checked = true };
            _rbOriginal = new RadioButton(_rbLineType) { Text = "Original layer" };
            _rbLineType.CheckedChanged += (s, e) => SaveLayerMode();
            layout.AddRow(_rbLineType, _rbOriginal);

            layout.Add(null);
            Content = layout;

            RefreshViewList();
            RhinoDoc.ActiveDocumentChanged += (s, e) => RefreshViewList();
        }

        // ── UI helpers ──

        private Label SectionLabel(string text) => new Label
        {
            Text = text,
            Font = new Font(SystemFont.Label, 9),
            TextColor = Colors.Gray
        };

        private Button Btn(string label, Action action)
        {
            var b = new Button { Text = label, Height = 26 };
            b.Click += (s, e) => action();
            return b;
        }

        // ── Actions ──

        private void Run(string command)
        {
            RhinoApp.InvokeOnUiThread(new Action(() =>
                RhinoApp.RunScript(command, false)));
        }

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
                    _viewList.Items.Add($"{v.Type}  {v.Name}");
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
