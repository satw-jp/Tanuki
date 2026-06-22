using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Geometry;
using Tanuki.Data;

namespace Tanuki.Generators
{
    public static class ViewGenerator
    {
        public static void Generate(RhinoDoc doc, ViewDef view, TanukiProject project, bool replaceExisting = true)
        {
            var offset = GetOffset(doc, view);

            switch (view.Type)
            {
                case ViewType.FloorPlan:
                case ViewType.RCP:
                    GenerateFloorPlan(doc, view, project, offset, replaceExisting);
                    break;
                case ViewType.Elevation:
                case ViewType.Section:
                    GenerateSectionOrElevation(doc, view, project, offset, replaceExisting);
                    break;
            }
        }

        public static void GenerateAll(RhinoDoc doc, TanukiProject project)
        {
            foreach (var view in project.Views)
                Generate(doc, view, project, replaceExisting: true);
        }

        // ── Floor Plan / RCP ──────────────────────────────────────────────────

        private static void GenerateFloorPlan(RhinoDoc doc, ViewDef view, TanukiProject project, Transform offset, bool replace)
        {
            bool reflected = view.Type == ViewType.RCP;
            var curves = LineClassifier.ClassifyFloorPlan(doc, view.CutHeight, reflected);

            // 通り芯バブル記号
            if (project.GridLines.Count > 0)
            {
                var gridCurves = GridSymbolGenerator.GenerateSymbols(project.GridLines);
                curves.AddRange(gridCurves);
            }

            int layerIdx = DrawingPlacer.Place(doc, view.Name, curves, offset, project.LayerMode, replace);

            // 通り芯テキストラベル
            if (project.GridLines.Count > 0 && layerIdx >= 0)
                GridSymbolGenerator.PlaceGridText(doc, project.GridLines, layerIdx, offset);
        }

        // ── Section / Elevation ───────────────────────────────────────────────

        private static void GenerateSectionOrElevation(RhinoDoc doc, ViewDef view, TanukiProject project, Transform offset, bool replace)
        {
            var cutPlane = view.GetCutPlane();
            var viewDir  = view.GetViewDirection();
            var curves   = LineClassifier.Classify(doc, cutPlane, viewDir);

            // 切断線と交差する通り芯を垂直線として追加
            if (project.GridLines.Count > 0)
                AddCrossingGridLines(doc, view, project, cutPlane, viewDir, curves);

            int layerIdx = DrawingPlacer.Place(doc, view.Name, curves, offset, project.LayerMode, replace);

            // 交差通り芯のテキストラベル（断面図の上下に）
            if (project.GridLines.Count > 0 && layerIdx >= 0)
                AddCrossingGridText(doc, view, project, cutPlane, viewDir, layerIdx, offset);
        }

        // ── 断面/立面に交差する通り芯の垂直線を生成 ──────────────────────────

        private static void AddCrossingGridLines(
            RhinoDoc doc, ViewDef view, TanukiProject project,
            Plane cutPlane, Vector3d viewDir,
            List<ClassifiedCurve> curves)
        {
            var projPlane = new Plane(cutPlane.Origin, viewDir);

            var bbox = DrawingPlacer.GetModelBBox(doc);
            double zMin = bbox.IsValid ? bbox.Min.Z - 1000 : -1000;
            double zMax = bbox.IsValid ? bbox.Max.Z + 1000 : 10000;

            double cutDX = view.CutEndX - view.CutStartX;
            double cutDY = view.CutEndY - view.CutStartY;

            foreach (var gl in project.GridLines)
            {
                // 2D交差判定（XY平面）
                double cross = cutDX * gl.DirectionY - cutDY * gl.DirectionX;
                if (Math.Abs(cross) < 1e-6) continue; // 平行 → スキップ

                double deltaX = gl.OriginX - view.CutStartX;
                double deltaY = gl.OriginY - view.CutStartY;
                double t = (deltaX * gl.DirectionY - deltaY * gl.DirectionX) / cross;

                // 交差点（3Dワールド座標）
                double px = view.CutStartX + t * cutDX;
                double py = view.CutStartY + t * cutDY;

                // 垂直線を作成してセクション面に投影
                var vertLine = new Line(
                    new Point3d(px, py, zMin),
                    new Point3d(px, py, zMax));

                var projected = Curve.ProjectToPlane(vertLine.ToNurbsCurve(), projPlane);
                if (projected != null && projected.IsValid)
                    curves.Add(new ClassifiedCurve
                    {
                        Curve = projected,
                        LineType = LineType.Visible,
                        SourceLayerIndex = 0
                    });

                // 上下にバブル（円）
                foreach (var z in new[] { zMin, zMax })
                {
                    var center3d = new Point3d(px, py, z);
                    var centerOnPlane = ProjectPointToPlane(center3d, projPlane);
                    var bubble = new Circle(
                        new Plane(centerOnPlane, projPlane.XAxis, projPlane.YAxis), 400);
                    curves.Add(new ClassifiedCurve
                    {
                        Curve = bubble.ToNurbsCurve(),
                        LineType = LineType.Visible,
                        SourceLayerIndex = 0
                    });
                }
            }
        }

