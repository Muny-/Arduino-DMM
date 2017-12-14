using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Research.DynamicDataDisplay.Charts.Filters;

namespace Arduino_DMM
{
    class LastXFilter : PointsFilterBase
    {
        public double LastX;

        public override List<Point> Filter(List<Point> points)
        {
            return points;
        }

        public override void SetScreenRect(Rect screenRect)
        {
            LastX = screenRect.X + screenRect.Width;
        }
    }
}
