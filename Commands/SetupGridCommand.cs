using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
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
            int drawIdx  = go.AddOption("描く");
            int pickIdx  = go.AddOption("既存線を選択");
            int listIdx  = go.AddOption("一覧");
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

            if (go.Option().Index == pickIdx)
                return AddFromExistingLine(doc, project);

            // 描く（2点入力）
            return AddByDrawing(doc, project);
        }

        // ── 既存の線から登録 ─────────────────────────────────────────────────

        private Result AddFromExistingLine(RhinoDoc doc, TanukiProject project)
        {
            var gobj = new GetObject();
            gobj.SetCommandPrompt("通り芯にする線を選択");
            gobj.GeometryFilter = ObjectType.Curve;
            gobj.Get();
            if (gobj.CommandResult() != Result.Success) return gobj.CommandResult();

            var curve = gobj.Object(0).Curve();
            if (curve == null) { RhinoApp.WriteLine("線を選択してください"); return Result.Failure; }

            var start = curve.PointAtStart;
            var end   = curve.PointAtEnd;
            var dir   = end - start;
            dir.Unitize();

            var gn = new GetString();
            gn.SetCommandPrompt("通り芯の名前 (例: A, 1)");
            gn.Get();
            if (gn.CommandResult() != Result.Success) return gn.CommandResult();

            project.GridLines.Add(new GridLine
            {
                Name       = gn.StringResult(),
                OriginX    = start.X,
                OriginY    = start.Y,
                DirectionX = dir.X,
                DirectionY = dir.Y,
                Length     = start.DistanceTo(end)
            });

            project.Save(doc);
            RhinoApp.WriteLine($"通り芯 '{gn.StringResult()}' を登録しました");
            return Result.Success;
        }

        // ── 2点を指定して新規作成 ────────────────────────────────────────────

        private Result AddByDrawing(RhinoDoc doc, TanukiProject project)
        {
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
            gp2.SetCommandPrompt("通り芯の終点");
            gp2.SetBasePoint(gp1.Point(), false);
            gp2.DrawLineFromPoint(gp1.Point(), true);
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
                Length     = gp1.Point().DistanceTo(gp2.Point())
            });

            project.Save(doc);
            RhinoApp.WriteLine($"通り芯 '{name}' を追加しました");
            return Result.Success;
        }
    }
}
