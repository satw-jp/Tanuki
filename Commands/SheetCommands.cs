using System;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.Commands;
using Rhino.Display;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using Tanuki.Data;
using Tanuki.Generators;

namespace Tanuki.Commands
{
    // ══════════════════════════════════════════════════════════
    // TanukiSheet — モデル空間の四角形をLayoutのViewportに変換
    // ══════════════════════════════════════════════════════════
    public class TanukiSheet : Command
    {
        public static TanukiSheet Instance { get; private set; }
        public override string EnglishName => "TanukiSheet";
        public TanukiSheet() { Instance = this; }

        // 標準用紙サイズ (mm)
        private static readonly Dictionary<string, (double W, double H)> Papers = new Dictionary<string, (double, double)>
        {
            { "A4",  (297,  210) },
            { "A3",  (420,  297) },
            { "A2",  (594,  420) },
            { "A1",  (841,  594) },
            { "A0",  (1189, 841) },
        };

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var project = TanukiProject.Load(doc);

            // ── 用紙サイズ選択 ──
            var goPaper = new GetOption();
            goPaper.SetCommandPrompt("用紙サイズを選択");
            foreach (var k in Papers.Keys) goPaper.AddOption(k);
            goPaper.Get();
            if (goPaper.CommandResult() != Result.Success) return goPaper.CommandResult();
            string paperKey = Papers.Keys.ElementAt(goPaper.Option().Index - 1);
            var paper = Papers[paperKey];

            // ── シート名 ──
            var gs = new GetString();
            gs.SetCommandPrompt("シート名");
            gs.SetDefaultString("Sheet 1");
            gs.Get();
            string sheetName = gs.StringResult();

            // ── モデル空間で四角形を選択（ビューポートの配置を定義） ──
            RhinoApp.WriteLine("シートのビューポートになる四角形を選択してください（Enter で確定）");
            var go = new GetObject();
            go.SetCommandPrompt("四角形を選択（ビューポート定義）");
            go.GeometryFilter = Rhino.DocObjects.ObjectType.Curve;
            go.EnablePreSelect(false, true);
            go.GetMultiple(0, 0);

            var rects = new List<(Rectangle3d Rect, string ViewName)>();

            if (go.CommandResult() == Result.Success && go.ObjectCount > 0)
            {
                // 選択した四角形とViewDefを紐付け
                foreach (var objRef in go.Objects())
                {
                    var curve = objRef.Curve();
                    if (curve == null) continue;

                    // 四角形かチェック
                    if (!curve.TryGetPolyline(out var pl) || pl.Count != 5) continue;
                    var bbox = curve.GetBoundingBox(true);
                    var rect = new Rectangle3d(Plane.WorldXY,
                        new Point3d(bbox.Min.X, bbox.Min.Y, 0),
                        new Point3d(bbox.Max.X, bbox.Max.Y, 0));

                    // 対応するViewを選択
                    var gv = new GetOption();
                    gv.SetCommandPrompt($"この四角形に対応する図面を選択");
                    gv.AddOption("自動_スキップ");
                    foreach (var v in project.Views) gv.AddOption(v.Name.Replace("-","_").Replace(" ","_"));
                    gv.Get();

                    if (gv.CommandResult() != Result.Success) continue;
                    int vi = gv.Option().Index - 2; // 0-based view index
                    if (vi >= 0 && vi < project.Views.Count)
                        rects.Add((rect, project.Views[vi].Name));
                }
            }

            // 四角形がない場合は全Viewを自動配置
            if (rects.Count == 0 && project.Views.Count > 0)
            {
                RhinoApp.WriteLine("四角形が選択されていないため、全図面を自動配置します");
                rects = AutoLayout(project.Views, paper);
            }

            if (rects.Count == 0) { RhinoApp.WriteLine("配置する図面がありません"); return Result.Nothing; }

            // ── Layout 作成 ──
            CreateLayout(doc, project, sheetName, paper, rects);
            return Result.Success;
        }

