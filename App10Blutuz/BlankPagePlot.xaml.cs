using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Axes;

// Документацию по шаблону элемента "Пустая страница" см. по адресу https://go.microsoft.com/fwlink/?LinkId=234238

namespace App10Blutuz
{
    /// <summary>
    /// Пустая страница, которую можно использовать саму по себе или для перехода внутри фрейма.
    /// </summary>
    public sealed partial class BlankPagePlot : Page
    {
        public BlankPagePlot()
        {
            PlotModel = new MainViewModel();
         
            this.InitializeComponent();
            chart.Model = PlotModel.MyModel;
        }
        public void iniPlot(string ss)
        {

            PlotModel.MyModel.Title = ss;
            chart.InvalidatePlot(true);


        }
      public MainViewModel PlotModel { get; set; }
        public class MainViewModel
        {
            public MainViewModel()
            {
                this.MyModel = new PlotModel();
                this.MyModel.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title="X" });
                this.MyModel.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Y" });
                MyModel.InvalidatePlot(true);
            }
            LineSeries lineSeries1 { get; set; }
            public void addSeries()
            {
                try
                {


                    this.MyModel.Series.Clear();
                    lineSeries1 = new LineSeries();
                    this.MyModel.Series.Add(lineSeries1);
                    MyModel.InvalidatePlot(true);
                }
                catch(Exception)
                {

                }
                


            }
            public void addPoint(double x)
            {
                try
                {


                    var dd = MyModel.Series.ElementAt(0) as LineSeries;
                    lineSeries1.Points.Add(new DataPoint(dd.Points.Count + 1, x));
                    MyModel.InvalidatePlot(true);
                }
                catch(Exception)
                {

                }
                
            }

            public PlotModel MyModel { get; set; }
        }
    }
}
