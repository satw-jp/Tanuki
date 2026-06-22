using Rhino.Geometry;

namespace Tanuki.Data
{
    public class GridLine
    {
        public string Name      { get; set; } = "";   // "A", "B", "1", "2" etc.
        public double OriginX   { get; set; }
        public double OriginY   { get; set; }
        public double DirectionX { get; set; } = 1;  // 方向ベクトル (単位ベクトル)
        public double DirectionY { get; set; } = 0;
        public double Length    { get; set; } = 20000; // mm

        public Line ToLine()
        {
            var origin = new Point3d(OriginX, OriginY, 0);
            var dir = new Vector3d(DirectionX, DirectionY, 0);
            return new Line(origin, dir, Length);
        }
    }
}
