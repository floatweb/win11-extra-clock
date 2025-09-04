using System;
using System.Windows;


namespace Win11_Extra_Clock.Models
{
    public enum Position
    {
        TopLeft,
        TopCenter,
        TopRight,
        MiddleLeft,
        Center,
        MiddleRight,
        BottomLeft,
        BottomCenter,
        BottomRight,
        Custom
    }


    public static class PositionHelper
    {
        public static (double left, double top) GetTopLeftFor(Position pos, Rect workArea, double width, double height, double margin, System.Windows.Point? customTopLeft = null)
        {
            double xCenter = workArea.Left + (workArea.Width - width) / 2.0;
            double yCenter = workArea.Top + (workArea.Height - height) / 2.0;


            if (pos == Position.Custom)
            {
                // default fallback
                var p = customTopLeft ?? new System.Windows.Point(workArea.Right - width - margin, workArea.Top + margin);

                // workarea fix
                double minX = workArea.Left + margin;
                double maxX = workArea.Right - width - margin;
                double minY = workArea.Top + margin;
                double maxY = workArea.Bottom - height - margin;

                double left = Clamp(p.X, minX, maxX);
                double top = Clamp(p.Y, minY, maxY);
                return (Math.Round(left), Math.Round(top));
            }


            return pos switch
            {
                Position.TopLeft => (workArea.Left + margin, workArea.Top + margin),
                Position.TopCenter => (xCenter, workArea.Top + margin),
                Position.TopRight => (workArea.Right - width - margin, workArea.Top + margin),
                Position.MiddleLeft => (workArea.Left + margin, yCenter),
                Position.Center => (xCenter, yCenter),
                Position.MiddleRight => (workArea.Right - width - margin, yCenter),
                Position.BottomLeft => (workArea.Left + margin, workArea.Bottom - height - margin),
                Position.BottomCenter => (xCenter, workArea.Bottom - height - margin),
                Position.BottomRight => (workArea.Right - width - margin, workArea.Bottom - height - margin),
                _ => (workArea.Right - width - margin, workArea.Top + margin)
            };
        }

        private static double Clamp(double v, double min, double max)
            => v < min ? min : (v > max ? max : v);
    }
}