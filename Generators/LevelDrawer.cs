using System.Collections.Generic;
using System.Drawing;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Tanuki.Data;

namespace Tanuki.Generators
{
    /// <summary>
    /// レベル（階高）をRhinoのモデル空間に可視化する。
    /// 各レベル高さに水平フレームを描画し、左端にラベルを配置する。
    /// </summary>
    public static class LevelDrawer
    {
        private const string RootPath   = "Tanuki";
        private const string LevelPath  = "Tanuki::レベル";
        private const string LinePath   = "Tanuki::レベル::線";
        private const string LabelPath  = "Tanuki::レベル::記号";

        private static readonly Color LevelColor = Color.FromArgb(0, 160, 128);

        public static void SyncAll(RhinoDoc doc, List<Level> levels)
        {
            EnsureLayers(doc);
            var bbox = DrawingPlacer.GetModelBBox(doc);

            int lineIdx  = GetLayer(doc, LinePath);
            int labelIdx = GetLayer(doc, LabelPath);

            // 既存を削除
            ClearLayer(doc, lineIdx);
            ClearLayer(doc, labelIdx);

            if (!bbox.IsValid || levels.Count == 0)
            {
                doc.Views.Redraw();
                return;
            }

            var lineAttr  = new ObjectAttributes { LayerIndex = lineIdx };
            var labelAttr = new ObjectAttributes { LayerIndex = labelIdx };

            double margin = 2000;
            double x0 = bbox.Min.X - margin;
            double x1 = bbox.Max.X + margin;
            double y0 = bbox.Min.Y - margin;
            double y1 = bbox.Max.Y + margin;

            // 左側の縦軸線（各レベルをつなぐ）
            double zMin = double.MaxValue, zMax = double.MinValue;
            foreach (var l in levels)
            {
                if (l.Elevation < zMin) zMin = l.Elevation;
                if (l.Elevation > zMax) zMax = l.Elevation;
            }

            double xSpine = x0 - 1000;
            if (levels.Count > 1)
                doc.Objects.AddLine(
                    new Line(new Point3d(xSpine, y0, zMin - 500),
                             new Point3d(xSpine, y0, zMax + 500)),
                    lineAttr);

            foreach (var level in levels)
            {
                double z = level.Elevation;

                // 水平フレーム矩形（建物断面がどのレベルにあるか視覚化）
                var corners = new Point3d[]
                {
                    new Point3d(x0, y0, z),
                    new Point3d(x1, y0, z),
                    new Point3d(x1, y1, z),
                    new Point3d(x0, y1, z),
                    new Point3d(x0, y0, z)
                };
                var polyline = new Polyline(corners);
                doc.Objects.AddPolyline(polyline, lineAttr);

                // 左端のティックと縦軸接続線
                doc.Objects.AddLine(
                    new Line(new Point3d(xSpine, y0, z),
                             new Point3d(x0, y0, z)),
                    lineAttr);

                // ラベル
                var te = new TextEntity
                {
                    PlainText     = $"{level.Name}  +{level.Elevation:F0}",
                    TextHeight    = 200,
                    Justification = TextJustification.MiddleRight
                };
                te.Plane = new Plane(
                    new Point3d(xSpine - 200, y0, z),
                    Vector3d.XAxis, Vector3d.ZAxis);
                doc.Objects.Add(te, labelAttr);
            }

            doc.Views.Redraw();
        }

        private static void ClearLayer(RhinoDoc doc, int layerIdx)
        {
            var objs = doc.Objects.FindByLayer(doc.Layers[layerIdx]);
            if (objs != null)
                foreach (var o in objs) doc.Objects.Delete(o, true);
        }

        private static void EnsureLayers(RhinoDoc doc)
        {
            GetOrCreateLayer(doc, RootPath,  null,       Color.DimGray);
            GetOrCreateLayer(doc, LevelPath, RootPath,   LevelColor);
            GetOrCreateLayer(doc, LinePath,  LevelPath,  LevelColor);
            GetOrCreateLayer(doc, LabelPath, LevelPath,  LevelColor);
        }

        private static int GetLayer(RhinoDoc doc, string path)
        {
            int idx = doc.Layers.FindByFullPath(path, RhinoMath.UnsetIntIndex);
            if (idx == RhinoMath.UnsetIntIndex) { EnsureLayers(doc); idx = doc.Layers.FindByFullPath(path, RhinoMath.UnsetIntIndex); }
            return idx;
        }

        private static int GetOrCreateLayer(RhinoDoc doc, string fullPath, string parentPath, Color color)
        {
            int parentIdx = parentPath != null
                ? doc.Layers.FindByFullPath(parentPath, RhinoMath.UnsetIntIndex)
                : -1;
            string name = fullPath.Contains("::") ? fullPath.Substring(fullPath.LastIndexOf("::") + 2) : fullPath;
            return LayerUtil.GetOrCreate(doc, name, parentIdx, color);
        }
    }
}
