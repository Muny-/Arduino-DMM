using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.IO.Ports;
using System.Threading;
using DNBSoft.WPF.RollingMonitor;
using Microsoft.Research.DynamicDataDisplay.DataSources;
using Microsoft.Research.DynamicDataDisplay;
using System.Globalization;
using System.Collections.ObjectModel;

namespace Arduino_DMM
{
    public static class ExtensionMethods
    {
        public static decimal Map(this decimal value, decimal fromSource, decimal toSource, decimal fromTarget, decimal toTarget)
        {
            return (value - fromSource) / (toSource - fromSource) * (toTarget - fromTarget) + fromTarget;
        }

        public static IList<OTimeSpan> Clone<OTimeSpan>(this IList<OTimeSpan> listToClone) where OTimeSpan : ICloneable
        {
            return listToClone.Select(item => (OTimeSpan)item.Clone()).ToList();
        }

        public static IList<double> CloneDouble<Double>(this IList<Double> listToClone) where Double : ICloneable
        {
            return listToClone.Select(item => (double)item.Clone()).ToList();
        }
    }

    public class VData
    {
        public int Value;
        public DateTime Time;

        public VData(int value, DateTime time)
        {
            this.Value = value;
            this.Time = time;
        }
    }
       
    public static class FormatExtensions
    {
        private static string ToEngineeringNotation(this double d)
        {
            double exponent = Math.Log10(Math.Abs(d));
            if (Math.Abs(d) >= 1)
            {
                switch ((int)Math.Floor(exponent))
                {
                    case 0:
                    case 1:
                    case 2:
                    return d.ToString("0.00");
                    case 3:
                    case 4:
                    case 5:
                    return (d / 1e3).ToString("0.00") + "k";
                    case 6:
                    case 7:
                    case 8:
                    return (d / 1e6).ToString("0.00") + "M";
                    case 9:
                    case 10:
                    case 11:
                    return (d / 1e9).ToString("0.00") + "G";
                    case 12:
                    case 13:
                    case 14:
                    return (d / 1e12).ToString("0.00") + "T";
                    case 15:
                    case 16:
                    case 17:
                    return (d / 1e15).ToString("0.00") + "P";
                    case 18:
                    case 19:
                    case 20:
                    return (d / 1e18).ToString("0.00") + "E";
                    case 21:
                    case 22:
                    case 23:
                    return (d / 1e21).ToString("0.00") + "Z";
                    default:
                    return (d / 1e24).ToString("0.00") + "Y";
                }
            }
            else if (Math.Abs(d) > 0)
            {
                switch ((int)Math.Floor(exponent))
                {
                    case -1:
                    case -2:
                    case -3:
                    return (d * 1e3).ToString("0.00") + "m";
                    case -4:
                    case -5:
                    case -6:
                    return (d * 1e6).ToString("0.00") + "μ";
                    case -7:
                    case -8:
                    case -9:
                    return (d * 1e9).ToString("0.00") + "n";
                    case -10:
                    case -11:
                    case -12:
                    return (d * 1e12).ToString("0.00") + "p";
                    case -13:
                    case -14:
                    case -15:
                    return (d * 1e15).ToString("0.00") + "f";
                    case -16:
                    case -17:
                    case -18:
                    return (d * 1e15).ToString("0.00") + "a";
                    case -19:
                    case -20:
                    case -21:
                    return (d * 1e15).ToString("0.00") + "z";
                    default:
                    return (d * 1e15).ToString("0.00") + "y";
                }
            }
            else
            {
                return "0";
            }
        }

        public static string ToEngineering( this double value )
        {
            /*int exp = (int)(Math.Floor( Math.Log10( value ) / 3.0 ) * 3.0);
            double newValue = value * Math.Pow(10.0,-exp);
            if (newValue >= 1000.0) {
                    newValue = newValue / 1000.0;
                    exp = exp + 3;
            }
            return string.Format( "{0:##0}e{1}", newValue, exp); */

            return value.ToEngineeringNotation();
        }
    }

    [ValueConversion(typeof(double), typeof(string))]
    public class DoubleIntStrConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return System.Convert.ToInt32(value).ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return new NotImplementedException();
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        LineGraph lg;

        LineGraph rollingLineGraph;

        ObservableCollection<Point> rollingData = new ObservableCollection<Point>();

        SerialPort serialPort1;

        // 0 = Analog
        // 1 = Digital
        int VoltageMode = 0;

        // 0 = rolling
        // 1 = normal
        int graphMode = 1;

        bool TriggerFrequency = false;

        bool ExtrapolateNegatives = false;

