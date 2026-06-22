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
            Vector3d viewDirection,
            bool includeHidden = false)
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

                // ── 見え掛かり（投影）──
                var visible = GetProjectedEdges(geo, cutPlane, viewDirection);
                foreach (var c in visible)
                    result.Add(new ClassifiedCurve { Curve = c, LineType = LineType.Visible, SourceLayerIndex = srcLayer });
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

        private static List<Curve> GetProjectedEdges(GeometryBase geo, Plane cutPlane, Vector3d viewDir)
        {
            var result = new List<Curve>();
            var projectPlane = new Plane(cutPlane.Origin, viewDir);
            var edges = ExtractEdges(geo);

            foreach (var e in edges)
            {
                var projected = Curve.ProjectToPlane(e, projectPlane);
                if (projected != null && projected.IsValid)
                    result.Add(projected);
            }
            return result;
        }

        private static List<Curve> ExtractEdges(GeometryBase geo)
        {
            var result = new List<Curve>();
            if      (geo is Brep b)     { foreach (var e in b.Edges) result.Add(e.DuplicateCurve()); }
            else if (geo is Extrusion e){ var br = e.ToBrep(); if (br != null) foreach (var ed in br.Edges) result.Add(ed.DuplicateCurve()); }
            else if (geo is Mesh m)     { var edges = m.GetNakedEdges(); if (edges != null) foreach (var pl in edges) result.Add(pl.ToNurbsCurve()); }
            else if (geo is Curve c)    { result.Add(c.DuplicateCurve()); }
            return result;
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
