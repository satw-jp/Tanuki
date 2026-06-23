using System;
using System.Collections.Generic;
using Rhino.Geometry;

namespace Tanuki.Data
{
    public enum ViewType { FloorPlan, RCP, Elevation, Section }

    public enum ViewDisplayMode   { Technical, Presentation }
    public enum PresentationStyle { SolidColor, MaterialColor, Texture }

    public class ViewDef
    {
        public Guid   Id           { get; set; } = Guid.NewGuid();
        public string Name         { get; set; } = "";
        public ViewType Type       { get; set; }

        // マーカーオブジェクトのID（モデル上の線）
        public Guid MarkerObjectId { get; set; } = Guid.Empty;

        // 視線方向インジケーター（ティック・矢印・ラベル）のID群
        public List<Guid> MarkerIndicatorIds { get; set; } = new List<Guid>();

        // 平面図 / 天井伏図
        public string LevelName    { get; set; } = "";
        public double CutHeight    { get; set; } = 1000;

        // 立面図・断面図：任意方向の切断平面
        // 切断線の始点・終点（XY平面上）
        public double CutStartX    { get; set; }
        public double CutStartY    { get; set; }
        public double CutEndX      { get; set; }
        public double CutEndY      { get; set; }
        // 視線方向（切断線の右手側 = 見る方向）
        public bool   ViewRight    { get; set; } = true;

        // レイヤー名の安定キー（作成時に設定。view.Name を変えてもこちらは不変）
        // 空の場合は Name.Replace("::", "_") にフォールバック（旧データ互換）
        public string LayerKey { get; set; } = "";

        // 生成済み図面の配置オフセット
        public double PlacedOffsetX  { get; set; }
        public double PlacedOffsetY  { get; set; }
        public bool   HasPlacement   { get; set; } = false;

        // Presentation Mode
        public ViewDisplayMode   DisplayMode       { get; set; } = ViewDisplayMode.Technical;
        public PresentationStyle PresentationStyle { get; set; } = PresentationStyle.SolidColor;

        // 断面/立面の処理対象制御（パフォーマンス）
        public bool   IncludeMeshes { get; set; } = true;
        public double ViewDepth     { get; set; } = 0;

        // ビュー固有の描画設定
        public LayerMode LayerMode       { get; set; } = LayerMode.LineType;
        public double    LabelTextHeight { get; set; } = 500;

        // ---- ヘルパー ----

        public Point3d CutStart => new Point3d(CutStartX, CutStartY, 0);
        public Point3d CutEnd   => new Point3d(CutEndX,   CutEndY,   0);

        /// <summary>レイヤーパスに使う安定キー。LayerKeyが空なら Name を代用する。</summary>
        public string GetLayerKey() => string.IsNullOrEmpty(LayerKey) ? Name.Replace("::", "_") : LayerKey;

        /// <summary>切断平面を返す（断面図・立面図用）</summary>
        public Plane GetCutPlane()
        {
            var dir = CutEnd - CutStart;
            dir.Z = 0;
            dir.Unitize();
            // 法線 = 切断線に垂直（右手系）、ViewRightで向きを決める
            var normal = Vector3d.CrossProduct(dir, Vector3d.ZAxis);
            if (!ViewRight) normal = -normal;
            return new Plane(CutStart, normal);
        }

        /// <summary>投影方向ベクトルを返す（立面図・断面図の見る方向）</summary>
        public Vector3d GetViewDirection()
        {
            var cutPlane = GetCutPlane();
            return -cutPlane.Normal; // 法線の逆が視線方向
        }
    }
}
