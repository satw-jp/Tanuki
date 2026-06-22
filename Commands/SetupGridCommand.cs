using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input.Custom;
using Tanuki.Data;

namespace Tanuki.Commands
{
    public class TanukiSetupGrid : Command
    {
        public static TanukiSetupGrid Instance { get; private set; }
        public override string EnglishName => "TanukiSetupGrid";
        public TanukiSetupGrid() { Instance = this; }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var project = TanukiProject.Load(doc);

            var go = new GetOption();
            go.SetCommandPrompt("通り芯の操作を選択");
            int addIdx = go.AddOption("追加");
            int listIdx = go.AddOption("一覧");
            int clearIdx = go.AddOption("全削除");
            go.Get();
            if (go.CommandResult() != Result.Success) return go.CommandResult();

            if (go.Option().Index == listIdx)
            {
                if (project.GridLines.Count == 0) { RhinoApp.WriteLine("通り芯なし"); return Result.Success; }
                foreach (var g in project.GridLines)
                    RhinoApp.WriteLine($"  {g.Name}  origin=({g.OriginX:F0},{g.OriginY:F0})");
                return Result.Success;
            }

            if (go.Option().Index == clearIdx)
            {
                project.GridLines.Clear();
                project.Save(doc);
                RhinoApp.WriteLine("通り芯をすべて削除しました");
                return Result.Success;
            }

            // 追加
            var gn = new GetString();
            gn.SetCommandPrompt("通り芯の名前 (例: A, 1)");
            gn.Get();
            if (gn.CommandResult() != Result.Success) return gn.CommandResult();
            string name = gn.StringResult();

            var gp1 = new GetPoint();
            gp1.SetCommandPrompt("通り芯の始点");
            gp1.Get();
            if (gp1.CommandResult() != Result.Success) return gp1.CommandResult();

            var gp2 = new GetPoint();
            gp2.SetCommandPrompt("通り芯の方向（終点）");
            gp2.Get();
            if (gp2.CommandResult() != Result.Success) return gp2.CommandResult();

            var dir = gp2.Point() - gp1.Point();
            dir.Unitize();

            project.GridLines.Add(new GridLine
            {
                Name       = name,
                OriginX    = gp1.Point().X,
                OriginY    = gp1.Point().Y,
                DirectionX = dir.X,
                DirectionY = dir.Y,
                Length     = gp1.Point().DistanceTo(gp2.Point()) * 2
            });

            project.Save(doc);
            RhinoApp.WriteLine($"通り芯 '{name}' を追加しました");
            return Result.Success;
        }
    }
}
