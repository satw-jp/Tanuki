using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input.Custom;
using Tanuki.Data;
using Tanuki.Generators;

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
            go.AddOption("描く");
            go.AddOption("既存線");
            go.AddOption("全削除");
            go.Get();
            if (go.CommandResult() != Result.Success) return go.CommandResult();

            switch (go.Option().Index)
            {
                case 1: return AddByDrawing(doc, project);
                case 2: return AddFromLine(doc, project);
                default:
                    project.GridLines.Clear();
                    project.Save(doc);
                    GridLineDrawer.SyncAll(doc, project.GridLines);
                    return Result.Success;
            }
        }

        private Result AddByDrawing(RhinoDoc doc, TanukiProject project)
        {
            var gn = new GetString(); gn.SetCommandPrompt("名前"); gn.Get();
            if (gn.CommandResult() != Result.Success) return gn.CommandResult();

            var gp1 = new GetPoint(); gp1.SetCommandPrompt("始点"); gp1.Get();
            if (gp1.CommandResult() != Result.Success) return gp1.CommandResult();

            var gp2 = new GetPoint();
            gp2.SetCommandPrompt("終点");
            gp2.SetBasePoint(gp1.Point(), false);
            gp2.DrawLineFromPoint(gp1.Point(), true);
            gp2.Get();
            if (gp2.CommandResult() != Result.Success) return gp2.CommandResult();

            AddGrid(doc, project, gn.StringResult(), gp1.Point(), gp2.Point());
            return Result.Success;
        }

        private Result AddFromLine(RhinoDoc doc, TanukiProject project)
        {
            var gobj = new GetObject();
            gobj.SetCommandPrompt("通り芯にする線を選択");
            gobj.GeometryFilter = ObjectType.Curve;
            gobj.Get();
            if (gobj.CommandResult() != Result.Success) return gobj.CommandResult();

            var curve = gobj.Object(0).Curve();
            if (curve == null) return Result.Failure;

            var gn = new GetString(); gn.SetCommandPrompt("名前"); gn.Get();
            if (gn.CommandResult() != Result.Success) return gn.CommandResult();

            AddGrid(doc, project, gn.StringResult(), curve.PointAtStart, curve.PointAtEnd);
            return Result.Success;
        }

        private void AddGrid(RhinoDoc doc, TanukiProject project, string name, Point3d start, Point3d end)
        {
            var dir = end - start; dir.Unitize();
            project.GridLines.Add(new GridLine
            {
                Name = name, OriginX = start.X, OriginY = start.Y,
                DirectionX = dir.X, DirectionY = dir.Y, Length = start.DistanceTo(end)
            });
            project.Save(doc);
            GridLineDrawer.SyncAll(doc, project.GridLines);
        }
    }
}