        private void CreateLayout(
            RhinoDoc doc,
            TanukiProject project,
            string name,
            (double W, double H) paper,
            List<(Rectangle3d Rect, string ViewName)> rects)
        {
            // 既存同名レイアウトを削除
            foreach (var pv in doc.Views.GetPageViews())
            {
                if (pv.PageName == name)
                {
                    pv.Close();
                    break;
                }
            }

            var pageView = doc.Views.AddPageView(name, paper.W, paper.H);
            if (pageView == null) { RhinoApp.WriteLine("レイアウトの作成に失敗しました"); return; }

            foreach (var (rect, viewName) in rects)
            {
                var view = project.Views.FirstOrDefault(v => v.Name == viewName);
                if (view == null) continue;

                var lo = new Point2d(rect.X.Min, rect.Y.Min);
                var hi = new Point2d(rect.X.Max, rect.Y.Max);

                var detail = pageView.AddDetailView(
                    view.Name,
                    lo, hi,
                    DefinedViewportProjection.Top);

                if (detail == null) continue;

                // 生成済み図面の範囲にカメラをフィット（LayerKey でレイヤーを特定）
                var drawBbox = GetDrawingBbox(doc, view.GetLayerKey());
                if (drawBbox.IsValid)
                {
                    detail.Viewport.SetCameraLocation(
                        new Point3d(drawBbox.Center.X, drawBbox.Center.Y, 1000),
                        false);
                    detail.Viewport.SetCameraDirection(new Vector3d(0, 0, -1), false);
                    detail.Viewport.ChangeToParallelProjection(true);

                    // スケール設定
                    double detailW = hi.X - lo.X;
                    double detailH = hi.Y - lo.Y;
                    double scaleX = drawBbox.Diagonal.X > 0 ? drawBbox.Diagonal.X / detailW : 1;
                    double scaleY = drawBbox.Diagonal.Y > 0 ? drawBbox.Diagonal.Y / detailH : 1;
                    double scale  = Math.Max(scaleX, scaleY);
                    detail.DetailGeometry.SetScale(1, doc.ModelUnitSystem, scale, Rhino.UnitSystem.Millimeters);
                    detail.CommitChanges();
                }
            }

            doc.Views.Redraw();
            RhinoApp.WriteLine($"レイアウト '{name}' に {rects.Count} ビューポートを作成しました");
            pageView.SetPageAsActive();
        }

        private BoundingBox GetDrawingBbox(RhinoDoc doc, string layerKey)
        {
            var bbox = BoundingBox.Empty;
            string layerPath = $"Tanuki::{layerKey}";
            int li = doc.Layers.FindByFullPath(layerPath, RhinoMath.UnsetIntIndex);
            if (li == RhinoMath.UnsetIntIndex) return bbox;
            Tanuki.Generators.LayerUtil.ForEachObject(doc, li, o => bbox.Union(o.Geometry.GetBoundingBox(true)));
            return bbox;
        }

