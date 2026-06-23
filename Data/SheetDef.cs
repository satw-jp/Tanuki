using System;
using System.Collections.Generic;

namespace Tanuki.Data
{
    /// <summary>
    /// モデル空間上に配置される図枠シート（C-1 Sheet システム用データ構造）。
    /// UI/コマンドは未実装。データ定義のみ。
    /// </summary>
    public class SheetDef
    {
        public Guid   Id             { get; set; } = Guid.NewGuid();
        public string Name           { get; set; } = "";
        // "A0","A1","A2","A3","A4","Custom"
        public string PaperSize      { get; set; } = "A3";
        public double PaperW         { get; set; } = 420;
        public double PaperH         { get; set; } = 297;
        // モデル空間上の図枠原点
        public double OffsetX        { get; set; }
        public double OffsetY        { get; set; }
        // TitleBlockCommand が扱うタイトルブロック識別子
        public string TitleBlockKey  { get; set; } = "";
        public List<SheetView> PlacedViews { get; set; } = new List<SheetView>();
    }

    /// <summary>シート上に配置された図面ビューの参照と位置。</summary>
    public class SheetView
    {
        // TanukiProject.Views[].Name と紐付け
        public string ViewDefName { get; set; } = "";
        // 図枠内ローカル座標 (mm)
        public double LocalX      { get; set; }
        public double LocalY      { get; set; }
        // 上書き縮尺（0 = プロジェクト既定 ViewScale を使用）
        public double Scale       { get; set; } = 0;
    }
}
