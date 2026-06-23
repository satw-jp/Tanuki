using System;
using System.Collections.Generic;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Tanuki.Data;

namespace Tanuki.Generators
{
    /// <summary>
    /// 通り芯寸法（平面図）とレベル寸法（断面/立面）を生成する
    /// </summary>
    public static class DimensionGenerator
    {
        // ── 平面図：通り芯寸法チェーン ──────────────────────────────────────

        public static void AddFloorPlanDimensions(
            RhinoDoc doc, ViewDef view, TanukiProject project, Transform offset)
        {
            if (project.GridLines.Count < 2) return;

            string viewPath = $"Tanuki::{view.GetLayerKey()}";
            int viewIdx = doc.Layers.FindByFullPath(viewPath, RhinoMath.UnsetIntIndex);
            if (viewIdx == RhinoMath.UnsetIntIndex) return;

            int dimIdx = GetOrCreateLayer(doc, "寸法", viewIdx, System.Drawing.Color.DimGray);
            var attr   = new ObjectAttributes { LayerIndex = dimIdx };
            double textH = 200;

            var bbox = DrawingPlacer.GetModelBBox(doc);
            if (!bbox.IsValid) return;

            double gap = project.BubbleRadius * 2 + 500;

            // N-S方向（Y向き）: 水平寸法チェーン（上端）
            var ns = new List<GridLine>();
            // E-W方向（X向き）: 垂直寸法チェーン（左端）
            var ew = new List<GridLine>();
            foreach (var gl in project.GridLines)
            {
                if (Math.Abs(gl.DirectionY) > Math.Abs(gl.DirectionX)) ns.Add(gl);
                else                                                      ew.Add(gl);
            }

            if (ns.Count >= 2)
            {
                ns.Sort((a, b) => a.OriginX.CompareTo(b.OriginX));
                double dimY = bbox.Max.Y + gap;
                var refPt = new Point3d(0, dimY, 0);
                refPt.Transform(offset);
                double topY = refPt.Y;

                for (int i = 0; i + 1 < ns.Count; i++)
                {
                    var p1 = new Point3d(ns[i].OriginX,     dimY, 0); p1.Transform(offset);
                    var p2 = new Point3d(ns[i+1].OriginX,   dimY, 0); p2.Transform(offset);
                    DrawDimH(doc, p1.X, p2.X, topY, Math.Abs(ns[i+1].OriginX - ns[i].OriginX), textH, attr);
                }
                if (ns.Count > 2)
                {
                    var p1 = new Point3d(ns[0].OriginX,              dimY + textH * 3.5, 0); p1.Transform(offset);
                    var p2 = new Point3d(ns[ns.Count-1].OriginX,     dimY + textH * 3.5, 0); p2.Transform(offset);
                    DrawDimH(doc, p1.X, p2.X, p1.Y,
                        Math.Abs(ns[ns.Count-1].OriginX - ns[0].OriginX), textH, attr);
                }
            }

            if (ew.Count >= 2)
            {
                ew.Sort((a, b) => a.OriginY.CompareTo(b.OriginY));
                double dimX = bbox.Min.X - gap;
                var refPt = new Point3d(dimX, 0, 0);
                refPt.Transform(offset);
                double leftX = refPt.X;

                for (int i = 0; i + 1 < ew.Count; i++)
                {
                    var p1 = new Point3d(dimX, ew[i].OriginY,   0); p1.Transform(offset);
                    var p2 = new Point3d(dimX, ew[i+1].OriginY, 0); p2.Transform(offset);
                    DrawDimV(doc, p1.Y, p2.Y, leftX, Math.Abs(ew[i+1].OriginY - ew[i].OriginY), textH, attr);
                }
                if (ew.Count > 2)
                {
                    var p1 = new Point3d(dimX - textH * 3.5, ew[0].OriginY,             0); p1.Transform(offset);
                    var p2 = new Point3d(dimX - textH * 3.5, ew[ew.Count-1].OriginY,    0); p2.Transform(offset);
                    DrawDimV(doc, p1.Y, p2.Y, p1.X,
                        Math.Abs(ew[ew.Count-1].OriginY - ew[0].OriginY), textH, attr);
                }
            }
        }

        // ── 断面/立面：レベル寸法チェーン ───────────────────────────────────

