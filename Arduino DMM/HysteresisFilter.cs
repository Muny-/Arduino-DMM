using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Research.DynamicDataDisplay.Charts.Filters;

namespace Arduino_DMM
{
    class HysteresisFilter : PointsFilterBase
    {
        double lastX;

        const int PDelta = 1;
        const int NDelta = -1;

        double lastY = 0;

        public override List<Point> Filter(List<Point> points)
        {
            List<Point> res = new List<Point>(points.Count);
            for (int i = 0; i < points.Count; i++)
            {
                if (i != 0)
                {
                    double delta = points[i].Y - lastY;

                    if (delta < 0)
                    {
                        if (delta < NDelta)
                        {
                            lastY = points[i].Y;
                        }
                    }
                    else
                    {
                        if (delta > PDelta)
                        {
                            lastY = points[i].Y;
                        }
                    }

                    res.Add(new Point(points[i].X, lastY));
                }
                else
                {
                    res.Add(points[0]);
                    lastY = points[0].Y;
                }
            }

            /*for (int i = 0; i < points.Count; i++)
            {
                res.Add(new Point(lastX, 0));
            }*/

            return res;
        }

        public override void SetScreenRect(Rect screenRect)
        {
            lastX = screenRect.X + screenRect.Width;
        }
    }
}
