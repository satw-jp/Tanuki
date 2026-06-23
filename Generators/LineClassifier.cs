using System.Collections.Generic;
using Rhino;
using Rhino.Geometry;

namespace Tanuki.Generators
{
    public enum LineType { Cut, Visible, Hidden }

    public class ClassifiedCurve
    {
        public Curve   Curve    { get; set; }
        public LineType LineType { get; set; }
        public int     SourceLayerIndex { get; set; }
    }

    /// <summary>
    /// ジオメトリから断面線・見え掛かり・隠れ線を分類して返す
    /// </summary>
    public static class LineClassifier
    {
        /// <summary>
        /// 断面/立面用の線分類。
        /// cutDir/cutLength を指定すると断面線の幅方向外にあるオブジェクトをスキップし、
        /// 処理コストを大幅に削減できる。
        /// </summary>
        public static List<ClassifiedCurve> Classify(
            RhinoDoc doc,
            Plane cutPlane,
            Vector3d viewDirection,
            Vector3d cutDir    = default,
            double cutLength   = 0,
            double cutMargin   = 2000)
        {
            var result = new List<ClassifiedCurve>();
            double tol = doc.ModelAbsoluteTolerance;
            bool widthCull = cutLength > 0 && cutDir.Length > 0.5;

            foreach (var obj in doc.Objects)
            {
                if (obj.IsHidden || !obj.IsValid) continue;
                if (IsTanukiLayer(doc, obj.Attributes.LayerIndex)) continue;

                int srcLayer = obj.Attributes.LayerIndex;
                var geo = obj.Geometry;

                var bbox = geo.GetBoundingBox(false);

                // ── 幅方向カリング（断面線の横幅外は全スキップ）──
                if (widthCull && bbox.IsValid)
                {
                    double minW = double.MaxValue, maxW = double.MinValue;
                    foreach (var corner in bbox.GetCorners())
                    {
                        double w = (corner - cutPlane.Origin) * cutDir;
                        if (w < minW) minW = w;
                        if (w > maxW) maxW = w;
                    }
                    if (maxW < -cutMargin || minW > cutLength + cutMargin) continue;
                }

                // ── 断面線（切断面との交差）──
                var cuts = GetCutCurves(geo, cutPlane, tol);
                foreach (var c in cuts)
                    result.Add(new ClassifiedCurve { Curve = c, LineType = LineType.Cut, SourceLayerIndex = srcLayer });

                // ── 見え掛かり・隠れ線（面法線による自己隠蔽判定）──
                // 切断面より先（視線方向側）にあるオブジェクトのみ投影する
                bool anyBeyondCut = false;
                if (bbox.IsValid)
                    foreach (var corner in bbox.GetCorners())
                        if ((corner - cutPlane.Origin) * viewDirection > -tol) { anyBeyondCut = true; break; }
                if (!anyBeyondCut) continue;

                var visible = new List<Curve>();
                var hidden  = new List<Curve>();
                ClassifyEdges(geo, cutPlane, viewDirection, visible, hidden);

                foreach (var c in visible)
                    result.Add(new ClassifiedCurve { Curve = c, LineType = LineType.Visible, SourceLayerIndex = srcLayer });
                foreach (var c in hidden)
                    result.Add(new ClassifiedCurve { Curve = c, LineType = LineType.Hidden, SourceLayerIndex = srcLayer });
            }

            return result;
        }

        public static List<ClassifiedCurve> ClassifyFloorPlan(
            RhinoDoc doc,
            double cutHeight,
            bool reflected = false)
        {
            var result = new List<ClassifiedCurve>();
            double tol = doc.ModelAbsoluteTolerance;
            var cutPlane = new Plane(new Point3d(0, 0, cutHeight), Vector3d.ZAxis);

            // 天井伏図の見え掛かりは上方向 (+Z) から投影
            // ミラーは削除: 通り芯と同じ XY 向きを保つ
            var rcpViewDir = Vector3d.ZAxis;

            foreach (var obj in doc.Objects)
            {
                if (obj.IsHidden || !obj.IsValid) continue;
                if (IsTanukiLayer(doc, obj.Attributes.LayerIndex)) continue;

                int srcLayer = obj.Attributes.LayerIndex;

                // 断面線（カット高さでの断面）
                var cuts = GetCutCurves(obj.Geometry, cutPlane, tol);
                foreach (var c in cuts)
                    result.Add(new ClassifiedCurve { Curve = c, LineType = LineType.Cut, SourceLayerIndex = srcLayer });

                // 天井伏図のみ: カット高さより上にあるオブジェクトの見え掛かりを投影
                if (!reflected) continue;

                var bbox = obj.Geometry.GetBoundingBox(false);
                bool anyAboveCut = false;
                if (bbox.IsValid)
                    foreach (var corner in bbox.GetCorners())
                        if (corner.Z - cutHeight > -tol) { anyAboveCut = true; break; }
                if (!anyAboveCut) continue;

                var visible = new System.Collections.Generic.List<Curve>();
                var hidden  = new System.Collections.Generic.List<Curve>();
                ClassifyEdges(obj.Geometry, cutPlane, rcpViewDir, visible, hidden);

                foreach (var c in visible)
                    result.Add(new ClassifiedCurve { Curve = c, LineType = LineType.Visible, SourceLayerIndex = srcLayer });
            }

            return result;
        }

