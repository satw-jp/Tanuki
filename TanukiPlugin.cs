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

        public TanukiPlugin()
        {
            Instance = this;
            RhinoApp.Initialized += OnRhinoInitialized;
        }

        private void OnRhinoInitialized(object sender, System.EventArgs e)
        {
            TryRegister(typeof(TanukiPanel),      "Tanuki");
            TryRegister(typeof(TanukiGridPanel),  "Tanuki: 通り芯");
            TryRegister(typeof(TanukiLevelPanel), "Tanuki: レベル");
            RhinoDoc.ReplaceRhinoObject += OnObjectReplaced;
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
            var doc     = args.Document;
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
                    view.CutStartX      = curve.PointAtStart.X;
                    view.CutStartY      = curve.PointAtStart.Y;
                    view.CutEndX        = curve.PointAtEnd.X;
                    view.CutEndY        = curve.PointAtEnd.Y;
                    view.MarkerObjectId = newId;
                    project.Save(doc);
                    var v = view;
                    RhinoApp.InvokeOnUiThread(new System.Action(() =>
                        ViewGenerator.Generate(doc, v, project)));
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
                    RhinoApp.InvokeOnUiThread(new System.Action(() =>
                        Tanuki.Generators.GridLineDrawer.SyncSymbols(doc, project.GridLines)));
                }
            }
        }

        protected override LoadReturnCode OnLoad(ref string errorMessage) => LoadReturnCode.Success;
    }
}
