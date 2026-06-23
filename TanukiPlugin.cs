using Rhino;
using Rhino.PlugIns;
using Tanuki.Data;
using Tanuki.Generators;
using Tanuki.UI;

namespace Tanuki
{
    public class TanukiPlugin : PlugIn
    {
        public static TanukiPlugin Instance { get; private set; }
        public override PlugInLoadTime LoadTime => PlugInLoadTime.AtStartup;

        public static event System.EventHandler GridLinesChanged;
        public static event System.EventHandler ViewsChanged;

        internal static void RaiseGridLinesChanged()
        {
            GridLinesChanged?.Invoke(null, System.EventArgs.Empty);
        }

        internal static void RaiseViewsChanged()
        {
            ViewsChanged?.Invoke(null, System.EventArgs.Empty);
        }

        public TanukiPlugin()
        {
            Instance = this;
        }

        private void TryRegister(System.Type panelType, string name)
        {
            try
            {
                Rhino.UI.Panels.RegisterPanel(this, panelType, name, (System.Drawing.Icon)null);
            }
            catch (System.Exception ex)
            {
                RhinoApp.WriteLine($"[Tanuki] パネル登録失敗 {name}: {ex.Message}");
            }
        }

        // オブジェクトが置き換えられたとき：断面マーカーまたは通り芯を追跡
        private void OnObjectReplaced(object sender, Rhino.DocObjects.RhinoReplaceObjectEventArgs args)
        {
            try { OnObjectReplacedCore(args); } catch { }
        }

        private void OnObjectReplacedCore(Rhino.DocObjects.RhinoReplaceObjectEventArgs args)
        {
            var doc = args.Document;
            if (doc == null) return;
            var project = TanukiProject.Load(doc);
            var oldId   = args.OldRhinoObject.Id;
            var newId   = args.NewRhinoObject.Id;

            // ── 断面/立面マーカーの移動追従 ──
            foreach (var view in project.Views)
            {
                if (view.MarkerObjectId == System.Guid.Empty) continue;
                if (view.MarkerObjectId != oldId) continue;

                if (args.NewRhinoObject.Geometry is Rhino.Geometry.Curve curve)
                {
                    var oldIndicatorIds = new System.Collections.Generic.List<System.Guid>(
                        view.MarkerIndicatorIds ?? new System.Collections.Generic.List<System.Guid>());
                    view.CutStartX      = curve.PointAtStart.X;
                    view.CutStartY      = curve.PointAtStart.Y;
                    view.CutEndX        = curve.PointAtEnd.X;
                    view.CutEndY        = curve.PointAtEnd.Y;
                    view.MarkerObjectId = newId;
                    var newLine    = new Rhino.Geometry.Line(curve.PointAtStart, curve.PointAtEnd);
                    bool viewRight = view.ViewRight;
                    string vName   = view.Name;
                    var v = view;
                    var p = project;
                    RhinoApp.InvokeOnUiThread(new System.Action(() =>
                    {
                        MarkerDrawer.DeleteIndicators(doc, oldIndicatorIds);
                        int layerIdx = MarkerDrawer.EnsureMarkersLayer(doc);
                        v.MarkerIndicatorIds = MarkerDrawer.DrawIndicators(doc, newLine, vName, viewRight, layerIdx, System.Drawing.Color.Magenta);
                        p.Save(doc);
                        ViewGenerator.Generate(doc, v, p);
                        RaiseViewsChanged();
                    }));
                }
                return;
            }

            // ── 通り芯の移動追従 ──
            if (args.NewRhinoObject.Geometry is Rhino.Geometry.Curve gridCurve)
            {
                bool updated = Tanuki.Generators.GridLineDrawer.TryUpdateFromObject(
                    doc, oldId, newId, gridCurve, project.GridLines);

                if (updated)
                {
                    project.Save(doc);
                    var snapProject = project;
                    RhinoApp.InvokeOnUiThread(new System.Action(() =>
                    {
                        Tanuki.Generators.GridLineDrawer.SyncSymbols(doc, snapProject.GridLines, snapProject.BubbleRadius);
                        foreach (var v in snapProject.Views)
                        {
                            if (v.Type == Data.ViewType.FloorPlan || v.Type == Data.ViewType.RCP)
                                Generators.ViewGenerator.Generate(doc, v, snapProject);
                        }
                        RaiseGridLinesChanged();
                    }));
                }
            }
        }

        protected override LoadReturnCode OnLoad(ref string errorMessage)
        {
            TryRegister(typeof(TanukiPanel),        "Tanuki");
            TryRegister(typeof(TanukiGridPanel),    "T:Grid");
            TryRegister(typeof(TanukiLevelPanel),   "T:Level");
            TryRegister(typeof(TanukiSectionPanel), "T:Views");
            RhinoDoc.ReplaceRhinoObject += OnObjectReplaced;
            RhinoApp.Initialized += OnRhinoInitialized;
            return LoadReturnCode.Success;
        }

        private void OnRhinoInitialized(object sender, System.EventArgs e)
        {
            RhinoApp.Initialized -= OnRhinoInitialized;
            Rhino.UI.Panels.OpenPanel(TanukiPanel.PanelId);
        }
    }
}
