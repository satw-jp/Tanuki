using System.Collections.Generic;
using Rhino.Geometry;

namespace Tanuki.Generators
{
    /// <summary>
    /// 投影後の曲線から重複線を除去し、可視線と重なる隠れ線を削除する
    /// </summary>
    public static class CurveCleanup
    {
        /// <param name="minLength">除去する最小長(mm)。0 の場合は tol を使用。ViewScale*0.1 推奨。</param>
        public static List<ClassifiedCurve> Process(List<ClassifiedCurve> curves, double tol, double minLength = 0)
        {
            if (minLength < tol) minLength = tol;
            var deduped = RemoveDuplicates(curves, tol, minLength);
            return RemoveShadowedHidden(deduped, tol);
        }

        // 同一線種グループ内の重複曲線を除去（始点・終点の一致で判定）
        private static List<ClassifiedCurve> RemoveDuplicates(List<ClassifiedCurve> input, double tol, double minLength)
        {
            var result = new List<ClassifiedCurve>();
            foreach (var cc in input)
            {
                if (!cc.Curve.IsValid || cc.Curve.GetLength() < minLength) continue;
                bool isDup = false;
                foreach (var ex in result)
                {
                    if (ex.LineType != cc.LineType) continue;
                    if (IsSameSegment(ex.Curve, cc.Curve, tol)) { isDup = true; break; }
                }
                if (!isDup) result.Add(cc);
            }
            return result;
        }

        // 可視線と同位置の隠れ線を除去（可視が優先）
        private static List<ClassifiedCurve> RemoveShadowedHidden(List<ClassifiedCurve> input, double tol)
        {
            var visibles = new List<Curve>();
            foreach (var cc in input)
                if (cc.LineType == LineType.Visible) visibles.Add(cc.Curve);

            var result = new List<ClassifiedCurve>();
            foreach (var cc in input)
            {
                if (cc.LineType == LineType.Hidden)
                {
                    bool shadowed = false;
                    foreach (var v in visibles)
                        if (IsSameSegment(v, cc.Curve, tol)) { shadowed = true; break; }
                    if (shadowed) continue;
                }
                result.Add(cc);
            }
            return result;
        }

        private static bool IsSameSegment(Curve a, Curve b, double tol)
        {
            var aS = a.PointAtStart; var aE = a.PointAtEnd;
            var bS = b.PointAtStart; var bE = b.PointAtEnd;
            return (aS.DistanceTo(bS) < tol && aE.DistanceTo(bE) < tol) ||
                   (aS.DistanceTo(bE) < tol && aE.DistanceTo(bS) < tol);
        }
    }
}