        RollingSeries s1;

        RollingSeries.NextValueDelegate del = new RollingSeries.NextValueDelegate(NextValue);

        Microsoft.Research.DynamicDataDisplay.Charts.Shapes.DraggablePoint dp1;
        Microsoft.Research.DynamicDataDisplay.Charts.Shapes.DraggablePoint dp2;

        Microsoft.Research.DynamicDataDisplay.Charts.VerticalLine m2;
        Microsoft.Research.DynamicDataDisplay.Charts.VerticalLine m1;

        Microsoft.Research.DynamicDataDisplay.Charts.HorizontalLine h1;
        Microsoft.Research.DynamicDataDisplay.Charts.HorizontalLine h2;

        Microsoft.Research.DynamicDataDisplay.ViewportRestrictions.MaxSizeRestriction maxSizeRestriction = new Microsoft.Research.DynamicDataDisplay.ViewportRestrictions.MaxSizeRestriction();

        HorizontalAxisTitle hztitle;

        int Samples = 1000;

        double xBias = 0;
        double yBias = 0;

        int startIndex = 0;
        int startIndexSafe = 0;
        int startEndIndex = 0;
        int endIndex = 0;

        int rel_startEndIndex = 0;

        double VoltTrigger = 1;

        double lastXDelta = 0;

        LastXFilter lastXFilter = new LastXFilter();

        int SampleDelay = 0;

        void ResetS1()
        {
            s1 = new RollingSeries(rollingGraph, del);
            s1.LineBrush = red;
            s1.LineThickness = 1;
        }

