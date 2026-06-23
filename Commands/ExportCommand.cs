using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.Commands;
using Rhino.Input.Custom;
using Tanuki.Data;

namespace Tanuki.Commands
{
    public class TanukiExport : Command
    {
        public static TanukiExport Instance { get; private set; }
        public override string EnglishName => "TanukiExport";
        public TanukiExport() { Instance = this; }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var project = TanukiProject.Load(doc);
            if (project.Views.Count == 0)
            {
                RhinoApp.WriteLine("[Tanuki] 図面がありません。先に図面を生成してください。");
                return Result.Nothing;
            }

            // エクスポートする図面を選択
            var go = new GetOption();
            go.SetCommandPrompt("エクスポートする図面を選択");
            go.AddOption("全図面");
            foreach (var v in project.Views)
                go.AddOption(v.Name.Replace("::", "_").Replace("-", "_").Replace(" ", "_").Replace(".", "_"));
            go.Get();
            if (go.CommandResult() != Result.Success) return go.CommandResult();

            var viewsToExport = new List<ViewDef>();
            if (go.Option().Index == 1)
                viewsToExport.AddRange(project.Views);
            else
                viewsToExport.Add(project.Views[go.Option().Index - 2]);

            // 該当レイヤーのオブジェクトを選択
            doc.Objects.UnselectAll();
            int selectedCount = 0;

            foreach (var view in viewsToExport)
            {
                string layerPath = $"Tanuki::{view.GetLayerKey()}";
                int li = doc.Layers.FindByFullPath(layerPath, RhinoMath.UnsetIntIndex);
                if (li == RhinoMath.UnsetIntIndex) continue;

                var objs = doc.Objects.FindByLayer(doc.Layers[li]);
                if (objs != null)
                    foreach (var o in objs) { o.Select(true); selectedCount++; }

                var children = doc.Layers[li].GetChildren();
                if (children != null)
                    foreach (var child in children)
                    {
                        var co = doc.Objects.FindByLayer(child);
                        if (co != null)
                            foreach (var o in co) { o.Select(true); selectedCount++; }
                    }
            }

            if (selectedCount == 0)
            {
                RhinoApp.WriteLine("[Tanuki] エクスポートするオブジェクトがありません。先に図面を生成してください。");
                return Result.Nothing;
            }

            RhinoApp.WriteLine($"[Tanuki] {selectedCount} オブジェクトを選択。エクスポートダイアログを開きます...");
            RhinoApp.RunScript("_Export", false);
            return Result.Success;
        }
    }
}
