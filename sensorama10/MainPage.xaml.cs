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
using WinRTXamlToolkit.Controls.DataVisualization.Charting;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace sensorama10
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            UpdateCharts();

        }

        private Grove10DoF sensor = null;
        private DispatcherTimer MainTimer = null;

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            sensor = new Grove10DoF();
            if (await sensor.InitSensor() == true)
            {
                Status.Text = "BMP180 found";
                MainTimer = new DispatcherTimer();
                MainTimer.Interval = new TimeSpan(0, 0, 0, 0, 1);
                MainTimer.Tick += MainTimer_Tick;
                MainTimer.Start();
            }
            else
            {
                Status.Text = "I2C Error";
            }

            if (await sensor.InitMPU9250Sensor() == true)
            {

            }

        }

        public class NameValueItem
        {
            public string Name { get; set; }
            public float Value { get; set; }
        }

        private int rate = 25;
        private List<NameValueItem> items = new List<NameValueItem>();
        private int x = 0;

        private void UpdateCharts()
        {
            ((LineSeries)this.TempChart.Series[0]).ItemsSource = items;
        }

        private async void MainTimer_Tick(object sender, object e)
        {
            MainTimer.Stop();

            //-----------------
            //uint utemp = await sensor.BMP180ReadUT();
            float temp = sensor.BMP180GetTemp(await sensor.BMP180ReadUT());
            //ulong upres = await sensor.BMP180ReadUP();
            long press = sensor.BMP180GetPressure(await sensor.BMP180ReadUP());
            float alt = sensor.BMP180CalcAltitude(press);
            float atm = press * 1000 / 101325;

            Status.Text = "BMP180: Temp: " + temp.ToString() + " °C --- Pressure: " + atm.ToString() + " mbar (" + press.ToString() + " Pa) --- Alt: " + alt.ToString() + " m";

            if (items.Count > 25)
            {
                items.RemoveAt(0);
            }
            items.Add(new NameValueItem { Name = x.ToString(), Value = temp });
            x++;
            TempChart.LegendItems.Clear();
            ((LineSeries)this.TempChart.Series[0]).Refresh();

            //-----------------
            await sensor.ReadMPU9250Sensor();
            Status1.Text = "MPU9250: ax: " + sensor.ax.ToString() + " ay: " + sensor.ay.ToString() + " az: " +  sensor.az.ToString();
            Status2.Text = "MPU9250: gx: " + sensor.gx.ToString() + " gy: " + sensor.gy.ToString() + " gz: " + sensor.gz.ToString();




            MainTimer.Start(); 
        }
    }
}
