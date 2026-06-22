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

            var view = new ViewDef { Name = viewName, Type = ViewType.FloorPlan, CutHeight = cutHeight };
            project.Views.RemoveAll(v => v.Name == viewName);
            project.Views.Add(view);
            project.Save(doc);

            ViewGenerator.Generate(doc, view, project);
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
            var view = new ViewDef { Name = name, Type = ViewType.RCP, CutHeight = gh.Number() };
            project.Views.RemoveAll(v => v.Name == name);
            project.Views.Add(view);
            project.Save(doc);
            ViewGenerator.Generate(doc, view, project);
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
            string name = $"Section_{gn.StringResult()}";

            var gp1 = new GetPoint();
            gp1.SetCommandPrompt("断面線の始点");
            gp1.Get();
            if (gp1.CommandResult() != Result.Success) return gp1.CommandResult();

            var gp2 = new GetPoint();
            gp2.SetCommandPrompt("断面線の終点");
            gp2.Get();
            if (gp2.CommandResult() != Result.Success) return gp2.CommandResult();

            // 視線方向（右手側か左手側か）
            var go = new GetOption();
            go.SetCommandPrompt("見る方向");
            go.AddOption("右手側");
            go.AddOption("左手側");
            go.Get();
            bool viewRight = go.Option().Index == 1;

            var view = new ViewDef
            {
                Name        = name,
                Type        = ViewType.Section,
                CutStartX   = gp1.Point().X,
                CutStartY   = gp1.Point().Y,
                CutEndX     = gp2.Point().X,
                CutEndY     = gp2.Point().Y,
                ViewRight   = viewRight
            };

            // モデル上にマーカー線を追加
            var markerLine = new Line(gp1.Point(), gp2.Point());
            var markerId = AddMarker(doc, markerLine, name, System.Drawing.Color.Magenta, viewRight);
            view.MarkerObjectId = markerId;

            project.Views.RemoveAll(v => v.Name == name);
            project.Views.Add(view);
            project.Save(doc);

            ViewGenerator.Generate(doc, view, project);
            return Result.Success;
        }

        private Guid AddMarker(RhinoDoc doc, Line line, string name, System.Drawing.Color color, bool viewRight = true)
        {
            int layerIdx = GetOrCreateMarkerLayer(doc);
            var lineAttr = new Rhino.DocObjects.ObjectAttributes
            {
                LayerIndex  = layerIdx,
                ColorSource = Rhino.DocObjects.ObjectColorSource.ColorFromObject,
                ObjectColor = color,
                Name        = $"[Tanuki Marker] {name}"
            };
            var id = doc.Objects.AddLine(line, lineAttr);

            // 視線方向インジケーター（各端に矢印マーク）
            AddDirectionIndicator(doc, line, name, viewRight, layerIdx, color);
            return id;
        }

        private void AddDirectionIndicator(
            RhinoDoc doc, Line line, string name,
            bool viewRight, int layerIdx, System.Drawing.Color color)
        {
            var attr = new Rhino.DocObjects.ObjectAttributes
            {
                LayerIndex  = layerIdx,
                ColorSource = Rhino.DocObjects.ObjectColorSource.ColorFromObject,
                ObjectColor = color
            };

            // 切断線の方向ベクトルと視線方向ベクトル
            var cutDir  = line.Direction; cutDir.Unitize();
            var viewDir = viewRight
                ? new Vector3d(-cutDir.Y,  cutDir.X, 0)   // 右手側
                : new Vector3d( cutDir.Y, -cutDir.X, 0);  // 左手側

            double tickLen  = line.Length * 0.08;
            double arrowLen = tickLen * 0.6;

            // 各端点に L字マーク（端点から視線方向へ）+ 矢印
            foreach (var pt in new[] { line.From, line.To })
            {
                var tipPt = pt + viewDir * tickLen;

                // 垂直ティック
                doc.Objects.AddLine(new Line(pt, tipPt), attr);

                // 矢印（2本の斜め線）
                var vLeft  = viewDir;
                var vRight = viewDir;
                vLeft.Transform(Transform.Rotation(0.5, Vector3d.ZAxis, Point3d.Origin));
                vRight.Transform(Transform.Rotation(-0.5, Vector3d.ZAxis, Point3d.Origin));
                doc.Objects.AddLine(new Line(tipPt, tipPt - vLeft  * arrowLen), attr);
                doc.Objects.AddLine(new Line(tipPt, tipPt - vRight * arrowLen), attr);
            }

            // ラベルテキスト（線の中間、視線方向寄り）
            var midPt = line.PointAt(0.5) + viewDir * (tickLen * 0.6);
            var te = new Rhino.Geometry.TextEntity
            {
                PlainText     = name,
                TextHeight    = Math.Max(line.Length * 0.04, 200),
                Justification = Rhino.Geometry.TextJustification.MiddleCenter
            };
            te.Plane = new Plane(midPt, Vector3d.ZAxis);
            doc.Objects.Add(te, attr);
        }

        private int GetOrCreateMarkerLayer(RhinoDoc doc)
        {
            int rootIdx = EnsureRootLayer(doc);
            string path = "Tanuki::Markers";
            int idx = doc.Layers.FindByFullPath(path, RhinoMath.UnsetIntIndex);
            if (idx != RhinoMath.UnsetIntIndex) return idx;
            var layer = new Rhino.DocObjects.Layer
            {
                Name = "Markers",
                Color = System.Drawing.Color.Magenta,
                ParentLayerId = doc.Layers[rootIdx].Id
            };
            return doc.Layers.Add(layer);
        }

        private int EnsureRootLayer(RhinoDoc doc)
        {
            int idx = doc.Layers.FindByFullPath("Tanuki", RhinoMath.UnsetIntIndex);
            if (idx != RhinoMath.UnsetIntIndex) return idx;
            return doc.Layers.Add(new Rhino.DocObjects.Layer { Name = "Tanuki", Color = System.Drawing.Color.DimGray });
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
            string name = $"Elevation_{gn.StringResult()}";

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
                Type      = ViewType.Elevation,
                CutStartX = gp1.Point().X,
                CutStartY = gp1.Point().Y,
                CutEndX   = gp2.Point().X,
                CutEndY   = gp2.Point().Y,
                ViewRight = true
            };

            project.Views.RemoveAll(v => v.Name == name);
            project.Views.Add(view);
            project.Save(doc);

            ViewGenerator.Generate(doc, view, project);
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
}
