using System;
using System.Drawing;
using Rhino;
using Rhino.Commands;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input.Custom;

namespace Tanuki.Commands
{
    /// <summary>
    /// アクティブなレイアウトにタイトルブロックを配置する。
    /// 用紙座標系（mm）で配置するため、RhinoPageView のページ空間に ObjectAttributes.Space = PageSpace を使用する。
    /// </summary>
    public class TanukiTitleBlock : Command
    {
        public static TanukiTitleBlock Instance { get; private set; }
        public override string EnglishName => "TanukiTitleBlock";
        public TanukiTitleBlock() { Instance = this; }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // 対象レイアウトを選択
            RhinoPageView pageView = null;

            if (doc.Views.ActiveView is RhinoPageView apv)
            {
                pageView = apv;
            }
            else
            {
                var pageViews = doc.Views.GetPageViews();
                if (pageViews == null || pageViews.Length == 0)
                {
                    RhinoApp.WriteLine("[Tanuki] レイアウトがありません。TanukiSheet でレイアウトを作成してください。");
                    return Result.Nothing;
                }

                var go = new GetOption();
                go.SetCommandPrompt("タイトルブロックを追加するレイアウトを選択");
                foreach (var pv in pageViews)
                    go.AddOption(pv.PageName.Replace(" ", "_").Replace("-", "_").Replace(".", "_"));
                go.Get();
                if (go.CommandResult() != Result.Success) return go.CommandResult();
                pageView = pageViews[go.Option().Index - 1];
                doc.Views.ActiveView = pageView;
            }

            // プロジェクト名
            var gs = new GetString();
            gs.SetCommandPrompt("プロジェクト名");
            gs.SetDefaultString("プロジェクト名");
            gs.Get();
            string projName = gs.StringResult();

            PlaceTitleBlock(doc, pageView, projName);
            return Result.Success;
        }

