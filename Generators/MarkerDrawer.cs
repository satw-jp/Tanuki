using System;
using System.Collections.Generic;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace Tanuki.Generators
{
    /// <summary>
    /// 断面/立面マーカー（切断線 + 視線方向インジケーター）の描画を担う。
    /// インジケーター（L字ティック・矢印・ラベル）のGUIDを返すことで、
    /// マーカー移動時に削除→再生成できる。
    /// </summary>
    public static class MarkerDrawer
    {
        private const string MarkersPath = "Tanuki::Markers";

        public static int EnsureMarkersLayer(RhinoDoc doc)
        {
            int rootIdx = doc.Layers.FindByFullPath("Tanuki", RhinoMath.UnsetIntIndex);
            if (rootIdx == RhinoMath.UnsetIntIndex)
                rootIdx = doc.Layers.Add(new Layer { Name = "Tanuki", Color = System.Drawing.Color.DimGray });

            int idx = doc.Layers.FindByFullPath(MarkersPath, RhinoMath.UnsetIntIndex);
            if (idx != RhinoMath.UnsetIntIndex) return idx;

            var layer = new Layer
            {
                Name          = "Markers",
                Color         = System.Drawing.Color.Magenta,
                ParentLayerId = doc.Layers[rootIdx].Id
            };
            return doc.Layers.Add(layer);
        }

        /// <summary>
        /// 断面線の視線方向インジケーター（Lティック×2 ＋ 矢印×4 ＋ ラベル×1）を描画し、
        /// 作成した全オブジェクトの Guid リストを返す。
        /// </summary>
        public static List<Guid> DrawIndicators(
            RhinoDoc doc, Line markerLine, string viewName, bool viewRight,
            int layerIdx, System.Drawing.Color color)
        {
            var ids  = new List<Guid>();
            var attr = new ObjectAttributes
            {
                LayerIndex  = layerIdx,
                ColorSource = ObjectColorSource.ColorFromObject,
                ObjectColor = color
            };

            var cutDir = markerLine.Direction; cutDir.Unitize();
            var viewDir = viewRight
                ? new Vector3d(-cutDir.Y,  cutDir.X, 0)
                : new Vector3d( cutDir.Y, -cutDir.X, 0);

            double tickLen  = markerLine.Length * 0.08;
            double arrowLen = tickLen * 0.6;

            foreach (var pt in new[] { markerLine.From, markerLine.To })
            {
                var tipPt = pt + viewDir * tickLen;

                // 垂直ティック
                ids.Add(doc.Objects.AddLine(new Line(pt, tipPt), attr));

                // 矢印（2本の斜め線）
                var vL = viewDir; vL.Transform(Transform.Rotation( 0.5, Vector3d.ZAxis, Point3d.Origin));
                var vR = viewDir; vR.Transform(Transform.Rotation(-0.5, Vector3d.ZAxis, Point3d.Origin));
                ids.Add(doc.Objects.AddLine(new Line(tipPt, tipPt - vL * arrowLen), attr));
                ids.Add(doc.Objects.AddLine(new Line(tipPt, tipPt - vR * arrowLen), attr));
            }

            // 断面名ラベル（線の中間・視線方向寄り）
            var midPt = markerLine.PointAt(0.5) + viewDir * (tickLen * 0.6);
            var te = new TextEntity
            {
                PlainText     = viewName,
                TextHeight    = Math.Max(markerLine.Length * 0.04, 200),
                Justification = TextJustification.MiddleCenter
            };
            te.Plane = new Plane(midPt, Vector3d.ZAxis);
            ids.Add(doc.Objects.Add(te, attr));

            return ids;
        }

        /// <summary>インジケーター群を一括削除する。</summary>
        public static void DeleteIndicators(RhinoDoc doc, List<Guid> ids)
        {
            if (ids == null) return;
            foreach (var id in ids)
                if (id != Guid.Empty) doc.Objects.Delete(id, true);
        }
    }
}