        private static void AddCrossingGridText(
            RhinoDoc doc, ViewDef view, TanukiProject project,
            Plane cutPlane, Vector3d viewDir,
            int layerIdx, Transform offset)
        {
            var projPlane = new Plane(cutPlane.Origin, viewDir);

            var bbox = DrawingPlacer.GetModelBBox(doc);
            double zMin = bbox.IsValid ? bbox.Min.Z - 1000 : -1000;
            double zMax = bbox.IsValid ? bbox.Max.Z + 1000 : 10000;

            double cutDX = view.CutEndX - view.CutStartX;
            double cutDY = view.CutEndY - view.CutStartY;

            var attr = new Rhino.DocObjects.ObjectAttributes { LayerIndex = layerIdx };

            foreach (var gl in project.GridLines)
            {
                double cross = cutDX * gl.DirectionY - cutDY * gl.DirectionX;
                if (Math.Abs(cross) < 1e-6) continue;

                double deltaX = gl.OriginX - view.CutStartX;
                double deltaY = gl.OriginY - view.CutStartY;
                double t = (deltaX * gl.DirectionY - deltaY * gl.DirectionX) / cross;

                double px = view.CutStartX + t * cutDX;
                double py = view.CutStartY + t * cutDY;

                foreach (var z in new[] { zMin, zMax })
                {
                    var center3d = new Point3d(px, py, z);
                    var centerOnPlane = ProjectPointToPlane(center3d, projPlane);
                    centerOnPlane.Transform(offset);

                    var te = new Rhino.Geometry.TextEntity
                    {
                        PlainText     = gl.Name,
                        TextHeight    = 360,
                        Justification = Rhino.Geometry.TextJustification.MiddleCenter
                    };
                    te.Plane = new Plane(centerOnPlane, projPlane.XAxis, projPlane.YAxis);
                    doc.Objects.Add(te, attr);
                }
            }
        }

        // ── ユーティリティ ────────────────────────────────────────────────────

        private static Point3d ProjectPointToPlane(Point3d pt, Plane plane)
        {
            Vector3d n = plane.Normal;
            n.Unitize();
            double d = (pt - plane.Origin) * n;
            return pt - d * n;
        }

        private static Transform GetOffset(RhinoDoc doc, ViewDef view)
        {
            if (view.PlacedOffsetX != 0 || view.PlacedOffsetY != 0)
                return Transform.Translation(new Vector3d(view.PlacedOffsetX, view.PlacedOffsetY, 0));

            var bbox = DrawingPlacer.GetModelBBox(doc);
            if (!bbox.IsValid) return Transform.Identity;

            double margin = 5000;
            double h = bbox.Max.Y - bbox.Min.Y;
            var v = new Vector3d(0, bbox.Min.Y - h - margin, -bbox.Min.Z);

            view.PlacedOffsetX = v.X;
            view.PlacedOffsetY = v.Y;

            return Transform.Translation(v);
        }
    }
}
