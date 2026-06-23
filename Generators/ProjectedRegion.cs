using System.Drawing;
using Rhino.Geometry;

namespace Tanuki.Generators
{
    public class ProjectedRegion
    {
        public Brep   FlatBrep  { get; set; }
        public Color  FillColor { get; set; }
        public double Depth     { get; set; }
    }
}
