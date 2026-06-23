using System;
using Rhino.Geometry;

namespace Tanuki.Data
{
    public class GridLine
    {
        public string Name       { get; set; } = "";
        public double OriginX    { get; set; }
        public double OriginY    { get; set; }
        public double DirectionX { get; set; } = 1;
        public double DirectionY { get; set; } = 0;
        public double Length     { get; set; } = 20000;

        // 不変のデータID（undo/redo 後もRhinoオブジェクトと紐付けるための安定キー）
        public Guid PersistentId { get; set; } = Guid.NewGuid();
        // Rhinoオブジェクト追跡用（ReplaceRhinoObject で更新される）
        public Guid LineObjectId { get; set; } = Guid.Empty;

        public Line ToLine()
        {
            var origin = new Point3d(OriginX, OriginY, 0);
            var dir = new Vector3d(DirectionX, DirectionY, 0);
            return new Line(origin, dir, Length);
        }
    }
}
