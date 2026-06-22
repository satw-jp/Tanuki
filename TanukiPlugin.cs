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

        // マーカー線が移動されたとき対応図面を自動再生成
        private void OnObjectReplaced(object sender, Rhino.DocObjects.RhinoReplaceObjectEventArgs args)
        {
            var doc     = args.Document;
            var project = TanukiProject.Load(doc);

            foreach (var view in project.Views)
            {
                if (view.MarkerObjectId == System.Guid.Empty) continue;
                if (view.MarkerObjectId != args.OldRhinoObject.Id) continue;

                if (args.NewRhinoObject.Geometry is Rhino.Geometry.Curve curve)
                {
                    view.CutStartX = curve.PointAtStart.X;
                    view.CutStartY = curve.PointAtStart.Y;
                    view.CutEndX   = curve.PointAtEnd.X;
                    view.CutEndY   = curve.PointAtEnd.Y;
                    project.Save(doc);
                    RhinoApp.InvokeOnUiThread(new System.Action(() =>
                        ViewGenerator.Generate(doc, view, project)));
                }
                break;
            }
        }

        protected override LoadReturnCode OnLoad(ref string errorMessage) => LoadReturnCode.Success;
    }
}
