using System;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input.Custom;
using Tanuki.Data;
using Tanuki.Generators;

namespace Tanuki.Commands
{
    public class TanukiFloorPlan : Command
    {
        public static TanukiFloorPlan Instance { get; private set; }
        public override string EnglishName => "TanukiFloorPlan";
        public TanukiFloorPlan() { Instance = this; }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var project = TanukiProject.Load(doc);

            // レベル選択 or 直接高さ入力
            double cutHeight = 1000;
            string viewName  = "FloorPlan";

            if (project.Levels.Count > 0)
            {
                var go = new GetOption();
                go.SetCommandPrompt("カット高さを選択");
                foreach (var l in project.Levels) go.AddOption(l.Name.Replace(" ", "_"));
                go.AddOption("直接入力");
                go.Get();
                if (go.CommandResult() != Result.Success) return go.CommandResult();

                int idx = go.Option().Index - 1;
                if (idx < project.Levels.Count)
                {
                    cutHeight = project.Levels[idx].Elevation + 1000; // FL+1000mm
                    viewName  = $"FloorPlan_{project.Levels[idx].Name}";
                }
            }

            if (viewName == "FloorPlan")
            {
                var gh = new GetNumber();
                gh.SetCommandPrompt("カット高さ (mm)");
                gh.SetDefaultNumber(1000);
                gh.Get();
                if (gh.CommandResult() != Result.Success) return gh.CommandResult();
                cutHeight = gh.Number();
                viewName  = $"FloorPlan_{(int)cutHeight}";
            }

            var view = new ViewDef { Name = viewName, LayerKey = viewName, Type = ViewType.FloorPlan, CutHeight = cutHeight,
                                     IncludeMeshes = project.DefaultIncludeMeshes, ViewDepth = project.DefaultViewDepth };
            ViewPlacement.Pick(doc, view);
            project.Views.RemoveAll(v => v.Name == viewName);
            project.Views.Add(view);
            project.Save(doc);

