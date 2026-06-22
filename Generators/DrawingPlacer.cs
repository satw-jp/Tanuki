using System.Collections.Generic;
using System.Drawing;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Tanuki.Data;

namespace Tanuki.Generators
{
    /// <summary>
    /// 図面カーブをRhinoのTanukiレイヤー群に配置する
    /// </summary>
    public static class DrawingPlacer
    {
        // Tanukiルートレイヤーの下にビュー名→その下に線種レイヤー
        // Tanuki::FloorPlan_1FL::断面線
        // Tanuki::FloorPlan_1FL::見え掛かり

        public static void Place(
            RhinoDoc doc,
            string viewName,
            List<ClassifiedCurve> curves,
            Transform offset,
            LayerMode mode,
            bool replaceExisting = true)
        {
            if (curves.Count == 0) return;

            if (replaceExisting) DeleteViewLayers(doc, viewName);

            int rootIdx  = GetOrCreateLayer(doc, "Tanuki",    -1,       Color.DimGray);
            int viewIdx  = GetOrCreateLayer(doc, viewName,    rootIdx,  Color.DimGray);
            int cutIdx   = GetOrCreateLayer(doc, "断面線",    viewIdx,  Color.Red);
            int visIdx   = GetOrCreateLayer(doc, "見え掛かり", viewIdx, Color.Black);
            int hidIdx   = GetOrCreateLayer(doc, "隠れ線",    viewIdx,  Color.Gray);

            foreach (var cc in curves)
            {
                var c = cc.Curve.DuplicateCurve();
                c.Transform(offset);

                int layerIdx;
                if (mode == LayerMode.OriginalLayer)
                {
                    // 元レイヤーの子レイヤーとして線種を作る
                    var srcLayer = doc.Layers[cc.SourceLayerIndex];
                    string suffix = cc.LineType == LineType.Cut     ? "断面線"
                                  : cc.LineType == LineType.Visible ? "見え掛かり"
                                  :                                    "隠れ線";
                    int srcInView = GetOrCreateLayer(doc, srcLayer?.Name ?? "Default", viewIdx, srcLayer?.Color ?? Color.Black);
                    layerIdx = GetOrCreateLayer(doc, suffix, srcInView, LineTypeColor(cc.LineType));
                }
                else
                {
                    layerIdx = cc.LineType == LineType.Cut     ? cutIdx
                             : cc.LineType == LineType.Visible ? visIdx
                             :                                    hidIdx;
                }

                var attr = new ObjectAttributes
                {
                    LayerIndex = layerIdx,
                    ObjectDecoration = cc.LineType == LineType.Hidden
                        ? ObjectDecoration.None : ObjectDecoration.None
                };

                // 隠れ線は破線パターン
                if (cc.LineType == LineType.Hidden)
                    attr.LinetypeSource = ObjectLinetypeSource.LinetypeFromObject;

                doc.Objects.AddCurve(c, attr);
            }

            doc.Views.Redraw();
            RhinoApp.WriteLine($"[Tanuki] {viewName}: {curves.Count}本を配置しました");
        }

        public static void DeleteViewLayers(RhinoDoc doc, string viewName)
        {
            int viewIdx = doc.Layers.FindByFullPath($"Tanuki::{viewName}", RhinoMath.UnsetIntIndex);
            if (viewIdx == RhinoMath.UnsetIntIndex) return;

            var objs = doc.Objects.FindByLayer(doc.Layers[viewIdx]);
            if (objs != null) foreach (var o in objs) doc.Objects.Delete(o, true);

            // 子レイヤーごと削除
            foreach (var child in doc.Layers[viewIdx].GetChildren() ?? new Layer[0])
            {
                var childObjs = doc.Objects.FindByLayer(child);
                if (childObjs != null) foreach (var o in childObjs) doc.Objects.Delete(o, true);
                doc.Layers.Delete(child.Index, true);
            }
            doc.Layers.Delete(viewIdx, true);
        }

        public static BoundingBox GetModelBBox(RhinoDoc doc)
        {
            var bbox = BoundingBox.Empty;
            foreach (var obj in doc.Objects)
            {
                if (obj.IsHidden || !obj.IsValid) continue;
                var layer = doc.Layers[obj.Attributes.LayerIndex];
                if (layer != null && layer.FullPath.StartsWith("Tanuki")) continue;
                bbox.Union(obj.Geometry.GetBoundingBox(true));
            }
            return bbox;
        }

        // ---- private ----

        private static int GetOrCreateLayer(RhinoDoc doc, string name, int parentIdx, Color color)
        {
            string fullPath = parentIdx < 0 ? name
                            : $"{doc.Layers[parentIdx].FullPath}::{name}";
            int idx = doc.Layers.FindByFullPath(fullPath, RhinoMath.UnsetIntIndex);
            if (idx != RhinoMath.UnsetIntIndex) return idx;

            var layer = new Layer { Name = name, Color = color };
            if (parentIdx >= 0) layer.ParentLayerId = doc.Layers[parentIdx].Id;
            return doc.Layers.Add(layer);
        }

        private static Color LineTypeColor(LineType lt) =>
            lt == LineType.Cut     ? Color.Red   :
            lt == LineType.Visible ? Color.Black  : Color.Gray;
    }
}
