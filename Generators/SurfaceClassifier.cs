using System;
using System.Collections.Generic;
using System.Drawing;
using Rhino;
using Rhino.Geometry;

namespace Tanuki.Generators
{
    // オクルージョン（前景オブジェクトによる背景クリッピング）は未対応。
    // Painter's Algorithm による描画順制御のみ実施する（KI-001 と同様の制約）。
    public static class SurfaceClassifier
    {
        public static List<ProjectedRegion> Classify(
            RhinoDoc doc,
            Plane    projPlane,
            Vector3d viewDir)
        {
            var result = new List<ProjectedRegion>();
            double tol = doc.ModelAbsoluteTolerance;

            foreach (var obj in doc.Objects)
            {
                if (obj.IsHidden || !obj.IsValid) continue;
                if (IsTanukiLayer(doc, obj.Attributes.LayerIndex)) continue;

                // LineClassifier と同一の bbox フィルター: 切断平面より手前のオブジェクトを除外
                var bbox = obj.Geometry.GetBoundingBox(false);
                if (bbox.IsValid)
                {
                    bool anyBeyond = false;
                    foreach (var corner in bbox.GetCorners())
                        if ((corner - projPlane.Origin) * viewDir > -tol)
                        { anyBeyond = true; break; }
                    if (!anyBeyond) continue;
                }

                Brep brep = obj.Geometry as Brep;
                if (brep == null)
                {
                    var ex = obj.Geometry as Extrusion;
                    if (ex != null) brep = ex.ToBrep();
                }
                if (brep == null) continue;

                Color fillColor = doc.Layers[obj.Attributes.LayerIndex].Color;
                ProjectFrontFaces(brep, projPlane, viewDir, tol, fillColor, result);
            }

            // Painter's Algorithm: 奥行き降順（遠い面を先に描画）
            result.Sort((a, b) => b.Depth.CompareTo(a.Depth));
            return result;
        }

        private static void ProjectFrontFaces(
            Brep brep, Plane projPlane, Vector3d viewDir,
            double tol, Color color, List<ProjectedRegion> result)
        {
            for (int fi = 0; fi < brep.Faces.Count; fi++)
            {
                var face = brep.Faces[fi];

                // Step 0: front-face テスト（LineClassifier.ClassifyBrepEdges と同一ロジック）
                Vector3d n = face.NormalAt(face.Domain(0).Mid, face.Domain(1).Mid);
                if (face.OrientationIsReversed) n = -n;
                if ((n * viewDir) >= 0.0) continue;         // 裏向き
                if (Math.Abs(n * viewDir) < 0.1) continue;  // 視線に平行 → 縮退防止

                Brep flatBrep;
                if (!TryProjectFace(face, projPlane, tol, out flatBrep))
                    continue;

                // BBox 最大投影値を depth として使用
                // 中心値ではなく最遠点を基準にすることで、奥に伸びる面が確実に先に描画される
                double depth = double.MinValue;
                foreach (var corner in face.GetBoundingBox(false).GetCorners())
                {
                    double d = (corner - projPlane.Origin) * viewDir;
                    if (d > depth) depth = d;
                }

                result.Add(new ProjectedRegion
                {
                    FlatBrep  = flatBrep,
                    FillColor = color,
                    Depth     = depth,
                });
            }
        }

        private static bool TryProjectFace(
            BrepFace face, Plane projPlane, double tol, out Brep flatBrep)
        {
            flatBrep = null;

            // Step 1: 外周ループ取得
            // InnerLoop（開口部・穴）は Phase 1 では未対応。
            // Phase 2 拡張ポイント: face.Loops を走査して BrepLoopType.Inner の曲線を収集し、
            // Brep.CreatePlanarBreps に [outerCurve, innerCurve1, ...] として渡す。
            if (face.OuterLoop == null) return false;
            var loopCurve = face.OuterLoop.To3dCurve();
            if (loopCurve == null || !loopCurve.IsValid) return false;

            // Step 2: projPlane への正射影
            var projected = Curve.ProjectToPlane(loopCurve, projPlane);
            if (projected == null || !projected.IsValid) return false;

            // Step 3: 閉鎖確認
            if (!projected.IsClosed)
            {
                var joinedA = Curve.JoinCurves(new Curve[] { projected }, tol);
                if (joinedA != null && joinedA.Length > 0 && joinedA[0].IsClosed)
                {
                    projected = joinedA[0];
                }
                else
                {
                    // FB-B: 公差を 10 倍に拡大
                    var joinedB = Curve.JoinCurves(new Curve[] { projected }, tol * 10.0);
                    if (joinedB == null || joinedB.Length == 0 || !joinedB[0].IsClosed)
                        return false;
                    projected = joinedB[0];
                }
            }

            // Step 4: 最小サイズ確認（縮退した投影を除外）
            var projBBox = projected.GetBoundingBox(false);
            double minSize = tol * 10.0;
            if (projBBox.Max.X - projBBox.Min.X < minSize &&
                projBBox.Max.Y - projBBox.Min.Y < minSize)
                return false;

            // Step 5: 平坦 Brep 生成
            var planarBreps = Brep.CreatePlanarBreps(new Curve[] { projected }, tol);
            if (planarBreps == null || planarBreps.Length == 0)
            {
                // FB-C: 公差を 10 倍に拡大
                planarBreps = Brep.CreatePlanarBreps(new Curve[] { projected }, tol * 10.0);
                if (planarBreps == null || planarBreps.Length == 0)
                    return false;
            }

            flatBrep = planarBreps[0];
            return true;
        }

        private static bool IsTanukiLayer(RhinoDoc doc, int layerIndex)
        {
            var layer = doc.Layers[layerIndex];
            if (layer == null) return false;
            return layer.FullPath.Split(
                new string[] { "::" },
                StringSplitOptions.None)[0] == "Tanuki";
        }
    }
}
