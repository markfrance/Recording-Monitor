using System.Drawing;

namespace System
{
    partial class MSharpExtensions
    {
        /// <summary>
        /// Darkens the specified color.
        /// </summary>
        /// <param name="color">The color.</param>
        /// <param name="pc">The pc.</param>
        public static Color Darker(this Color color, double pc = 0.2)
        {
            pc = Math.Max(1d - pc, 0d);
            var rD = Convert.ToByte(Math.Max(Math.Round(pc * color.R), 0d));
            var gD = Convert.ToByte(Math.Max(Math.Round(pc * color.G), 0d));
            var bD = Convert.ToByte(Math.Max(Math.Round(pc * color.B), 0d));
            var darker = Color.FromArgb(color.A, rD, gD, bD);
            return darker;
        }

        /// <summary>
        /// Lightens the specified color.
        /// </summary>
        /// <param name="color">The color.</param>
        /// <param name="pc">The pc.</param>
        public static Color Lighter(this Color color, double pc = 0.2)
        {
            pc = Math.Max(1d + pc, 0d);
            var rL = Convert.ToByte(Math.Min(Math.Round(pc * color.R), 255d));
            var gL = Convert.ToByte(Math.Min(Math.Round(pc * color.G), 255d));
            var bL = Convert.ToByte(Math.Min(Math.Round(pc * color.B), 255d));
            var darker = Color.FromArgb(color.A, rL, gL, bL);
            return darker;
        }
    }
}