        private List<(Rectangle3d, string)> AutoLayout(List<ViewDef> views, (double W, double H) paper)
        {
            var result = new List<(Rectangle3d, string)>();
            double margin = 10;
            double colW = (paper.W - margin * 3) / 2;
            double rowH = (paper.H - margin * 3) / 2;

            for (int i = 0; i < views.Count; i++)
            {
                double col = i % 2;
                double row = i / 2;
                var lo = new Point3d(margin + col * (colW + margin), margin + row * (rowH + margin), 0);
                var hi = new Point3d(lo.X + colW, lo.Y + rowH, 0);
                var rect = new Rectangle3d(Plane.WorldXY, lo, hi);
                result.Add((rect, views[i].Name));
            }
            return result;
        }
    }

    // ══════════════════════════════════════════════════════════
    // TanukiPrint — 印刷範囲を可視化する
    // ══════════════════════════════════════════════════════════
    public class TanukiPrint : Command
    {
        public static TanukiPrint Instance { get; private set; }
        public override string EnglishName => "TanukiPrint";
        public TanukiPrint() { Instance = this; }

        private static readonly Dictionary<string, (double W, double H)> Papers = new Dictionary<string, (double, double)>
        {
            { "A4",  (297,  210) },
            { "A3",  (420,  297) },
            { "A2",  (594,  420) },
            { "A1",  (841,  594) },
            { "A0",  (1189, 841) },
        };

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // ── 設定入力 ──
            var goPaper = new GetOption();
            goPaper.SetCommandPrompt("用紙サイズ");
            foreach (var k in Papers.Keys) goPaper.AddOption(k);
            goPaper.Get();
            if (goPaper.CommandResult() != Result.Success) return goPaper.CommandResult();
            var paper = Papers[Papers.Keys.ElementAt(goPaper.Option().Index - 1)];

            var goOrient = new GetOption();
            goOrient.SetCommandPrompt("向き");
            goOrient.AddOption("横");
            goOrient.AddOption("縦");
            goOrient.Get();
            bool landscape = goOrient.Option().Index == 1;
            double pw = landscape ? Math.Max(paper.W, paper.H) : Math.Min(paper.W, paper.H);
            double ph = landscape ? Math.Min(paper.W, paper.H) : Math.Max(paper.W, paper.H);

            var ghScale = new GetNumber();
            ghScale.SetCommandPrompt("縮尺の分母 (例: 100 → 1:100)");
            ghScale.SetDefaultNumber(100);
            ghScale.Get();
            if (ghScale.CommandResult() != Result.Success) return ghScale.CommandResult();
            double scale = ghScale.Number();

            // モデル上での1枚分のサイズ
            double pageW = pw * scale;
            double pageH = ph * scale;

            // ── 枚数指定 ──
            var gCols = new GetInteger();
            gCols.SetCommandPrompt("横方向の枚数");
            gCols.SetDefaultInteger(2);
            gCols.Get();
            int cols = gCols.CommandResult() == Result.Success ? gCols.Number() : 2;

            var gRows = new GetInteger();
            gRows.SetCommandPrompt("縦方向の枚数");
            gRows.SetDefaultInteger(2);
            gRows.Get();
            int rows = gRows.CommandResult() == Result.Success ? gRows.Number() : 2;

            // ── 基準点指定 ──
            var gp = new GetPoint();
            gp.SetCommandPrompt("印刷範囲の左下コーナーを指定");
            gp.Get();
            if (gp.CommandResult() != Result.Success) return gp.CommandResult();
            var origin = gp.Point();

            // ── 印刷枠を描画 ──
            PlacePrintFrames(doc, origin, pageW, pageH, cols, rows, scale, pw, ph);
            return Result.Success;
        }

        private void PlacePrintFrames(
            RhinoDoc doc,
            Point3d origin,
            double pageW, double pageH,
            int cols, int rows,
            double scale, double pw, double ph)
        {
            // 専用レイヤー
            int rootIdx = GetOrCreate(doc, "Tanuki", -1, System.Drawing.Color.DimGray);
            int printIdx = GetOrCreate(doc, "PrintFrames", rootIdx, System.Drawing.Color.Blue);

            // 既存の印刷枠を削除
            var existing = doc.Objects.FindByLayer(doc.Layers[printIdx]);
            if (existing != null) foreach (var o in existing) doc.Objects.Delete(o, true);

            var attr = new Rhino.DocObjects.ObjectAttributes { LayerIndex = printIdx };

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    double x = origin.X + c * pageW;
                    double y = origin.Y + r * pageH;

                    // 外枠
                    var rect = new Rectangle3d(Plane.WorldXY,
                        new Point3d(x, y, 0),
                        new Point3d(x + pageW, y + pageH, 0));
                    doc.Objects.AddRectangle(rect, attr);

                    // ページ番号ラベル
                    int pageNum = r * cols + c + 1;
                    var te = new TextEntity
                    {
                        PlainText  = $"P{pageNum}\n1:{(int)scale} / {pw:F0}×{ph:F0}mm",
                        TextHeight = pageH * 0.05,
                        Justification = TextJustification.BottomLeft
                    };
                    te.Plane = new Plane(new Point3d(x + pageW * 0.02, y + pageH * 0.02, 0), Vector3d.ZAxis);
                    doc.Objects.Add(te, attr);
                }
            }

            // 全体枠
            var totalRect = new Rectangle3d(Plane.WorldXY,
                origin,
                new Point3d(origin.X + cols * pageW, origin.Y + rows * pageH, 0));
            var totalAttr = new Rhino.DocObjects.ObjectAttributes
            {
                LayerIndex = printIdx,
                ColorSource = Rhino.DocObjects.ObjectColorSource.ColorFromObject,
                ObjectColor = System.Drawing.Color.Red
            };
            doc.Objects.AddRectangle(totalRect, totalAttr);

            doc.Views.Redraw();
            RhinoApp.WriteLine($"印刷枠を配置しました: {cols}×{rows}枚  1:{(int)scale}  用紙{pw:F0}×{ph:F0}mm");
            RhinoApp.WriteLine($"  1枚のモデル範囲: {pageW:F0} × {pageH:F0} mm");
        }

        private int GetOrCreate(RhinoDoc doc, string name, int parentIdx, System.Drawing.Color color)
        {
            string path = parentIdx < 0 ? name : $"{doc.Layers[parentIdx].FullPath}::{name}";
            int idx = doc.Layers.FindByFullPath(path, RhinoMath.UnsetIntIndex);
            if (idx != RhinoMath.UnsetIntIndex) return idx;
            var layer = new Rhino.DocObjects.Layer { Name = name, Color = color };
            if (parentIdx >= 0) layer.ParentLayerId = doc.Layers[parentIdx].Id;
            return doc.Layers.Add(layer);
        }
    }
}
