using System;
using System.Drawing;
using Rhino.Geometry;

namespace Tanuki.Generators
{
    public class ProjectedRegion
    {
        public Brep   FlatBrep  { get; set; }
        public Color  FillColor { get; set; }
        public double Depth     { get; set; }

        // ── Phase 2 拡張ポイント: Drawing → Model 逆引きメタデータ ──────────
        // 現在は Guid.Empty / "" のまま。SurfaceClassifier で面を生成する際に
        // 元オブジェクト情報を書き込むことで Drawing→Model 選択同期・プロパティ同期が実現できる。
        // DrawingPlacer.PlacePresentation() は SourceObjectId != Guid.Empty のときのみ
        // ObjectAttributes.SetUserString() を呼ぶようにすれば後方互換性ゼロのコスト。
        public Guid   SourceObjectId  { get; set; } = Guid.Empty;
        public string SourceLayer     { get; set; } = "";
        public string SourceMaterial  { get; set; } = "";
        public Guid   SourceViewId    { get; set; } = Guid.Empty;
    }
}