        public MainWindow()
        {

            string port = "";

            COMPortPrompt cpp = new COMPortPrompt();
            if (cpp.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                port = cpp.COMPort;
            }
            else
            {
                Environment.Exit(0);
            }

            InitializeComponent();

            System.Diagnostics.Process.GetCurrentProcess().PriorityClass = System.Diagnostics.ProcessPriorityClass.RealTime;


            rollingLineGraph = staticGraph.AddLineGraph(rollingData.AsDataSource());

            staticGraph.Children.Remove(rollingLineGraph);

            //ResetS1();

            //rollingGraph.MaxValue = 55;

            //s1.IsRunning = false;

            serialPort1 = new SerialPort(port, 1000000, Parity.None, 8, StopBits.One);
            serialPort1.Handshake = Handshake.None;
            serialPort1.DtrEnable = true;
            serialPort1.ReadBufferSize = 1024 * 10;
            serialPort1.WriteTimeout = 1;
            serialPort1.WriteBufferSize = 2;

            //serialPort1.DataReceived += serialPort1_DataReceived;

            serialPort1.Open();

            SetupDataSources(new List<OTimeSpan>(), new List<double>());

            

            lg = new LineGraph(new CompositeDataSource(xDataSource, yDataSource));
            lg.Stroke = new SolidColorBrush(Color.FromRgb(0, 100, 255));
            //lg.Description = new PenDescription(String.Format("Data series {0}", 1 + 1));
            lg.StrokeThickness = 2;

            staticGraph.Legend.Visibility = System.Windows.Visibility.Hidden;
            staticGraph.LegendVisible = false;
            staticGraph.Legend.AutoShowAndHide = true;

            //Microsoft.Research.DynamicDataDisplay.Filters.FrequencyFilter ff = new Microsoft.Research.DynamicDataDisplay.Filters.FrequencyFilter();
            
            //lg.Filters.Add(ff);

            //HysteresisFilter hf = new HysteresisFilter();

            //lg.Filters.Add(hf);

            lg.Filters.Add(lastXFilter);


            hztitle = new HorizontalAxisTitle();
            hztitle.Content = "s";

            VerticalAxisTitle vatitle = new VerticalAxisTitle();
            vatitle.Content = "v";
            
            staticGraph.Children.Add(lg);

            staticGraph.AxisGrid.path.Stroke = new SolidColorBrush(Color.FromRgb(23,23,23));

            Microsoft.Research.DynamicDataDisplay.Charts.HorizontalTimeSpanAxis htsa = new Microsoft.Research.DynamicDataDisplay.Charts.HorizontalTimeSpanAxis();
            staticGraph.HorizontalAxis = htsa;
            
            
            staticGraph.Children.Add(hztitle);
            staticGraph.Children.Add(vatitle);

            dp1 = new Microsoft.Research.DynamicDataDisplay.Charts.Shapes.DraggablePoint(new Point(0, 0));
            dp1.PositionChanged += dp1_PositionChanged;
            dp1.Foreground = red;
            Panel.SetZIndex(dp1, 12);
            staticGraph.Children.Add(dp1);

            dp2 = new Microsoft.Research.DynamicDataDisplay.Charts.Shapes.DraggablePoint(new Point(0, 0));
            dp2.PositionChanged += dp2_PositionChanged;
            dp2.Foreground = new SolidColorBrush(Color.FromArgb(50, 0, 255, 0));
            Panel.SetZIndex(dp2, 13);
            staticGraph.Children.Add(dp2);

            m1 = new Microsoft.Research.DynamicDataDisplay.Charts.VerticalLine();
            m1.Value = 0;
            m1.Stroke = red;
            Panel.SetZIndex(m1, 10);
            staticGraph.Children.Add(m1);

            m2 = new Microsoft.Research.DynamicDataDisplay.Charts.VerticalLine();
            m2.Value = 0;
            m2.Stroke = new SolidColorBrush(Color.FromArgb(50, 0, 255, 0));
            Panel.SetZIndex(m2, 11);
            staticGraph.Children.Add(m2);

            h1 = new Microsoft.Research.DynamicDataDisplay.Charts.HorizontalLine();
            h1.Value = 0;
            h1.Stroke = red;
            Panel.SetZIndex(h1, 10);
            staticGraph.Children.Add(h1);

            h2 = new Microsoft.Research.DynamicDataDisplay.Charts.HorizontalLine();
            h2.Value = 0;
            h2.Stroke = new SolidColorBrush(Color.FromArgb(50, 0, 255, 0));
            Panel.SetZIndex(h2, 11);
            staticGraph.Children.Add(h2);

            SetCursorsVisibility(System.Windows.Visibility.Hidden);

            new Thread(delegate()
            {
                while(true)
                {
                    try
                    {
                        updatesLabel.Dispatcher.Invoke(delegate()
                        {

                            updatesLabel.Content = "Samp. rate: " + ((double)cur_updates).ToEngineering() + "S/s\nlen(q): " + DataQueue.Count;

                        });

                        cur_updates = 0;
                    }
                    catch
                    {

                    }

                    

                    Thread.Sleep(1000);
                }
            }).Start();

            new Thread(delegate()
            {
                

                while(true)
                {
                    
                    if (FrequencySmoothing.Count > 0)
                    {

                        double freqsum = 0;

                        var tmpFreq = new List<double>(FrequencySmoothing);
                        FrequencySmoothing.Clear();

                        for (int i = 0; i < tmpFreq.Count; i++)
                        {
                            freqsum += tmpFreq[i];
                        }

                        double smoothed_freq = freqsum / tmpFreq.Count;

                        if (smoothed_freq > 0)
                        {

                            frequencyLabel.Dispatcher.Invoke(delegate()
                            {
                                frequencyLabel.Content = smoothed_freq.ToEngineering() + "Hz";
                            });

                        }
                    }

                    Thread.Sleep(1000);
                }
            }).Start();

            new Thread(delegate()
            {
                int index = 0;

                while(true)
                {
                    if (graphMode == 0)
                    {
                        double val = currentValue;

                        if (DigitalFilter)
                            val = digitizeY(val);

                        Dispatcher.Invoke(delegate()
                        {
                            rollingData.Add(new Point(index++, val));
                            //staticGraph.FitToView();
                            //staticGraph.Viewport.MaxWidth = 500;
                            
                        });

                        Thread.Sleep(10);
                        
                    }
                    else
                    {
                        Thread.Sleep(1000);
                    }
                }
            }).Start();

            new Thread(delegate()
            {
                bool negVal = false;

                bool polarityPhase = true;

                while(true)
                {
                    if (serialPort1.IsOpen)
                    {
                        byte[] buf = new byte[1];

                        serialPort1.Read(buf, 0, buf.Length);

                        for (int i = 0; i < buf.Length; i++)
                        {
                            int val = buf[i];

                            if (!Paused)
                            {

                                //Console.WriteLine(line);

                                if (VoltageMode == 0)
                                {
                                    val = (int)(((decimal)val).Map(0, 255, 0, 1023));

                                    double voltage = (double)((val * 4.9d) / 1023d);

                                    currentValue = voltage * 12.1;


                                    // this is the positive line, if it's 0, switch to negative
                                    /*if (currentValue == 0 && polarityPhase)
                                        negVal = true;
                                    // this is the negative line, if it's 0, switch to positive
                                    else if (currentValue == 0 && !polarityPhase)
                                        negVal = false;

                                    if (negVal)
                                    {
                                        currentValue = -currentValue;
                                    }



                                    polarityPhase = !polarityPhase;*/

                                    //currentValue -= 0.18;

                                    /*string tmpcurVal = currentValue.ToString();

                                    if (tmpcurVal != "0")
                                        tmpcurVal = tmpcurVal.Remove(tmpcurVal.Length - 1, 1);

                                    currentValue = double.Parse(tmpcurVal);*/
                                }
                                else if (VoltageMode == 1)
                                {

                                    currentValue = /*(1 - (*/val/*))*/;

                                    //Console.WriteLine(val);

                                    //currentValue = val;
                                }

                                if (AnalogVoltagePaused && VoltageMode == /*0*/0)
                                {
                                    label1.Dispatcher.BeginInvoke(new Action(delegate()
                                    {
                                        label1.Content = currentValue.ToString("0.00") + "v";
                                    }));
                                }
                                else if (!AnalogVoltagePaused && !didSet)
                                {
                                    label1.Dispatcher.Invoke(delegate()
                                    {
                                        label1.Content = "PAUSED";
                                    });
                                }

                                if (graphMode == 1)
                                {
                                    /*if ((VoltageMode == 0 && currentValue != 0) || VoltageMode == 1)
                                    {*/
                                        TimeSpan timeElapsed = (DateTime.Now - lastUpdate);

                                        if (/*current_datapoints < max_datapoints && current_datapoints < x.Length*/current_datapoints < Samples)
                                        {
                                            xValues.Add(new OTimeSpan(timeElapsed.Ticks));
                                            yValues.Add(currentValue);

                                            current_datapoints++;


                                        }
                                        else
                                        {
                                            if (DigitalFilter)
                                            {
                                                List<OTimeSpan> newer_x = new List<OTimeSpan>(xValues.Count * 2);
                                                List<double> newer_y = new List<double>(yValues.Count * 2);

                                                for (int ll = 0; ll < xValues.Count; ll++)
                                                {
                                                    newer_x.Add(xValues[ll]);
                                                    newer_y.Add(digitizeY(yValues[ll]));

                                                    if (ll < xValues.Count - 1)
                                                    {
                                                        //res.Add(new Point(points[ll + 1].X, digitizeY(points[ll].Y)));

                                                        newer_x.Add(xValues[ll + 1]);
                                                        newer_y.Add(digitizeY(yValues[ll]));
                                                    }
                                                    else
                                                    {
                                                        //res.Add(new Point(lastX, digitizeY(points[ll].Y)));

                                                        //newer_x.Add(xValues[ll]);

                                                        newer_x.Add(xValues[ll - 2]);

                                                        newer_y.Add(digitizeY(yValues[ll]));
                                                    }
                                                }

                                                xValues = newer_x;
                                                yValues = newer_y;
                                            }


                                            if (AveragedTiming)
                                            {


                                                double timesum = 0;

                                                int sample = 0;

                                                for (int ll = 1; ll < xValues.Count; ll++)
                                                {
                                                    sample++;
                                                    timesum += xValues[ll].Ticks - xValues[ll - 1].Ticks;
                                                }

                                                double avg_x_delta = timesum / sample;


                                                double x = xValues[0].Ticks;

                                                for (int ll = 0; ll < xValues.Count; ll++)
                                                {
                                                    if (ll != 0)
                                                        x += avg_x_delta;

                                                    xValues[ll].Ticks = (long)x;
                                                }
                                            }

                                            if (ExtrapolateNegatives)
                                            {
                                                //List<OTimeSpan> newer_x = new List<OTimeSpan>(xValues.Count * 2);
                                                List<double> newer_y = new List<double>(yValues);

                                                int waveFormStartIndex = -1;
                                                int waveFormEndIndex = -1;

                                                int waveFormHalfLen = -1;

                                                bool done = false;

                                                int cur_index = 0;

                                                

                                                while (cur_index < yValues.Count)
                                                {

                                                    for (; cur_index < yValues.Count; cur_index++)
                                                    {
                                                        if (newer_y[cur_index] == 0)
                                                        {
                                                            waveFormStartIndex = cur_index;

                                                            // to ignore the waveFormStartIndex:
                                                            cur_index++;

                                                            break;
                                                        }
                                                        else if (cur_index >= yValues.Count)
                                                            done = true;
                                                    }

                                                    if (waveFormStartIndex != -1)
                                                    {
                                                        for (; cur_index < yValues.Count; cur_index++)
                                                        {
                                                            if (newer_y[cur_index] == 0)
                                                            {
                                                                waveFormEndIndex = cur_index;

                                                                // to fix something
                                                                cur_index++;

                                                                break;
                                                            }
                                                            else if (cur_index >= yValues.Count)
                                                                done = true;
                                                        }

                                                        if (waveFormEndIndex != -1)
                                                        {
                                                            waveFormHalfLen = waveFormEndIndex - waveFormStartIndex;

                                                            for (int tt = waveFormEndIndex; tt < waveFormEndIndex + waveFormHalfLen; tt++ )
                                                            {
                                                                int corres_index = tt - waveFormHalfLen;

                                                                if (corres_index > 0 && tt < yValues.Count)
                                                                {
                                                                    newer_y[tt] = -(yValues[corres_index]);
                                                                }
                                                                else if (tt >= yValues.Count)
                                                                    done = true;
                                                            }
                                                        }
                                                    }
                                                }

                                                if (cur_index >= yValues.Count)
                                                    done = true;

                                                //xValues = newer_x;
                                                yValues = newer_y;
                                            }





                                            List<OTimeSpan> new_xval = null;
                                            List<double> new_yval = null;

                                            int status = 0;

                                            if (TriggerFrequency)
                                            {
                                                new_xval = new List<OTimeSpan>();
                                                new_yval = new List<double>();

                                                // 0 = no start found
                                                // 1 = start found
                                                // 2 = start end fond
                                                // 3 = end found





                                                bool add = false;

                                                long delta = 0;

                                                int startAddIndex = 0;

                                                for (int si = 400; si < xValues.Count; si++)
                                                {
                                                    if (si != 0)
                                                    {
                                                        if (status == 0)
                                                        {
                                                            if (yValues[si - 1] < VoltTrigger)
                                                            {
                                                                if (yValues[si] >= VoltTrigger)
                                                                {
                                                                    status = 1;
                                                                    startIndexSafe = si;

                                                                    delta = xValues[si].Ticks - xValues[0].Ticks;

                                                                    int numToAdd = 100;

                                                                    if (si < numToAdd)
                                                                        numToAdd = si;

                                                                    startIndex = numToAdd;

                                                                    for (int ii = numToAdd; ii > 0; ii--)
                                                                    {
                                                                        new_xval.Add(new OTimeSpan(xValues[si - numToAdd].Ticks - delta));
                                                                        new_yval.Add(yValues[si - numToAdd]);
                                                                    }

                                                                    add = true;
                                                                }
                                                            }
                                                        }
                                                        else if (status == 1)
                                                        {
                                                            if (/*yValues[si] < VoltTrigger*/ yValues[si] == 0)
                                                            {
                                                                status = 2;
                                                                startEndIndex = si;
                                                                rel_startEndIndex = new_xval.Count;
                                                            }
                                                        }
                                                        else if (status == 2)
                                                        {
                                                            if (/*yValues[si] >= VoltTrigger*/ yValues[si] >= VoltTrigger)
                                                            {
                                                                status = 3;
                                                                //endIndex = si;

                                                                endIndex = new_xval.Count;
                                                            }
                                                        }
                                                    }

                                                    if (add)
                                                    {
                                                        new_xval.Add(new OTimeSpan(xValues[si].Ticks - delta));
                                                        new_yval.Add(yValues[si]);
                                                    }
                                                }

                                                if (status == 3)
                                                {
                                                    new_yval[startIndex] = 0;
                                                }

                                                /*if (status <= 1 && Samples < 1600)
                                                {
                                                    Samples+=10;

                                                    Dispatcher.Invoke(delegate()
                                                    {
                                                        speedSlider.Value = Samples;
                                                    });
                                                }*/
                                            }

                                            //Console.WriteLine("updating...");

                                            Dispatcher.Invoke(delegate()
                                            {

                                                if (TriggerFrequency && new_xval != null && new_xval.Count > endIndex && new_yval.Count > startEndIndex)
                                                {
                                                    if (new_xval[startIndex].Ticks == 0)
                                                    {
                                                        if (/*new_yval[startIndex] > VoltTrigger*/status == 3)
                                                        {
                                                            try
                                                            {
                                                                dp1.Position = new Point(new_xval[startIndex].Ticks + (xBias * 50000), new_yval[startIndex] + yBias);
                                                                dp2.Position = new Point(new_xval[endIndex].Ticks + (xBias * 50000), new_yval[endIndex] + yBias);
                                                            }
                                                            catch
                                                            {

                                                            }
                                                        }

                                                        //UpdateDeltaGR();

                                                        SetupDataSources(new_xval, new_yval);
                                                    }

                                                }
                                                else
                                                    SetupDataSources((List<OTimeSpan>)xValues.Clone(), new List<double>(yValues));

                                                //yDataSource.RaiseDataChanged();

                                                //max_datapoints = (int)speedSlider.Value;




                                                lg.DataSource = new CompositeDataSource(xDataSource, yDataSource);

                                                yDataSource.RaiseDataChanged();





                                                //lg.Stroke = new SolidColorBrush(Color.FromRgb(0, 0, 255));
                                                //lg.Description = new PenDescription(String.Format("Data series {0}", 1 + 1));

                                                //lg.StrokeThickness = 2;

                                                dataPointsLabel.Content = current_datapoints.ToString();


                                            });



                                            current_datapoints = 0;

                                            lastUpdate = DateTime.Now;

                                            xValues.Clear();
                                            yValues.Clear();

                                        }
                                    //}
                                }
                                else
                                {
                                    
                                }

                                cur_updates++;
                            }
                        }
                    }
                }
            }).Start();

            HandleQueue = new Thread(QueueHandler);
            HandleQueue.Start();

            //serialPort1.DataReceived +=serialPort1_DataReceived;
            
        }