            ViewGenerator.Generate(doc, view, project);
            TanukiPlugin.RaiseViewsChanged();
            return Result.Success;
        }
    }

    public class TanukiRCP : Command
    {
        public static TanukiRCP Instance { get; private set; }
        public override string EnglishName => "TanukiRCP";
        public TanukiRCP() { Instance = this; }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var project = TanukiProject.Load(doc);
            var gh = new GetNumber();
            gh.SetCommandPrompt("天井高さ (mm)");
            gh.SetDefaultNumber(2400);
            gh.Get();
            if (gh.CommandResult() != Result.Success) return gh.CommandResult();

            string name = $"RCP_{(int)gh.Number()}";
            var view = new ViewDef { Name = name, LayerKey = name, Type = ViewType.RCP, CutHeight = gh.Number(),
                                     IncludeMeshes = project.DefaultIncludeMeshes, ViewDepth = project.DefaultViewDepth };
            ViewPlacement.Pick(doc, view);
            project.Views.RemoveAll(v => v.Name == name);
            project.Views.Add(view);
            project.Save(doc);
            ViewGenerator.Generate(doc, view, project);
            TanukiPlugin.RaiseViewsChanged();
            return Result.Success;
        }
    }

    public class TanukiSection : Command
    {
        public static TanukiSection Instance { get; private set; }
        public override string EnglishName => "TanukiSection";
        public TanukiSection() { Instance = this; }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var project = TanukiProject.Load(doc);

            var gn = new GetString();
            gn.SetCommandPrompt("断面図の名前 (例: A-A, B-B)");
            gn.SetDefaultString("A-A");
            gn.Get();
            if (gn.CommandResult() != Result.Success) return gn.CommandResult();
            string name = $"Section_{gn.StringResult().Replace("::", "_")}";

            var gp1 = new GetPoint();
            gp1.SetCommandPrompt("断面線の始点");
            gp1.Get();
            if (gp1.CommandResult() != Result.Success) return gp1.CommandResult();

            var gp2 = new GetPoint();
            gp2.SetCommandPrompt("断面線の終点");
            gp2.Get();
            if (gp2.CommandResult() != Result.Success) return gp2.CommandResult();

            // ① 3点目クリックで見る方向を決定
            var midPt    = new Point3d(
                (gp1.Point().X + gp2.Point().X) * 0.5,
                (gp1.Point().Y + gp2.Point().Y) * 0.5, 0);
            var cutDir   = gp2.Point() - gp1.Point(); cutDir.Unitize();
            double lineLen = gp1.Point().DistanceTo(gp2.Point());

            var gp3 = new GetPoint();
            gp3.SetCommandPrompt("見る方向側をクリック（切断線のどちら側を見るか）");
            gp3.DynamicDraw += (sender2, e) =>
            {
                var toMouse = e.CurrentPoint - midPt;
                toMouse.Z = 0;
                if (toMouse.Length > 1)
                {
                    toMouse.Unitize();
                    var arrowTip = midPt + toMouse * (lineLen * 0.3);
                    e.Display.DrawLine(midPt, arrowTip, System.Drawing.Color.Magenta, 2);
                }
            };
            gp3.Get();
            if (gp3.CommandResult() != Result.Success) return gp3.CommandResult();

            var toClick = new Vector3d(gp3.Point().X - midPt.X, gp3.Point().Y - midPt.Y, 0);
            double cross = cutDir.X * toClick.Y - cutDir.Y * toClick.X;
            bool viewRight = cross < 0;

            var view = new ViewDef
            {
                Name        = name,
                LayerKey    = name,
                Type        = ViewType.Section,
                CutStartX   = gp1.Point().X,
                CutStartY   = gp1.Point().Y,
                CutEndX     = gp2.Point().X,
                CutEndY     = gp2.Point().Y,
                ViewRight   = viewRight,
                DisplayMode       = project.DefaultDisplayMode,
                PresentationStyle = project.DefaultPresentationStyle,
                IncludeMeshes     = project.DefaultIncludeMeshes,
                ViewDepth         = project.DefaultViewDepth,
            };

            // モデル上にマーカー線を追加
            var markerLine = new Line(gp1.Point(), gp2.Point());
            System.Collections.Generic.List<System.Guid> indicatorIds;
            var markerId = AddMarker(doc, markerLine, name, System.Drawing.Color.Magenta, viewRight, out indicatorIds);
            view.MarkerObjectId      = markerId;
            view.MarkerIndicatorIds  = indicatorIds;
            ViewPlacement.Pick(doc, view);

            project.Views.RemoveAll(v => v.Name == name);
            project.Views.Add(view);
            project.Save(doc);

            ViewGenerator.Generate(doc, view, project);
            TanukiPlugin.RaiseViewsChanged();
            return Result.Success;
        }

        private Guid AddMarker(RhinoDoc doc, Line line, string name, System.Drawing.Color color, bool viewRight, out System.Collections.Generic.List<System.Guid> indicatorIds)
        {
            int layerIdx = MarkerDrawer.EnsureMarkersLayer(doc);
            var lineAttr = new Rhino.DocObjects.ObjectAttributes
            {
                LayerIndex  = layerIdx,
                ColorSource = Rhino.DocObjects.ObjectColorSource.ColorFromObject,
                ObjectColor = color,
                Name        = $"[Tanuki Marker] {name}"
            };
            var id = doc.Objects.AddLine(line, lineAttr);
            indicatorIds = MarkerDrawer.DrawIndicators(doc, line, name, viewRight, layerIdx, color);
            return id;
        }
    }

    public class TanukiElevation : Command
    {
        public static TanukiElevation Instance { get; private set; }
        public override string EnglishName => "TanukiElevation";
        public TanukiElevation() { Instance = this; }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var project = TanukiProject.Load(doc);

            var gn = new GetString();
            gn.SetCommandPrompt("立面図の名前 (例: North, South, EastA)");
            gn.SetDefaultString("North");
            gn.Get();
            if (gn.CommandResult() != Result.Success) return gn.CommandResult();
            string name = $"Elevation_{gn.StringResult().Replace("::", "_")}";

            // 立面図の位置を平面で指定（2点で方向と視線を決める）
            var gp1 = new GetPoint();
            gp1.SetCommandPrompt("立面線の始点（建物の手前側）");
            gp1.Get();
            if (gp1.CommandResult() != Result.Success) return gp1.CommandResult();

            var gp2 = new GetPoint();
            gp2.SetCommandPrompt("立面線の終点");
            gp2.Get();
            if (gp2.CommandResult() != Result.Success) return gp2.CommandResult();

            var view = new ViewDef
            {
                Name      = name,
                LayerKey  = name,
                Type      = ViewType.Elevation,
                CutStartX = gp1.Point().X,
                CutStartY = gp1.Point().Y,
                CutEndX   = gp2.Point().X,
                CutEndY   = gp2.Point().Y,
                ViewRight = true,
                DisplayMode       = project.DefaultDisplayMode,
                PresentationStyle = project.DefaultPresentationStyle,
                IncludeMeshes     = project.DefaultIncludeMeshes,
                ViewDepth         = project.DefaultViewDepth,
            };
            ViewPlacement.Pick(doc, view);

            project.Views.RemoveAll(v => v.Name == name);
            project.Views.Add(view);
            project.Save(doc);

            ViewGenerator.Generate(doc, view, project);
            TanukiPlugin.RaiseViewsChanged();
            return Result.Success;
        }
    }

    public class TanukiUpdateAll : Command
    {
        public static TanukiUpdateAll Instance { get; private set; }
        public override string EnglishName => "TanukiUpdateAll";
        public TanukiUpdateAll() { Instance = this; }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var project = TanukiProject.Load(doc);
            if (project.Views.Count == 0) { RhinoApp.WriteLine("図面がありません"); return Result.Nothing; }
            ViewGenerator.GenerateAll(doc, project);
            return Result.Success;
        }
    }

    // 図面生成コマンド共通: 配置基準点の対話入力
    internal static class ViewPlacement
    {
        internal static void Pick(RhinoDoc doc, ViewDef view)
        {
            var gp = new GetPoint();
            gp.SetCommandPrompt("配置基準点をクリック (Enter で自動配置)");
            gp.AcceptNothing(true);
            gp.Get();
            if (gp.CommandResult() != Result.Success) return;

            var  pt        = gp.Point();
            var  bbox      = Generators.DrawingPlacer.GetModelBBox(doc);
            bool isSection = view.Type == ViewType.Section || view.Type == ViewType.Elevation;

            view.PlacedOffsetX = isSection ? pt.X : (bbox.IsValid ? pt.X - bbox.Min.X : pt.X);
            view.PlacedOffsetY = isSection ? 0    : (bbox.IsValid ? pt.Y - bbox.Min.Y : pt.Y);
            view.HasPlacement  = true;
        }
    }
}
