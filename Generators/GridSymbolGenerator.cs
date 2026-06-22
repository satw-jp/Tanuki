using System.Collections.Generic;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Tanuki.Data;

namespace Tanuki.Generators
{
    /// <summary>
    /// 通り芯のバブル記号（丸＋ラベル）を生成する
    /// </summary>
    public static class GridSymbolGenerator
    {
        private const double BubbleRadius = 400; // mm

        public static List<ClassifiedCurve> GenerateSymbols(RhinoDoc doc, List<GridLine> gridLines)
        {
            var result = new List<ClassifiedCurve>();
            double textHeight = BubbleRadius * 0.9;

            foreach (var gl in gridLines)
            {
                var line = gl.ToLine();

                // 線本体
                result.Add(new ClassifiedCurve
                {
                    Curve = line.ToNurbsCurve(),
                    LineType = LineType.Visible,
                    SourceLayerIndex = 0
                });

                // 両端にバブル
                AddBubble(result, doc, line.From, gl.Name, textHeight);
                AddBubble(result, doc, line.To,   gl.Name, textHeight);
            }

            return result;
        }

        private static void AddBubble(
            List<ClassifiedCurve> result,
            RhinoDoc doc,
            Point3d center,
            string label,
            double textHeight)
        {
            // 丸
            var circle = new Circle(new Plane(center, Vector3d.ZAxis), BubbleRadius);
            result.Add(new ClassifiedCurve
            {
                Curve = circle.ToNurbsCurve(),
                LineType = LineType.Visible,
                SourceLayerIndex = 0
            });

            // ラベルはRhinoのテキストとして別途追加（PlaceにてTextEntityを使用）
            // ClassifiedCurveに乗せられないのでTagに名前を格納
            // → DrawingPlacerのPlaceGridTextで別途追加
        }

        public static void PlaceGridText(
            RhinoDoc doc,
            List<GridLine> gridLines,
            int layerIdx,
            Transform offset)
        {
            double textHeight = BubbleRadius * 0.9;

            foreach (var gl in gridLines)
            {
                var line = gl.ToLine();
                foreach (var pt in new[] { line.From, line.To })
                {
                    var textPt = pt;
                    textPt.Transform(offset);

                    var te = new TextEntity
                    {
                        PlainText  = gl.Name,
                        TextHeight = textHeight,
                        Justification = TextJustification.MiddleCenter
                    };
                    te.Plane = new Plane(textPt, Vector3d.ZAxis);

                    var attr = new ObjectAttributes { LayerIndex = layerIdx };
                    doc.Objects.Add(te, attr);
                }
            }
        }
    }
}
