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
            var offset = GetOffset(doc, view, project);

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

            // 図面タイトルと縮尺を追加
            AddDrawingTitle(doc, view, project, offset);

            // PlacedOffset を保存（次回再生成時に同じ位置を使用）
            project.Save(doc);
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
                var gridCurves = GridSymbolGenerator.GenerateSymbols(project.GridLines, project.BubbleRadius);
                curves.AddRange(gridCurves);
            }

            int layerIdx = DrawingPlacer.Place(doc, view.GetLayerKey(), curves, offset, project.LayerMode, replace);

            // 通り芯テキストラベル
            if (project.GridLines.Count > 0 && layerIdx >= 0)
                GridSymbolGenerator.PlaceGridText(doc, project.GridLines, layerIdx, offset, project.BubbleRadius);

            // 通り芯寸法チェーン
            if (project.GridLines.Count >= 2)
                DimensionGenerator.AddFloorPlanDimensions(doc, view, project, offset);
        }

        // ── Section / Elevation ───────────────────────────────────────────────

        private static void GenerateSectionOrElevation(RhinoDoc doc, ViewDef view, TanukiProject project, Transform offset, bool replace)
        {
            var cutPlane  = view.GetCutPlane();
            var viewDir   = view.GetViewDirection();
            var projPlane = new Plane(cutPlane.Origin, viewDir);
            // 投影平面 → XY平面へのフラット変換（シートに平面図と並べて配置できるようにする）
            var flatten   = Transform.PlaneToPlane(projPlane, Plane.WorldXY);

            if (view.DisplayMode == ViewDisplayMode.Presentation)
            {
                var regions = SurfaceClassifier.Classify(doc, projPlane, viewDir);
                DrawingPlacer.PlacePresentation(doc, regions, view.GetLayerKey(), flatten, offset);
            }

            var curves = LineClassifier.Classify(doc, cutPlane, viewDir);

            if (project.GridLines.Count > 0)
                AddCrossingGridLines(doc, view, project, cutPlane, viewDir, curves);

            AddLevelLines(view, project, cutPlane, viewDir, curves);

            foreach (var cc in curves)
                cc.Curve.Transform(flatten);

            int layerIdx = DrawingPlacer.Place(doc, view.GetLayerKey(), curves, offset, project.LayerMode, replace);

            if (project.GridLines.Count > 0 && layerIdx >= 0)
                AddCrossingGridText(doc, view, project, cutPlane, viewDir, layerIdx, offset, flatten);

            if (project.Levels.Count > 0 && layerIdx >= 0)
                AddLevelLabels(doc, view, project, cutPlane, viewDir, layerIdx, offset, flatten);

            // レベル寸法チェーン
            if (project.Levels.Count >= 2)
                DimensionGenerator.AddSectionLevelDimensions(doc, view, project, offset, flatten);
        }

        // ── レベル参照線（各レベル高さを断面/立面に表示） ─────────────────

        private static void AddLevelLines(
            ViewDef sectionView, TanukiProject project,
            Plane cutPlane, Vector3d viewDir,
            List<ClassifiedCurve> curves)
        {
            if (project.Levels.Count == 0) return;

            var projPlane = new Plane(cutPlane.Origin, viewDir);

            double cutDX = sectionView.CutEndX - sectionView.CutStartX;
            double cutDY = sectionView.CutEndY - sectionView.CutStartY;
            double len   = Math.Sqrt(cutDX * cutDX + cutDY * cutDY);
            if (len < 1) return;
            double ndx = cutDX / len;
            double ndy = cutDY / len;
            double ext = len * 0.3; // 切断線両端から延長

            foreach (var level in project.Levels)
            {
                double z = level.Elevation;

                var pt1 = new Point3d(sectionView.CutStartX - ndx * ext,
                                      sectionView.CutStartY - ndy * ext, z);
                var pt2 = new Point3d(sectionView.CutEndX   + ndx * ext,
                                      sectionView.CutEndY   + ndy * ext, z);

                var lineNurbs = new Line(pt1, pt2).ToNurbsCurve();
                var projected = Curve.ProjectToPlane(lineNurbs, projPlane);
                if (projected == null || !projected.IsValid) continue;

                curves.Add(new ClassifiedCurve
                {
                    Curve            = projected,
                    LineType         = LineType.Hidden,
                    SourceLayerIndex = 0
                });
            }
        }

        private static void AddLevelLabels(
            RhinoDoc doc, ViewDef view, TanukiProject project,
            Plane cutPlane, Vector3d viewDir,
            int layerIdx, Transform offset, Transform flatten)
        {
            var projPlane = new Plane(cutPlane.Origin, viewDir);

            double cutDX = view.CutEndX - view.CutStartX;
            double cutDY = view.CutEndY - view.CutStartY;
            double len   = Math.Sqrt(cutDX * cutDX + cutDY * cutDY);
            if (len < 1) return;
            double ndx = cutDX / len;
            double ndy = cutDY / len;
            double ext = len * 0.3;

            var attr = new Rhino.DocObjects.ObjectAttributes { LayerIndex = layerIdx };

            foreach (var level in project.Levels)
            {
                double z = level.Elevation;

                // 左端（切断線始点から ext 延長）の位置にラベル
                var worldPt = new Point3d(
                    view.CutStartX - ndx * ext - 500,
                    view.CutStartY - ndy * ext - 500,
                    z);
                var projected = ProjectPointToPlane(worldPt, projPlane);
                projected.Transform(flatten);
                projected.Transform(offset);

                var te = new Rhino.Geometry.TextEntity
                {
                    PlainText     = $"{level.Name}  FL{level.Elevation:F0}",
                    TextHeight    = 250,
                    Justification = Rhino.Geometry.TextJustification.MiddleRight
                };
                te.Plane = new Plane(projected, Vector3d.ZAxis);
                doc.Objects.Add(te, attr);
            }
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
                        new Plane(centerOnPlane, projPlane.XAxis, projPlane.YAxis), project.BubbleRadius);
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
            int layerIdx, Transform offset, Transform flatten)
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
                    centerOnPlane.Transform(flatten);
                    centerOnPlane.Transform(offset);

                    var te = new Rhino.Geometry.TextEntity
                    {
                        PlainText     = gl.Name,
                        TextHeight    = project.BubbleRadius * 0.9,
                        Justification = Rhino.Geometry.TextJustification.MiddleCenter
                    };
                    te.Plane = new Plane(centerOnPlane, Vector3d.ZAxis);
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

        // ── 図面タイトル ─────────────────────────────────────────────────────

        private static void AddDrawingTitle(RhinoDoc doc, ViewDef view, TanukiProject project, Transform offset)
        {
            var bbox = DrawingPlacer.GetModelBBox(doc);
            if (!bbox.IsValid) return;

            // 図面の下端を推定（モデル高さを基準に）
            double modelH = bbox.Max.Y - bbox.Min.Y;
            double titleY = (view.Type == ViewType.FloorPlan || view.Type == ViewType.RCP)
                ? bbox.Min.Y - modelH - 3000  // 平面図は Y 方向の下端
                : bbox.Min.Z - 4000;          // 断面/立面: フラット化後の高さ軸下端より下

            var titlePt = new Point3d(
                bbox.Min.X + (bbox.Max.X - bbox.Min.X) * 0.5,
                titleY, 0);
            titlePt.Transform(offset);

            int rootIdx = doc.Layers.FindByFullPath("Tanuki", RhinoMath.UnsetIntIndex);
            if (rootIdx == RhinoMath.UnsetIntIndex) return;

            string layerKey  = view.GetLayerKey();
            string titlePath = $"Tanuki::{layerKey}::タイトル";
            int ti = doc.Layers.FindByFullPath(titlePath, RhinoMath.UnsetIntIndex);
            if (ti == RhinoMath.UnsetIntIndex)
            {
                int vi = doc.Layers.FindByFullPath($"Tanuki::{layerKey}", RhinoMath.UnsetIntIndex);
                if (vi == RhinoMath.UnsetIntIndex) return;
                var tl = new Rhino.DocObjects.Layer
                {
                    Name          = "タイトル",
                    Color         = System.Drawing.Color.DimGray,
                    ParentLayerId = doc.Layers[vi].Id
                };
                ti = doc.Layers.Add(tl);
            }

            // 既存タイトルを削除
            var existingTitle = doc.Objects.FindByLayer(doc.Layers[ti]);
            if (existingTitle != null)
                foreach (var o in existingTitle) doc.Objects.Delete(o, true);

            var attr = new Rhino.DocObjects.ObjectAttributes { LayerIndex = ti };

            // 横線
            doc.Objects.AddLine(
                new Line(
                    new Point3d(titlePt.X - (bbox.Max.X - bbox.Min.X) * 0.5, titlePt.Y + 1200, 0),
                    new Point3d(titlePt.X + (bbox.Max.X - bbox.Min.X) * 0.5, titlePt.Y + 1200, 0)),
                attr);

            // タイトルテキスト
            string titleText = $"{view.Name}   1:{project.ViewScale}";
            var te = new Rhino.Geometry.TextEntity
            {
                PlainText     = titleText,
                TextHeight    = 500,
                Justification = Rhino.Geometry.TextJustification.BottomCenter
            };
            te.Plane = new Plane(new Point3d(titlePt.X, titlePt.Y + 1300, 0), Vector3d.ZAxis);
            doc.Objects.Add(te, attr);
        }

        // ── グリッド位置統一自動配置 ─────────────────────────────────────────

        private static Transform GetOffset(RhinoDoc doc, ViewDef view, TanukiProject project)
        {
            // ユーザーが明示的に配置済み
            if (view.HasPlacement)
                return Transform.Translation(new Vector3d(view.PlacedOffsetX, view.PlacedOffsetY, 0));

            var bbox = DrawingPlacer.GetModelBBox(doc);
            double margin = 5000;
            double modelH = bbox.IsValid ? bbox.Max.Y - bbox.Min.Y : 15000;
            double modelW = bbox.IsValid ? bbox.Max.X - bbox.Min.X : 15000;
            double modelZ = bbox.IsValid ? bbox.Max.Z - bbox.Min.Z : 5000;
            double zOff   = bbox.IsValid ? -bbox.Min.Z : 0;

            Vector3d v;

            if (view.Type == ViewType.FloorPlan || view.Type == ViewType.RCP)
            {
                // ★ 通り芯グリッド位置を揃えるため X=0 固定、Y 方向に積み重ね
                double baseY = bbox.IsValid
                    ? bbox.Min.Y - modelH - margin
                    : -(modelH + margin);
                double y = baseY;

                foreach (var other in project.Views)
                {
                    if (other == view || !other.HasPlacement) continue;
                    if (other.Type != ViewType.FloorPlan && other.Type != ViewType.RCP) continue;
                    double candidate = other.PlacedOffsetY - modelH - margin;
                    if (candidate < y) y = candidate;
                }

                v = new Vector3d(0, y, zOff);
            }
            else
            {
                // 断面/立面: 全平面図の右側に X 方向に並べる
                double baseX = bbox.IsValid
                    ? bbox.Max.X + margin
                    : modelW + margin;
                double x = baseX;

                foreach (var other in project.Views)
                {
                    if (other == view || !other.HasPlacement) continue;
                    if (other.Type == ViewType.FloorPlan || other.Type == ViewType.RCP) continue;
                    double cutLen = Math.Sqrt(
                        Math.Pow(other.CutEndX - other.CutStartX, 2) +
                        Math.Pow(other.CutEndY - other.CutStartY, 2));
                    double candidate = other.PlacedOffsetX + Math.Max(cutLen, modelW) + margin;
                    if (candidate > x) x = candidate;
                }

                v = new Vector3d(x, 0, 0);
            }

            view.PlacedOffsetX = v.X;
            view.PlacedOffsetY = v.Y;
            view.HasPlacement  = true;
            return Transform.Translation(v);
        }
    }
}