        void serialPort1_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            Console.WriteLine(serialPort1.ReadLine());

            /*if (VoltageMode == 0)
            {


                try
                {*/

            //Console.WriteLine(val);

            /*byte[] buf = new byte[2];

            buf[0] = (byte)serialPort1.ReadByte();
            buf[1] = (byte)serialPort1.ReadByte();

            readEndPos:
            byte endPos = (byte)serialPort1.ReadByte();

            if (endPos != (byte)255)
                goto readEndPos;

            Int16 val = BitConverter.ToInt16(buf, 0);*/

            //int val = serialPort1.ReadByte();

            //int val = serialPort1.ReadByte()*3;

            //Console.WriteLine(val);

            //DataQueue.Enqueue(new VData(val/*/val*3*/, DateTime.Now));
            /*}
            catch (System.ArgumentException)
            {

            }
            catch (System.FormatException)
            {
                serialPort1.Close();
                serialPort1.Open();
            }
        }
        else if (VoltageMode == 1)
        {
            try
            {
                DataQueue.Enqueue(new VData(serialPort1.ReadByte(), DateTime.Now));
            }
            catch
            {

            }
        }*/


        }

        double digitizeY(double y)
        {
            if (y > 0)
                return 1;
            else
                return 0;
        }

        //string lastEnding = "m";

        void SetupDataSources(List<OTimeSpan> ts, List<double> _y)
        {
            xDataSource = new EnumerableDataSource<OTimeSpan>(ts);
            xDataSource.SetXMapping(X => X.Ticks + (xBias*50000));

            //xDataSource.SetXMapping(X => X.TotalMilliseconds);

            yDataSource = new EnumerableDataSource<double>(_y);
            yDataSource.SetYMapping(Y => Y + yBias);
        }

        List<double> FrequencySmoothing = new List<double>();

        void UpdateDeltaGR()
        {
            double deltaGxRx = m2.Value - m1.Value;
            double deltaGyRy = h2.Value - h1.Value;
            double slope = deltaGyRy / deltaGxRx;

            double freq = (1 / (deltaGxRx / 10000000));

            if (freq != Double.PositiveInfinity)
            {

                FrequencySmoothing.Add(freq);

                deltaGRLabel.Dispatcher.BeginInvoke(new Action(() =>
                {
                    //frequencyLabel.Content = freq.ToEngineering() + "Hz";
                    deltaGRLabel.Content = (deltaGxRx / 10000000).ToEngineering() + "s";
                    deltaGyRyLabel.Content = (deltaGyRy).ToEngineering() + "V";
                    slopeLabel.Content = slope.ToEngineering() + "v/s";
                }));
            }
            
        }

        void dp2_PositionChanged(object sender, Microsoft.Research.DynamicDataDisplay.Charts.PositionChangedEventArgs e)
        {
            m2.Value = e.Position.X;
            h2.Value = e.Position.Y;

            UpdateDeltaGR();
        }

        void dp1_PositionChanged(object sender, Microsoft.Research.DynamicDataDisplay.Charts.PositionChangedEventArgs e)
        {
            m1.Value = e.Position.X;
            h1.Value = e.Position.Y;

            UpdateDeltaGR();
        }

        const int max_datapoints = 100;

        int current_datapoints = 0;

        List<OTimeSpan> xValues = new List<OTimeSpan>();
        List<double> yValues = new List<double>();

        EnumerableDataSource<OTimeSpan> xDataSource;
        EnumerableDataSource<double> yDataSource;

        SolidColorBrush red = new SolidColorBrush(Color.FromArgb(50, 255, 0, 0));
        SolidColorBrush black = new SolidColorBrush(Color.FromArgb(50, 0, 0, 0));

        DateTime lastUpdate = DateTime.Now;

        

        Queue<VData> DataQueue = new Queue<VData>();

        static double currentValue = 0;

        int cur_updates = 0;

        Thread HandleQueue;

        OTimeSpan[] x = new OTimeSpan[0];
        double[] y = new double[0];

        void QueueHandler()
        {
            while (true)
            {
                if (DataQueue.Count > 0)
                {
                    /*try
                    {*/
                        //Console.WriteLine(line);

                        
                    /*}
                    catch (System.FormatException)
                    {
                        Console.WriteLine();
                        //serialPort1.Close();
                        //serialPort1.Open();
                    }*/

                    
                }
                else
                {
                    Thread.Sleep(1);
                }
            }
            
        }

        static double NextValue()
        {
            return currentValue;
        }

        private void speedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            //voltageGraph.UpdateInterval = (int)speedSlider.Value;

            //dataTime = new OTimeSpan(0, 0, 0, 0, (int)speedSlider.Value);

            Samples = (int)speedSlider.Value;

            /*if (lg != null)
            {
                staticGraph.Children.Remove(lg);

                current_datapoints = 0;



                //max_datapoints = (int)speedSlider.Value;

                x = new OTimeSpan[max_datapoints];
                y = new double[max_datapoints];

                xDataSource = new EnumerableDataSource<OTimeSpan>(x);
                xDataSource.SetXMapping(X => X.Milliseconds);

                yDataSource = new EnumerableDataSource<double>(y);
                yDataSource.SetYMapping(Y => Y);

                lg = new LineGraph(new CompositeDataSource(xDataSource, yDataSource));
                lg.Stroke = new SolidColorBrush(Color.FromRgb(0, 0, 255));
                //lg.Description = new PenDescription(String.Format("Data series {0}", 1 + 1));
                
                lg.StrokeThickness = 2;
                staticGraph.Children.Add(lg);
            }*/
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if ((string)GraphModeButton.Content == "Rolling")
            {
                GraphModeButton.Content = "Normal";
                graphMode = 1;

                staticGraph.Children.Add(lg);
                staticGraph.Children.Remove(rollingLineGraph);

                staticGraph.Viewport.Restrictions.Remove(maxSizeRestriction);
                
            }
            else
            {
                GraphModeButton.Content = "Rolling";
                graphMode = 0;

                staticGraph.Children.Remove(lg);
                staticGraph.Children.Add(rollingLineGraph);

                staticGraph.Viewport.Restrictions.Add(maxSizeRestriction);
            }
        }

        bool Paused = false;

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if ((string)pauseButton.Content == "Pause")
            {
                Paused = true;
                pauseButton.Content = "Play";
            }
            else
            {
                Paused = false;
                pauseButton.Content = "Pause";
            }
        }

        private void xBiasSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            xBias = xBiasSlider.Value;

            if (yDataSource != null)
                yDataSource.RaiseDataChanged();
        }

        private void yBiasSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            yBias = yBiasSlider.Value;

            if (yDataSource != null)
                yDataSource.RaiseDataChanged();
        }

        private void calcFreqCheckbox_Click(object sender, RoutedEventArgs e)
        {
            TriggerFrequency = (bool)calcFreqCheckbox.IsChecked;
        }

        bool ShowCursors = false;

        bool AveragedTiming = false;

        bool DigitalFilter = false;

        void SetCursorsVisibility(System.Windows.Visibility vis)
        {
            m1.Visibility = vis;
            m2.Visibility = vis;
            h1.Visibility = vis;
            h2.Visibility = vis;

            dp1.Visibility = vis;
            dp2.Visibility = vis;
        }

        private void showCursorsCheckbox_Click(object sender, RoutedEventArgs e)
        {
            if ((bool)showCursorsCheckbox.IsChecked)
            {
                ShowCursors = true;

                SetCursorsVisibility(System.Windows.Visibility.Visible);
            }
            else
            {
                ShowCursors = false;

                SetCursorsVisibility(System.Windows.Visibility.Hidden);
            }
        }

        private void AveragedTimingCheckbox_Click(object sender, RoutedEventArgs e)
        {
            AveragedTiming = (bool)AveragedTimingCheckbox.IsChecked;
        }

        private void triggerVoltageNumeric_KeyDown(object sender, KeyEventArgs e)
        {
            
        }

        void ChangeNumericUpDownValue(eisiWare.NumericUpDown control, double value)
        {
            if (control.MaxValue < value)
                control.Value = control.MaxValue;
            else if (control.MinValue > value)
                control.Value = control.MinValue;
            else
                control.Value = value;
        }

        private void triggerVoltageNumeric_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            eisiWare.NumericUpDown nup = (eisiWare.NumericUpDown)sender;

            double newVal = -999;

            if (e.Key == Key.Up)
            {
                newVal = nup.Value + nup.Step;
            }
            else if (e.Key == Key.Down)
            {
                newVal = nup.Value - nup.Step;
            }

            if (newVal != -999)
            {
                ChangeNumericUpDownValue(nup, newVal);

                
            }

            trigChange();
        }

        private void triggerVoltageNumeric_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            double newVal = 0;

            newVal = triggerVoltageNumeric.Value + (double)(e.Delta / 10000.0);

            ChangeNumericUpDownValue(triggerVoltageNumeric, newVal);

            trigChange();
        }

        void trigChange()
        {
            VoltTrigger = triggerVoltageNumeric.Value - 0.01;

            if (VoltTrigger < 0)
            {
                VoltTrigger = 0;

                triggerVoltageNumeric.Value = VoltTrigger;
            }
        }

        private void triggerVoltageNumeric_ValueChanged(object sender, RoutedEventArgs e)
        {

            trigChange();
            
        }

        private void digitalFilterCheckbox_Click(object sender, RoutedEventArgs e)
        {
            if ((bool)digitalFilterCheckbox.IsChecked)
            {
                DigitalFilter = true;
            }
            else
            {
                DigitalFilter = false;
            }
        }

        private void speedSlider_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            speedSlider.Value += (double)(e.Delta / 100.0);
        }

        int oldSampleDelay = 0;

        private void sampleDelayNumericUpDown_ValueChanged(object sender, RoutedEventArgs e)
        {
            if (oldSampleDelay != (int)sampleDelayNumericUpDown.Value)
            {

                SampleDelay = (int)sampleDelayNumericUpDown.Value;



                if (SampleDelay < 0)
                {
                    SampleDelay = 0;
                    sampleDelayNumericUpDown.Value = 0;
                }

                oldSampleDelay = SampleDelay;

                SendOptions();
            }
        }

        void SendOptions()
        {
            if ((bool)analogToggleButton.IsChecked)
                VoltageMode = 0;
            else
                VoltageMode = 1;

            for(int i = 0; i < 1; i++)
            {
                _sendopt();
            }
        }

        void _sendopt()
        {
            if (serialPort1 != null)
            {
                try
                {
                    serialPort1.Write(new byte[] { (byte)SampleDelay, (byte)VoltageMode, (byte)'\n' }, 0, 3);

                    serialPort1.BaseStream.Flush();
                    
                }
                catch
                {
                    serialPort1.Close();
                    Thread.Sleep(1000);
                    serialPort1.Open();
                    _sendopt();
                }
            }
        }

        private void analogToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            if (digitalToggleButton != null)
            {
                digitalToggleButton.IsChecked = false;
                SendOptions();
            }
        }

        private void analogToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            digitalToggleButton.IsChecked = true;
            SendOptions();
        }

        private void digitalToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            analogToggleButton.IsChecked = false;
            SendOptions();
        }

        private void digitalToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            analogToggleButton.IsChecked = true;
            SendOptions();
        }

        private void sampleDelayNumericUpDown_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            eisiWare.NumericUpDown nup = (eisiWare.NumericUpDown)sender;

            double newVal = 0;

            if (e.Key == Key.Up)
            {
                newVal = nup.Value + nup.Step;
            }
            else if (e.Key == Key.Down)
            {
                newVal = nup.Value - nup.Step;
            }

            ChangeNumericUpDownValue(nup, newVal);
        }

        private void sampleDelayNumericUpDown_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            eisiWare.NumericUpDown nup = (eisiWare.NumericUpDown)sender;

            double newVal = 0;



            if (nup.Step < 1)
                newVal = nup.Value + (double)(e.Delta / 10000.0);
            else
            {
                newVal = nup.Value + (double)(e.Delta / 100);
            }

            ChangeNumericUpDownValue(nup, newVal);
        }

        private void extrapolateNegativesCheckbox_Click(object sender, RoutedEventArgs e)
        {
            ExtrapolateNegatives = (bool)extrapolateNegativesCheckbox.IsChecked;
        }

        bool AnalogVoltagePaused = false;

        bool didSet = false;

        private void label1_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (AnalogVoltagePaused)
            {
                AnalogVoltagePaused = false;
                didSet = false;
            }
            else
            {
                AnalogVoltagePaused = true;

                didSet = false;
            }
        }
    }
}
