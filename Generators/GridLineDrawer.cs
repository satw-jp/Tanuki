using System.Collections.Generic;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Tanuki.Data;

namespace Tanuki.Generators
{
    /// <summary>
    /// 通り芯をRhinoのモデル空間に可視化する（Tanuki::通り芯レイヤー）
    /// </summary>
    public static class GridLineDrawer
    {
        private const string LayerPath  = "Tanuki::通り芯";
        private const double BubbleR    = 400;
        private const double TextHeight = 360;

        public static void SyncToDoc(RhinoDoc doc, List<GridLine> gridLines)
        {
            // 既存オブジェクトをすべて削除
            int layerIdx = EnsureLayer(doc);
            var existing = doc.Objects.FindByLayer(doc.Layers[layerIdx]);
            if (existing != null)
                foreach (var o in existing) doc.Objects.Delete(o, true);

            var attr = new ObjectAttributes { LayerIndex = layerIdx };

            foreach (var gl in gridLines)
            {
                var line = gl.ToLine();

                // 線本体
                doc.Objects.AddLine(line, attr);

                // 両端のバブル＋テキスト
                foreach (var pt in new[] { line.From, line.To })
                {
                    var plane  = new Plane(pt, Vector3d.ZAxis);
                    var circle = new Circle(plane, BubbleR);
                    doc.Objects.AddCircle(circle, attr);

                    var te = new TextEntity
                    {
                        PlainText     = gl.Name,
                        TextHeight    = TextHeight,
                        Justification = TextJustification.MiddleCenter
                    };
                    te.Plane = plane;
                    doc.Objects.Add(te, attr);
                }
            }

            doc.Views.Redraw();
        }

        private static int EnsureLayer(RhinoDoc doc)
        {
            // 親レイヤー Tanuki
            int rootIdx = doc.Layers.FindByFullPath("Tanuki", RhinoMath.UnsetIntIndex);
            if (rootIdx == RhinoMath.UnsetIntIndex)
            {
                var root = new Layer { Name = "Tanuki", Color = System.Drawing.Color.DimGray };
                rootIdx = doc.Layers.Add(root);
            }

            // 子レイヤー 通り芯
            int idx = doc.Layers.FindByFullPath(LayerPath, RhinoMath.UnsetIntIndex);
            if (idx != RhinoMath.UnsetIntIndex) return idx;

            var layer = new Layer
            {
                Name           = "通り芯",
                Color          = System.Drawing.Color.FromArgb(0, 128, 255),
                ParentLayerId  = doc.Layers[rootIdx].Id
            };
            return doc.Layers.Add(layer);
        }
    }
}
