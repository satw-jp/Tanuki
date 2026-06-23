using System.Collections.Generic;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Tanuki.Data;

namespace Tanuki.Generators
{
    /// <summary>
    /// 図面内に配置する通り芯バブル記号（丸＋ラベル）を生成する。
    /// モデル空間への表示は GridLineDrawer が担当。
    /// </summary>
    public static class GridSymbolGenerator
    {
        private const double DefaultBubbleRadius = 400;
        private const double TextHeightRatio      = 0.9;

        public static List<ClassifiedCurve> GenerateSymbols(List<GridLine> gridLines, double bubbleRadius = DefaultBubbleRadius)
        {
            var result = new List<ClassifiedCurve>();

            foreach (var gl in gridLines)
            {
                var line = gl.ToLine();

                result.Add(new ClassifiedCurve
                {
                    Curve = line.ToNurbsCurve(),
                    LineType = LineType.Visible,
                    SourceLayerIndex = 0
                });

                foreach (var pt in new[] { line.From, line.To })
                {
                    var circle = new Circle(new Plane(pt, Vector3d.ZAxis), bubbleRadius);
                    result.Add(new ClassifiedCurve
                    {
                        Curve = circle.ToNurbsCurve(),
                        LineType = LineType.Visible,
                        SourceLayerIndex = 0
                    });
                }
            }

            return result;
        }

        public static void PlaceGridText(
            RhinoDoc doc,
            List<GridLine> gridLines,
            int layerIdx,
            Transform offset,
            double bubbleRadius = DefaultBubbleRadius)
        {
            var attr = new ObjectAttributes { LayerIndex = layerIdx };

            foreach (var gl in gridLines)
            {
                var line = gl.ToLine();
                foreach (var pt in new[] { line.From, line.To })
                {
                    var textPt = pt;
                    textPt.Transform(offset);

                    var te = new TextEntity
                    {
                        PlainText     = gl.Name,
                        TextHeight    = bubbleRadius * TextHeightRatio,
                        Justification = TextJustification.MiddleCenter
                    };
                    te.Plane = new Plane(textPt, Vector3d.ZAxis);
                    doc.Objects.Add(te, attr);
                }
            }
        }
    }
}
