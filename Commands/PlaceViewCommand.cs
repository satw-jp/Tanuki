using System.Linq;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input.Custom;
using Tanuki.Data;
using Tanuki.Generators;

namespace Tanuki.Commands
{
    /// <summary>
    /// 既存の図面を任意の位置に再配置する。
    /// HasPlacement が true の場合は差分移動（高速）、未配置の場合のみ再生成する。
    /// </summary>
    public class TanukiPlaceView : Command
    {
        public static TanukiPlaceView Instance { get; private set; }
        public override string EnglishName => "TanukiPlaceView";
        public TanukiPlaceView() { Instance = this; }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var project = TanukiProject.Load(doc);
            if (project.Views.Count == 0)
            {
                RhinoApp.WriteLine("図面がありません。先に図面を生成してください。");
                return Result.Nothing;
            }

            // 図面を選択
            var go = new GetOption();
            go.SetCommandPrompt("配置を変更する図面を選択");
            foreach (var v in project.Views) go.AddOption(v.Name.Replace("::", "_").Replace("-", "_").Replace(" ", "_"));
            go.Get();
            if (go.CommandResult() != Result.Success) return go.CommandResult();

            int viewIdx = go.Option().Index - 1;
            if (viewIdx < 0 || viewIdx >= project.Views.Count) return Result.Cancel;
            var view = project.Views[viewIdx];

            // 新しい配置基準点を指定
            var gp = new GetPoint();
            gp.SetCommandPrompt($"[{view.Name}] の配置基準点を指定");
            gp.Get();
            if (gp.CommandResult() != Result.Success) return gp.CommandResult();

            var pt = gp.Point();
            var bbox = DrawingPlacer.GetModelBBox(doc);

            // 新オフセット計算
            double newOffX, newOffY;
            if (view.Type == ViewType.Section || view.Type == ViewType.Elevation)
            {
                newOffX = pt.X;
                newOffY = 0;
            }
            else
            {
                newOffX = bbox.IsValid ? pt.X - bbox.Min.X : pt.X;
                newOffY = bbox.IsValid ? pt.Y - bbox.Min.Y : pt.Y;
            }

            if (view.HasPlacement)
            {
                // 差分移動: 全オブジェクトを平行移動するだけなので瞬時に完了
                double dx = newOffX - view.PlacedOffsetX;
                double dy = newOffY - view.PlacedOffsetY;
                var delta = Transform.Translation(dx, dy, 0);
                MoveLayerRecursive(doc, $"Tanuki::{view.GetLayerKey()}", delta);
                view.PlacedOffsetX = newOffX;
                view.PlacedOffsetY = newOffY;
                project.Save(doc);
                doc.Views.Redraw();
            }
            else
            {
                // 未配置の場合のみフル再生成（初回のみ）
                view.PlacedOffsetX = newOffX;
                view.PlacedOffsetY = newOffY;
                view.HasPlacement  = true;
                project.Save(doc);
                ViewGenerator.Generate(doc, view, project, replaceExisting: true);
            }

            return Result.Success;
        }

        private static void MoveLayerRecursive(RhinoDoc doc, string fullPath, Transform move)
        {
            int li = doc.Layers.FindByFullPath(fullPath, RhinoMath.UnsetIntIndex);
            if (li == RhinoMath.UnsetIntIndex) return;
            var objs = doc.Objects.FindByLayer(doc.Layers[li]);
            if (objs != null)
                foreach (var o in objs)
                    doc.Objects.Transform(o.Id, move, true);
            var children = doc.Layers[li].GetChildren();
            if (children != null)
                foreach (var child in children)
                    MoveLayerRecursive(doc, child.FullPath, move);
        }
    }
}
