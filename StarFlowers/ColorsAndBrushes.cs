using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace StarFlowers
{
    class ColorsAndBrushes
    {
        public static Brush getBrushFromColorRadialGradient(Color color)
        {
            RadialGradientBrush b = new RadialGradientBrush();
            b.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(0xFF, color.R, color.G, color.B), 0.25));
            b.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(0x00, color.R, color.G, color.B), 1.0));
            return b;
        }

        /// <summary>
        /// creates a new linear horizontal gradient from teh given color to transparent. the gradient is from bottom to top, 
        /// meaning the color is full at the bottom an fully translucent at the top.
        /// </summary>
        /// <param name="color"></param>
        /// <returns></returns>
        public static Brush getBrushFromColorLinearGradient(Color color)
        {
            LinearGradientBrush b = new LinearGradientBrush();
            b.StartPoint = new Point(0, 1);
            b.EndPoint = new Point(0, 0);
            b.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(0xFF, color.R, color.G, color.B), 0.4)); //red
            b.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(0x00, color.R, color.G, color.B), 1.0)); //green
            return b;
        }
    }


}
