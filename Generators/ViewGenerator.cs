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

            AddDrawingTitle(doc, view, project, offset);
            RefreshMarkerIndicators(doc, view);
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
            var curves = LineClassifier.ClassifyFloorPlan(doc, view.CutHeight, reflected, view.IncludeMeshes);

            if (project.GridLines.Count > 0)
            {
                var gridCurves = GridSymbolGenerator.GenerateSymbols(project.GridLines, project.BubbleRadius);
                curves.AddRange(gridCurves);
            }

            int layerIdx = DrawingPlacer.Place(doc, view.GetLayerKey(), curves, offset, view.LayerMode, replace,
                                               minLength: project.ViewScale * 0.1);

            AddPoché(doc, curves, view.GetLayerKey(), Transform.Identity, offset, doc.ModelAbsoluteTolerance);

            if (project.GridLines.Count > 0 && layerIdx >= 0)
                GridSymbolGenerator.PlaceGridText(doc, project.GridLines, layerIdx, offset, project.BubbleRadius);

            if (project.GridLines.Count >= 2)
                DimensionGenerator.AddFloorPlanDimensions(doc, view, project, offset);
        }

        // ── Section / Elevation ───────────────────────────────────────────────

        private static void GenerateSectionOrElevation(RhinoDoc doc, ViewDef view, TanukiProject project, Transform offset, bool replace)
        {
            var cutPlane = view.GetCutPlane();
            var viewDir  = view.GetViewDirection();

            // XAxis=切断線方向, YAxis=ZAxis を明示 → 低い方が必ず下
            var cutDirH  = new Vector3d(view.CutEndX - view.CutStartX, view.CutEndY - view.CutStartY, 0);
            cutDirH.Unitize();
            var projPlane = new Plane(cutPlane.Origin, cutDirH, Vector3d.ZAxis);
            var flatten   = Transform.PlaneToPlane(projPlane, Plane.WorldXY);

            if (view.DisplayMode == ViewDisplayMode.Presentation)
            {
                var regions = SurfaceClassifier.Classify(doc, projPlane, viewDir);
                DrawingPlacer.PlacePresentation(doc, regions, view.GetLayerKey(), flatten, offset);
            }

            var cutDirVec  = new Vector3d(view.CutEndX - view.CutStartX, view.CutEndY - view.CutStartY, 0);
            double cutLen2D = cutDirVec.Length;
            cutDirVec.Unitize();
            var curves = LineClassifier.Classify(doc, cutPlane, viewDir, cutDirVec, cutLen2D, 2000,
                                                 view.IncludeMeshes, view.ViewDepth);

            if (project.GridLines.Count > 0)
                AddCrossingGridLines(doc, view, project, projPlane, curves);

            AddLevelLines(view, project, projPlane, curves);

            foreach (var cc in curves)
                cc.Curve.Transform(flatten);

            int layerIdx = DrawingPlacer.Place(doc, view.GetLayerKey(), curves, offset, view.LayerMode, replace,
                                               minLength: project.ViewScale * 0.1);

            AddPoché(doc, curves, view.GetLayerKey(), Transform.Identity, offset, doc.ModelAbsoluteTolerance);

            if (project.GridLines.Count > 0 && layerIdx >= 0)
                AddCrossingGridText(doc, view, project, projPlane, layerIdx, offset, flatten);

            if (project.Levels.Count > 0 && layerIdx >= 0)
                AddLevelLabels(doc, view, project, projPlane, layerIdx, offset, flatten);

            if (project.Levels.Count >= 2)
                DimensionGenerator.AddSectionLevelDimensions(doc, view, project, offset, flatten);
        }

        // ── レベル参照線 ──────────────────────────────────────────────────────

        private static void AddLevelLines(
            ViewDef sectionView, TanukiProject project,
            Plane projPlane, List<ClassifiedCurve> curves)
        {
            if (project.Levels.Count == 0) return;

            double cutDX = sectionView.CutEndX - sectionView.CutStartX;
            double cutDY = sectionView.CutEndY - sectionView.CutStartY;
            double len   = Math.Sqrt(cutDX * cutDX + cutDY * cutDY);
            if (len < 1) return;
            double ndx = cutDX / len;
            double ndy = cutDY / len;
            double ext = len * 0.3;

            foreach (var level in project.Levels)
            {
                double z = level.Elevation;

                var pt1 = new Point3d(sectionView.CutStartX - ndx * ext,
                                      sectionView.CutStartY - ndy * ext, z);
                var pt2 = new Point3d(sectionView.CutEndX   + ndx * ext,
                                      sectionView.CutEndY   + ndy * ext, z);

                var projected = Curve.ProjectToPlane(new Line(pt1, pt2).ToNurbsCurve(), projPlane);
                if (projected == null || !projected.IsValid) continue;

                curves.Add(new ClassifiedCurve
                {
                    Curve            = projected,
                    LineType         = LineType.Level,
                    SourceLayerIndex = 0
                });
            }
        }

        private static void AddLevelLabels(
            RhinoDoc doc, ViewDef view, TanukiProject project,
            Plane projPlane, int layerIdx, Transform offset, Transform flatten)
        {
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

        // ── 通り芯交差線 ──────────────────────────────────────────────────────

        private static void AddCrossingGridLines(
            RhinoDoc doc, ViewDef view, TanukiProject project,
            Plane projPlane, List<ClassifiedCurve> curves)
        {
            var bbox = DrawingPlacer.GetModelBBox(doc);
            double zMin = bbox.IsValid ? bbox.Min.Z - 1000 : -1000;
            double zMax = bbox.IsValid ? bbox.Max.Z + 1000 : 10000;

            double cutDX = view.CutEndX - view.CutStartX;
            double cutDY = view.CutEndY - view.CutStartY;

            foreach (var gl in project.GridLines)
            {
                double cross = cutDX * gl.DirectionY - cutDY * gl.DirectionX;
                if (Math.Abs(cross) < 1e-6) continue;

                double deltaX = gl.OriginX - view.CutStartX;
                double deltaY = gl.OriginY - view.CutStartY;
                double t = (deltaX * gl.DirectionY - deltaY * gl.DirectionX) / cross;

                // 切断線の幅外（10%マージン）の通り芯はスキップ
                if (t < -0.1 || t > 1.1) continue;

                double px = view.CutStartX + t * cutDX;
                double py = view.CutStartY + t * cutDY;

                var projected = Curve.ProjectToPlane(
                    new Line(new Point3d(px, py, zMin), new Point3d(px, py, zMax)).ToNurbsCurve(),
                    projPlane);
                if (projected != null && projected.IsValid)
                    curves.Add(new ClassifiedCurve { Curve = projected, LineType = LineType.Grid, SourceLayerIndex = 0 });

                foreach (var z in new[] { zMin, zMax })
                {
                    var center3d    = new Point3d(px, py, z);
                    var centerOnPlane = ProjectPointToPlane(center3d, projPlane);
                    var bubble = new Circle(
                        new Plane(centerOnPlane, projPlane.XAxis, projPlane.YAxis), project.BubbleRadius);
                    curves.Add(new ClassifiedCurve { Curve = bubble.ToNurbsCurve(), LineType = LineType.Grid, SourceLayerIndex = 0 });
                }
            }
        }

        private static void AddCrossingGridText(
            RhinoDoc doc, ViewDef view, TanukiProject project,
            Plane projPlane, int layerIdx, Transform offset, Transform flatten)
        {
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

                if (t < -0.1 || t > 1.1) continue;

                double px = view.CutStartX + t * cutDX;
                double py = view.CutStartY + t * cutDY;

                foreach (var z in new[] { zMin, zMax })
                {
                    var center3d      = new Point3d(px, py, z);
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

        // ── ポシェハッチ ──────────────────────────────────────────────────────

        private static void AddPoché(
            RhinoDoc doc, List<ClassifiedCurve> curves, string layerKey,
            Transform flatten, Transform offset, double tol)
        {
            // flatten は平面図用（断面/立面では呼び出し前に適用済みなので Identity を渡す）
            var cutCurves = new List<Curve>();
            foreach (var cc in curves)
            {
                if (cc.LineType != LineType.Cut) continue;
                var dup = cc.Curve.DuplicateCurve();
                dup.Transform(flatten);
                dup.Transform(offset);
                cutCurves.Add(dup);
            }
            if (cutCurves.Count == 0) return;

            var joined = Curve.JoinCurves(cutCurves.ToArray(), tol);
            if (joined == null || joined.Length == 0) return;

            var closed = new List<Curve>();
            foreach (var c in joined)
                if (c.IsClosed) closed.Add(c);
            if (closed.Count == 0) return;

            // Solid ハッチパターン取得
            int patIdx = doc.HatchPatterns.Find("Solid", true);
            if (patIdx < 0)
                patIdx = doc.HatchPatterns.Add(Rhino.DocObjects.HatchPattern.Defaults.Solid);

            string safe = layerKey.Replace("::", "_");
            int viewIdx = doc.Layers.FindByFullPath($"Tanuki::{safe}", RhinoMath.UnsetIntIndex);
            if (viewIdx == RhinoMath.UnsetIntIndex) return;

            // ポシェレイヤーの既存オブジェクト削除 or 新規作成
            int pocheIdx = doc.Layers.FindByFullPath($"Tanuki::{safe}::ポシェ", RhinoMath.UnsetIntIndex);
            if (pocheIdx >= 0)
            {
                LayerUtil.ForEachObject(doc, pocheIdx, o => doc.Objects.Delete(o, true));
            }
            else
            {
                var pl = new Rhino.DocObjects.Layer
                {
                    Name          = "ポシェ",
                    Color         = System.Drawing.Color.Black,
                    ParentLayerId = doc.Layers[viewIdx].Id
                };
                pocheIdx = doc.Layers.Add(pl);
            }

            var attr = new Rhino.DocObjects.ObjectAttributes { LayerIndex = pocheIdx };
            var hatches = Hatch.Create(closed.ToArray(), patIdx, 0, 1, tol);
            if (hatches == null) return;
            foreach (var h in hatches)
                doc.Objects.AddHatch(h, attr);
        }

        // ── マーカーインジケーター再描画 ──────────────────────────────────────

        private static void RefreshMarkerIndicators(RhinoDoc doc, ViewDef view)
        {
            if (view.MarkerObjectId == Guid.Empty) return;
            if (view.MarkerIndicatorIds == null || view.MarkerIndicatorIds.Count == 0) return;

            MarkerDrawer.DeleteIndicators(doc, view.MarkerIndicatorIds);

            var markerLine = new Line(
                new Point3d(view.CutStartX, view.CutStartY, 0),
                new Point3d(view.CutEndX,   view.CutEndY,   0));
            int layerIdx = MarkerDrawer.EnsureMarkersLayer(doc);
            var color = view.Type == ViewType.Elevation
                ? System.Drawing.Color.Cyan
                : System.Drawing.Color.Magenta;
            view.MarkerIndicatorIds = MarkerDrawer.DrawIndicators(
                doc, markerLine, view.Name, view.ViewRight, layerIdx, color);
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

            double modelH = bbox.Max.Y - bbox.Min.Y;
            double titleY = (view.Type == ViewType.FloorPlan || view.Type == ViewType.RCP)
                ? bbox.Min.Y - modelH - 3000
                : bbox.Min.Z - 4000;

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

            var existingTitle = doc.Objects.FindByLayer(doc.Layers[ti]);
            if (existingTitle != null)
                foreach (var o in existingTitle) doc.Objects.Delete(o, true);

            var attr = new Rhino.DocObjects.ObjectAttributes { LayerIndex = ti };

            doc.Objects.AddLine(
                new Line(
                    new Point3d(titlePt.X - (bbox.Max.X - bbox.Min.X) * 0.5, titlePt.Y + 1200, 0),
                    new Point3d(titlePt.X + (bbox.Max.X - bbox.Min.X) * 0.5, titlePt.Y + 1200, 0)),
                attr);

            string titleText = $"{view.Name}   1:{project.ViewScale}";
            var te = new Rhino.Geometry.TextEntity
            {
                PlainText     = titleText,
                TextHeight    = view.LabelTextHeight,
                Justification = Rhino.Geometry.TextJustification.BottomCenter
            };
            te.Plane = new Plane(new Point3d(titlePt.X, titlePt.Y + 1300, 0), Vector3d.ZAxis);
            doc.Objects.Add(te, attr);
        }

        // ── 自動配置オフセット ────────────────────────────────────────────────

        private static Transform GetOffset(RhinoDoc doc, ViewDef view, TanukiProject project)
        {
            if (view.HasPlacement)
                return Transform.Translation(new Vector3d(view.PlacedOffsetX, view.PlacedOffsetY, 0));

            var bbox = DrawingPlacer.GetModelBBox(doc);
            double margin = 5000;
            double modelH = bbox.IsValid ? bbox.Max.Y - bbox.Min.Y : 15000;
            double modelW = bbox.IsValid ? bbox.Max.X - bbox.Min.X : 15000;
            double zOff   = bbox.IsValid ? -bbox.Min.Z : 0;

            Vector3d v;

            if (view.Type == ViewType.FloorPlan || view.Type == ViewType.RCP)
            {
                double baseY = bbox.IsValid ? bbox.Min.Y - modelH - margin : -(modelH + margin);
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
                double baseX = bbox.IsValid ? bbox.Max.X + margin : modelW + margin;
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
