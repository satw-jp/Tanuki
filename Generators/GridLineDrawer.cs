using System.Collections.Generic;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Tanuki.Data;

namespace Tanuki.Generators
{
    /// <summary>
    /// 通り芯をRhinoのモデル空間に可視化する。
    /// 「線」レイヤーはオブジェクトIDを保持して移動追従。
    /// 「記号」レイヤーはバブル＋テキストで都度再生成。
    /// </summary>
    public static class GridLineDrawer
    {
        private const string RootPath    = "Tanuki";
        private const string GridPath    = "Tanuki::通り芯";
        private const string LinePath    = "Tanuki::通り芯::線";
        private const string SymbolPath  = "Tanuki::通り芯::記号";

        private const double BubbleR    = 400;
        private const double TextHeight = 360;

        // ── 全同期（初回・パネルから追加時） ─────────────────────────────

        public static void SyncAll(RhinoDoc doc, List<GridLine> gridLines)
        {
            EnsureLayers(doc);
            SyncLines(doc, gridLines);   // 線を描いてIDを保存
            SyncSymbols(doc, gridLines); // バブル＋テキスト
        }

        // ── 線のみ同期（IDを GridLine.LineObjectId に保存） ─────────────

        public static void SyncLines(RhinoDoc doc, List<GridLine> gridLines)
        {
            int lineIdx = GetLayer(doc, LinePath);

            // 既存線をすべて削除（IDが残らないよう）
            var existing = doc.Objects.FindByLayer(doc.Layers[lineIdx]);
            if (existing != null)
                foreach (var o in existing) doc.Objects.Delete(o, true);

            var attr = new ObjectAttributes
            {
                LayerIndex  = lineIdx,
                ColorSource = ObjectColorSource.ColorFromLayer
            };

            foreach (var gl in gridLines)
            {
                var id = doc.Objects.AddLine(gl.ToLine(), attr);
                gl.LineObjectId = id;
            }
        }

        // ── シンボルのみ再生成（線は触らない） ───────────────────────────

        public static void SyncSymbols(RhinoDoc doc, List<GridLine> gridLines)
        {
            int symIdx = GetLayer(doc, SymbolPath);

            // 既存シンボルを削除
            var existing = doc.Objects.FindByLayer(doc.Layers[symIdx]);
            if (existing != null)
                foreach (var o in existing) doc.Objects.Delete(o, true);

            var attr = new ObjectAttributes { LayerIndex = symIdx };

            foreach (var gl in gridLines)
            {
                var line = gl.ToLine();
                foreach (var pt in new[] { line.From, line.To })
                {
                    var plane  = new Plane(pt, Vector3d.ZAxis);
                    doc.Objects.AddCircle(new Circle(plane, BubbleR), attr);

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

        // ── 移動追従：特定グリッド線のデータを新ジオメトリから更新 ────────

        public static bool TryUpdateFromObject(
            RhinoDoc doc,
            System.Guid oldId,
            System.Guid newId,
            Curve newCurve,
            List<GridLine> gridLines)
        {
            foreach (var gl in gridLines)
            {
                if (gl.LineObjectId != oldId) continue;

                var start = newCurve.PointAtStart;
                var end   = newCurve.PointAtEnd;
                var dir   = end - start;
                dir.Unitize();

                gl.OriginX     = start.X;
                gl.OriginY     = start.Y;
                gl.DirectionX  = dir.X;
                gl.DirectionY  = dir.Y;
                gl.Length      = start.DistanceTo(end);
                gl.LineObjectId = newId;
                return true;
            }
            return false;
        }

        // ── レイヤー管理 ─────────────────────────────────────────────────

        private static void EnsureLayers(RhinoDoc doc)
        {
            GetOrCreateLayer(doc, RootPath,   null,     System.Drawing.Color.DimGray);
            GetOrCreateLayer(doc, GridPath,   RootPath, System.Drawing.Color.FromArgb(0, 128, 255));
            GetOrCreateLayer(doc, LinePath,   GridPath, System.Drawing.Color.FromArgb(0, 128, 255));
            GetOrCreateLayer(doc, SymbolPath, GridPath, System.Drawing.Color.FromArgb(0, 128, 255));
        }

        private static int GetLayer(RhinoDoc doc, string path)
        {
            int idx = doc.Layers.FindByFullPath(path, RhinoMath.UnsetIntIndex);
            if (idx == RhinoMath.UnsetIntIndex)
            {
                EnsureLayers(doc);
                idx = doc.Layers.FindByFullPath(path, RhinoMath.UnsetIntIndex);
            }
            return idx;
        }

        private static int GetOrCreateLayer(
            RhinoDoc doc, string fullPath, string parentFullPath, System.Drawing.Color color)
        {
            int existing = doc.Layers.FindByFullPath(fullPath, RhinoMath.UnsetIntIndex);
            if (existing != RhinoMath.UnsetIntIndex) return existing;

            string name = fullPath.Contains("::") ? fullPath.Substring(fullPath.LastIndexOf("::") + 2) : fullPath;
            var layer = new Layer { Name = name, Color = color };

            if (parentFullPath != null)
            {
                int parentIdx = doc.Layers.FindByFullPath(parentFullPath, RhinoMath.UnsetIntIndex);
                if (parentIdx != RhinoMath.UnsetIntIndex)
                    layer.ParentLayerId = doc.Layers[parentIdx].Id;
            }
            return doc.Layers.Add(layer);
        }
    }
}
