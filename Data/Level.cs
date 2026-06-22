namespace Tanuki.Data
{
    public class Level
    {
        public string Name      { get; set; } = "1FL"; // "GL", "1FL", "2FL", "RF" etc.
        public double Elevation { get; set; } = 0;     // mm (Z高さ)
    }
}
