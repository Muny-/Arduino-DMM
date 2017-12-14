using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Research.DynamicDataDisplay.Charts.Filters;

namespace Arduino_DMM
{
    class TimeSmoothFilter : PointsFilterBase
    {
        double lastX;

        const int smoothing_values = 50;

        List<double> pastXDeltas = new List<double>(smoothing_values);
        int cur_index = 0;

        public override List<Point> Filter(List<Point> points)
        {
            List<Point> res = new List<Point>(points.Count);

            double timesum = 0;

            int sample = 0;

            for (int i = 1; i < points.Count; i++)
            {
                sample++;
                timesum += points[i].X - points[i - 1].X;
            }

            double avg_x_delta = timesum / sample;

            cur_index++;

            pastXDeltas[cur_index] = avg_x_delta;

            if (cur_index == smoothing_values)
                cur_index = 0;

            double fin_x_delta = 0;

            for (int i = 0; i < smoothing_values; i++)
            {
                fin_x_delta += pastXDeltas[i];
            }

            fin_x_delta = fin_x_delta / smoothing_values;
            

            double x = points[0].X;

            for (int i = 0; i < points.Count; i++)
            {
                if (i != 0)
                    x += fin_x_delta;

                res.Add(new Point(x, points[i].Y));
            }

            return res;
        }

        public override void SetScreenRect(Rect screenRect)
        {
            lastX = screenRect.X + screenRect.Width;
        }
    }
}