        // ── Private helpers ──

        private static List<Curve> GetCutCurves(GeometryBase geo, Plane plane, double tol)
        {
            var result = new List<Curve>();
            Curve[] cuts = null;

            if      (geo is Brep b)     cuts = Brep.CreateContourCurves(b, plane);
            else if (geo is Mesh m)     cuts = Mesh.CreateContourCurves(m, plane, tol);
            else if (geo is Extrusion e){ var br = e.ToBrep(); if (br != null) cuts = Brep.CreateContourCurves(br, plane); }

            if (cuts != null) result.AddRange(cuts);
            return result;
        }

        private static void ClassifyEdges(
            GeometryBase geo,
            Plane cutPlane,
            Vector3d viewDir,
            List<Curve> visible,
            List<Curve> hidden)
        {
            var projectPlane = new Plane(cutPlane.Origin, viewDir);

            if      (geo is Brep brep)    { ClassifyBrepEdges(brep, projectPlane, viewDir, visible, hidden); }
            else if (geo is Extrusion ex) { var br = ex.ToBrep(); if (br != null) ClassifyBrepEdges(br, projectPlane, viewDir, visible, hidden); }
            else if (geo is Mesh mesh)
            {
                var nakedEdges = mesh.GetNakedEdges();
                if (nakedEdges != null)
                    foreach (var pl in nakedEdges)
                    {
                        var c = pl.ToNurbsCurve();
                        if (c != null && c.IsValid) visible.Add(c);
                    }
            }
            else if (geo is Curve curve) { visible.Add(curve.DuplicateCurve()); }
        }

        private static void ClassifyBrepEdges(
            Brep brep,
            Plane projectPlane,
            Vector3d viewDir,
            List<Curve> visible,
            List<Curve> hidden)
        {
            // Determine which faces are front-facing (outward normal opposes viewDir)
            var frontFace = new bool[brep.Faces.Count];
            for (int fi = 0; fi < brep.Faces.Count; fi++)
            {
                var face = brep.Faces[fi];
                Vector3d n = face.NormalAt(face.Domain(0).Mid, face.Domain(1).Mid);
                if (face.OrientationIsReversed) n = -n;
                frontFace[fi] = (n * viewDir) < 0.0;
            }

            // Classify each edge by whether any adjacent face is front-facing
            foreach (var edge in brep.Edges)
            {
                var dup = edge.DuplicateCurve();
                if (dup == null) continue;
                var projected = Curve.ProjectToPlane(dup, projectPlane);
                if (projected == null || !projected.IsValid) continue;

                bool anyFront = false;
                int[] trimIndices = edge.TrimIndices();
                if (trimIndices != null)
                {
                    foreach (int ti in trimIndices)
                    {
                        if (ti >= 0 && ti < brep.Trims.Count)
                        {
                            int faceIdx = brep.Trims[ti].Face.FaceIndex;
                            if (faceIdx >= 0 && faceIdx < frontFace.Length && frontFace[faceIdx])
                            { anyFront = true; break; }
                        }
                    }
                }

                if (anyFront) visible.Add(projected);
                else          hidden.Add(projected);
            }
        }

        private static bool IsTanukiLayer(RhinoDoc doc, int layerIndex)
        {
            var layer = doc.Layers[layerIndex];
            if (layer == null) return false;
            var root = layer.FullPath.Split(new[]{ "::" }, System.StringSplitOptions.None)[0];
            return root == "Tanuki";
        }
    }
}
