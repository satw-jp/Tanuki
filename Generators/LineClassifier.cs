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
        public static List<ClassifiedCurve> Classify(
            RhinoDoc doc,
            Plane cutPlane,
            Vector3d viewDirection)
        {
            var result = new List<ClassifiedCurve>();
            double tol = doc.ModelAbsoluteTolerance;

            foreach (var obj in doc.Objects)
            {
                if (obj.IsHidden || !obj.IsValid) continue;
                if (IsTanukiLayer(doc, obj.Attributes.LayerIndex)) continue;

                int srcLayer = obj.Attributes.LayerIndex;
                var geo = obj.Geometry;

                // ── 断面線（切断面との交差）──
                var cuts = GetCutCurves(geo, cutPlane, tol);
                foreach (var c in cuts)
                    result.Add(new ClassifiedCurve { Curve = c, LineType = LineType.Cut, SourceLayerIndex = srcLayer });

                // ── 見え掛かり・隠れ線（面法線による自己隠蔽判定）──
                // 切断面より先（視線方向側）にあるオブジェクトのみ投影する
                var bbox = geo.GetBoundingBox(false);
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

            foreach (var obj in doc.Objects)
            {
                if (obj.IsHidden || !obj.IsValid) continue;
                if (IsTanukiLayer(doc, obj.Attributes.LayerIndex)) continue;

                int srcLayer = obj.Attributes.LayerIndex;
                var cuts = GetCutCurves(obj.Geometry, cutPlane, tol);

                if (reflected)
                {
                    var mirror = Transform.Mirror(new Plane(Point3d.Origin, Vector3d.XAxis, Vector3d.ZAxis));
                    foreach (var c in cuts) c.Transform(mirror);
                }

                foreach (var c in cuts)
                    result.Add(new ClassifiedCurve { Curve = c, LineType = LineType.Cut, SourceLayerIndex = srcLayer });
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