        private void PlaceTitleBlock(RhinoDoc doc, RhinoPageView pageView, string projName)
        {
            double paperW = pageView.PageWidth;
            double paperH = pageView.PageHeight;

            // タイトルブロックのサイズ・位置（右下マージン 5mm）
            double margin = 5;
            double tbW    = Math.Min(180, paperW - margin * 2);
            double tbH    = 55;
            double x0     = paperW - tbW - margin;
            double y0     = margin;

            // レイヤー
            int rootIdx = EnsureLayer(doc, "Tanuki", -1, Color.DimGray);
            int tbIdx   = EnsureLayer(doc, "TitleBlock", rootIdx, Color.Black);

            // ページ空間（レイアウト）に配置するための属性
            var attr = new ObjectAttributes
            {
                LayerIndex  = tbIdx,
                Space       = ActiveSpace.PageSpace,
                ViewportId  = pageView.MainViewport.Id,
            };

            // 既存タイトルブロックを削除
            var existing = doc.Objects.FindByLayer(doc.Layers[tbIdx]);
            if (existing != null)
                foreach (var o in existing)
                    if (o.Attributes.ViewportId == pageView.MainViewport.Id)
                        doc.Objects.Delete(o, true);

            // ── 外枠 ──
            doc.Objects.AddRectangle(
                new Rectangle3d(Plane.WorldXY, new Point3d(x0, y0, 0), new Point3d(x0 + tbW, y0 + tbH, 0)), attr);

            // ── 行区切り（3行: 上/中/下） ──
            double rowH   = tbH / 3.0;
            double row1Y  = y0 + rowH;
            double row2Y  = y0 + rowH * 2;
            HLine(doc, x0, x0 + tbW, row1Y, attr);
            HLine(doc, x0, x0 + tbW, row2Y, attr);

            // ── 列区切り ──
            //  col A: 70mm  (プロジェクト名 / 図面名 / 作図者欄)
            //  col B: 30mm  (図面番号 / 作図 / 確認)
            //  col C: 40mm  (縮尺 / 承認)
            //  col D: 残り  (日付 / 備考)
            double cA = 70; double cB = 30; double cC = 40;
            double cD = tbW - cA - cB - cC;
            double xA = x0 + cA; double xB = xA + cB; double xC = xB + cC;
            VLine(doc, xA, y0, y0 + tbH, attr);
            VLine(doc, xB, y0, y0 + tbH, attr);
            VLine(doc, xC, y0, y0 + tbH, attr);

            // 中行の追加列区切り（col A を左右に分ける）
            double xA2 = x0 + cA / 2;
            VLine(doc, xA2, y0, row1Y, attr);

            double small = 1.8;  // 項目ラベルの文字高さ (mm)
            double large = 3.5;  // 値の文字高さ (mm)

            // ── 上行: プロジェクト / 番号 / 縮尺 / 日付 ──
            Cell(doc, "プロジェクト", projName,      x0,  row2Y, cA,  rowH, small, large, attr);
            Cell(doc, "図面番号",     "001",         xA,  row2Y, cB,  rowH, small, large, attr);
            Cell(doc, "縮尺",         "1:100",       xB,  row2Y, cC,  rowH, small, large, attr);
            Cell(doc, "日付",         DateTime.Now.ToString("yyyy-MM-dd"), xC, row2Y, cD, rowH, small, large, attr);

            // ── 中行: 図面名 / 作図者 / 確認者 / 承認者 ──
            Cell(doc, "図面名", "図面名",   x0,  row1Y, cA, rowH, small, large, attr);
            Cell(doc, "作図",   "",         xA,  row1Y, cB, rowH, small, large, attr);
            Cell(doc, "確認",   "",         xB,  row1Y, cC, rowH, small, large, attr);
            Cell(doc, "承認",   "",         xC,  row1Y, cD, rowH, small, large, attr);

            // ── 下行: 会社名 / 担当者補足 ──
            Cell(doc, "作成者", "", x0,   y0, cA * 1.5, rowH, small, large, attr);
            Cell(doc, "備考",   "", xA2,  y0, tbW - cA * 1.5, rowH, small, large, attr);

            doc.Views.Redraw();
            RhinoApp.WriteLine($"[Tanuki] タイトルブロックをレイアウト '{pageView.PageName}' に配置しました。");
        }

        private static void HLine(RhinoDoc doc, double x1, double x2, double y, ObjectAttributes attr)
        {
            doc.Objects.AddLine(new Line(new Point3d(x1, y, 0), new Point3d(x2, y, 0)), attr);
        }

        private static void VLine(RhinoDoc doc, double x, double y1, double y2, ObjectAttributes attr)
        {
            doc.Objects.AddLine(new Line(new Point3d(x, y1, 0), new Point3d(x, y2, 0)), attr);
        }

        private static void Cell(
            RhinoDoc doc,
            string label, string value,
            double cellX, double cellY, double cellW, double cellH,
            double labelTH, double valueTH,
            ObjectAttributes attr)
        {
            double p = 1.0;

            // ラベル（左上）
            var lbl = new TextEntity { PlainText = label, TextHeight = labelTH, Justification = TextJustification.TopLeft };
            lbl.Plane = new Plane(new Point3d(cellX + p, cellY + cellH - p, 0), Vector3d.ZAxis);
            doc.Objects.Add(lbl, attr);

            // 値（中央）
            if (!string.IsNullOrEmpty(value))
            {
                var val = new TextEntity { PlainText = value, TextHeight = valueTH, Justification = TextJustification.MiddleCenter };
                val.Plane = new Plane(new Point3d(cellX + cellW / 2, cellY + cellH / 2, 0), Vector3d.ZAxis);
                doc.Objects.Add(val, attr);
            }
        }

        private static int EnsureLayer(RhinoDoc doc, string name, int parentIdx, Color color)
        {
            return Tanuki.Generators.LayerUtil.GetOrCreate(doc, name, parentIdx, color);
        }
    }
}