        public static void AddSectionLevelDimensions(
            RhinoDoc doc, ViewDef view, TanukiProject project,
            Transform offset, Transform flatten)
        {
            if (project.Levels.Count < 2) return;

            string viewPath = $"Tanuki::{view.GetLayerKey()}";
            int viewIdx = doc.Layers.FindByFullPath(viewPath, RhinoMath.UnsetIntIndex);
            if (viewIdx == RhinoMath.UnsetIntIndex) return;

            int dimIdx = GetOrCreateLayer(doc, "寸法", viewIdx, System.Drawing.Color.DimGray);
            var attr   = new ObjectAttributes { LayerIndex = dimIdx };
            double textH = 200;

            var levels = new List<Level>(project.Levels);
            levels.Sort((a, b) => a.Elevation.CompareTo(b.Elevation));

            // 断面左端 X を推定（フラット化後の切断線始点）
            var cutPlane  = view.GetCutPlane();
            var viewDir   = view.GetViewDirection();
            var projPlane = new Plane(cutPlane.Origin, viewDir);
            var start3d   = new Point3d(view.CutStartX, view.CutStartY, 0);
            var startFlat = ProjectToPlane(start3d, projPlane);
            startFlat.Transform(flatten);
            // 始点のX座標（フラット化後）＝切断線の「左端」
            double dimX = startFlat.X - 2500;

            for (int i = 0; i + 1 < levels.Count; i++)
            {
                // 各レベルの Y 座標（フラット化後 = 高さ）
                var wpt1 = new Point3d(view.CutStartX, view.CutStartY, levels[i].Elevation);
                var wpt2 = new Point3d(view.CutStartX, view.CutStartY, levels[i+1].Elevation);
                var fp1  = ProjectToPlane(wpt1, projPlane); fp1.Transform(flatten);
                var fp2  = ProjectToPlane(wpt2, projPlane); fp2.Transform(flatten);

                var p1 = new Point3d(dimX, fp1.Y, 0); p1.Transform(offset);
                var p2 = new Point3d(dimX, fp2.Y, 0); p2.Transform(offset);

                double dist = Math.Abs(levels[i+1].Elevation - levels[i].Elevation);
                DrawDimV(doc, p1.Y, p2.Y, p1.X, dist, textH, attr);
            }

            // 全体寸法
            if (levels.Count > 2)
            {
                var wpt1 = new Point3d(view.CutStartX, view.CutStartY, levels[0].Elevation);
                var wptN = new Point3d(view.CutStartX, view.CutStartY, levels[levels.Count-1].Elevation);
                var fp1  = ProjectToPlane(wpt1, projPlane); fp1.Transform(flatten);
                var fpN  = ProjectToPlane(wptN, projPlane); fpN.Transform(flatten);

                var p1 = new Point3d(dimX - textH * 3.5, fp1.Y, 0); p1.Transform(offset);
                var pN = new Point3d(dimX - textH * 3.5, fpN.Y, 0); pN.Transform(offset);
                double totalDist = Math.Abs(levels[levels.Count-1].Elevation - levels[0].Elevation);
                DrawDimV(doc, p1.Y, pN.Y, p1.X, totalDist, textH, attr);
            }
        }

        // ── 水平寸法線（線 + テキスト） ────────────────────────────────────

        private static void DrawDimH(RhinoDoc doc, double x1, double x2, double y,
            double dist, double textH, ObjectAttributes attr)
        {
            if (Math.Abs(x2 - x1) < 1) return;
            double tickH = textH * 0.8;
            double mid   = (x1 + x2) / 2.0;

            doc.Objects.AddLine(new Line(new Point3d(x1, y, 0), new Point3d(x2, y, 0)), attr);
            doc.Objects.AddLine(new Line(new Point3d(x1, y - tickH * 0.5, 0), new Point3d(x1, y + tickH * 0.5, 0)), attr);
            doc.Objects.AddLine(new Line(new Point3d(x2, y - tickH * 0.5, 0), new Point3d(x2, y + tickH * 0.5, 0)), attr);

            var te = new TextEntity { PlainText = $"{dist:F0}", TextHeight = textH, Justification = TextJustification.BottomCenter };
            te.Plane = new Plane(new Point3d(mid, y + textH * 0.2, 0), Vector3d.ZAxis);
            doc.Objects.Add(te, attr);
        }

        // ── 垂直寸法線（線 + テキスト） ────────────────────────────────────

        private static void DrawDimV(RhinoDoc doc, double y1, double y2, double x,
            double dist, double textH, ObjectAttributes attr)
        {
            if (Math.Abs(y2 - y1) < 1) return;
            double tickW = textH * 0.8;
            double mid   = (y1 + y2) / 2.0;

            doc.Objects.AddLine(new Line(new Point3d(x, y1, 0), new Point3d(x, y2, 0)), attr);
            doc.Objects.AddLine(new Line(new Point3d(x - tickW * 0.5, y1, 0), new Point3d(x + tickW * 0.5, y1, 0)), attr);
            doc.Objects.AddLine(new Line(new Point3d(x - tickW * 0.5, y2, 0), new Point3d(x + tickW * 0.5, y2, 0)), attr);

            // テキストを90度回転（YAxis=上, XAxis方向=正のX方向だと文字が左向きになるので逆に）
            var te = new TextEntity { PlainText = $"{dist:F0}", TextHeight = textH, Justification = TextJustification.BottomCenter };
            te.Plane = new Plane(
                new Point3d(x - textH * 0.2, mid, 0),
                new Vector3d(0, 1, 0),
                new Vector3d(-1, 0, 0));
            doc.Objects.Add(te, attr);
        }

        // ── ヘルパー ───────────────────────────────────────────────────────

        private static Point3d ProjectToPlane(Point3d pt, Plane plane)
        {
            Vector3d n = plane.Normal; n.Unitize();
            return pt - (pt - plane.Origin) * n * n;
        }

        private static int GetOrCreateLayer(RhinoDoc doc, string name, int parentIdx, System.Drawing.Color color)
        {
            return LayerUtil.GetOrCreate(doc, name, parentIdx, color);
        }
    }
}
