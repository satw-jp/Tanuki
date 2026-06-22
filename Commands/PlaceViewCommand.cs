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
    /// 既存の図面を任意の位置に再配置する
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
            foreach (var v in project.Views) go.AddOption(v.Name.Replace("-", "_").Replace(" ", "_"));
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

            // モデルBBoxの左下を基準にオフセット計算
            var bbox = DrawingPlacer.GetModelBBox(doc);
            view.PlacedOffsetX = pt.X - bbox.Min.X;
            view.PlacedOffsetY = pt.Y - bbox.Min.Y;
            project.Save(doc);

            ViewGenerator.Generate(doc, view, project, replaceExisting: true);
            return Result.Success;
        }
    }
}
