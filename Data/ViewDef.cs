using System;
using Rhino.Geometry;

namespace Tanuki.Data
{
    public enum ViewType { FloorPlan, RCP, Elevation, Section }

    public class ViewDef
    {
        public Guid   Id           { get; set; } = Guid.NewGuid();
        public string Name         { get; set; } = "";
        public ViewType Type       { get; set; }

        // マーカーオブジェクトのID（モデル上の線）
        public Guid MarkerObjectId { get; set; } = Guid.Empty;

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

        // 生成済み図面の配置オフセット
        public double PlacedOffsetX { get; set; }
        public double PlacedOffsetY { get; set; }

        // ---- ヘルパー ----

        public Point3d CutStart => new Point3d(CutStartX, CutStartY, 0);
        public Point3d CutEnd   => new Point3d(CutEndX,   CutEndY,   0);

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
