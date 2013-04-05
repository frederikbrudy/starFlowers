using Particles;
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
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace StarFlowers
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// dispatcher for frame events
        /// </summary>
        System.Windows.Threading.DispatcherTimer frameTimer;

        /// <summary>
        /// the spawn point of the particlesystems
        /// </summary>
        private Point3D spawnPoint;

        /// <summary>
        /// elapsed time since last frame, in seconds
        /// </summary>
        private double elapsed;

        /// <summary>
        /// elapesed time in seconds since start of the particle system
        /// </summary>
        private double totalElapsed;
        private int lastTick;
        private int currentTick;

        private int frameCount;
        private double frameCountTime;
        private int frameRate;

        private ParticleSystemManager pm;

        private Random rand;

        private const double particleSizeMultiplier = 10.0;
        private const double particleLifeMultiplier = 5.0;

        private Point mousePosition;

        /// <summary>
        /// Brush used to draw mouse point
        /// </summary>
        private Brush MouseBrush = Brushes.Blue;

        /// <summary>
        /// Thickness of body center ellipse
        /// </summary>
        private const double MouseThickness = 10;

        /// <summary>
        /// Drawing group for mouse position output
        /// </summary>
        private DrawingGroup drawingGroup;

        private DrawingImage imageSource;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            //this.WindowState = WindowState.Normal;
            //this.Width = System.Windows.SystemParameters.PrimaryScreenWidth;
            //this.Height = System.Windows.SystemParameters.PrimaryScreenHeight;
            Rect size = System.Windows.SystemParameters.WorkArea;
            this.Width = size.Width;
            this.Height = size.Height;
            this.Left = size.Left;
            this.Top = size.Top;
            this.initParticleSystem();
            this.initDrawingData();
            this.initBrushes(Colors.Red);
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Console.WriteLine(this.ActualWidth);
            Console.WriteLine(this.ActualHeight);
        }

        private void key_down(object sender, KeyEventArgs e)
        {
            if (e.Key.Equals(System.Windows.Input.Key.Escape))
            {
                System.Windows.Application.Current.Shutdown();
            }
            else if (e.Key.Equals(System.Windows.Input.Key.F11))
            {
                if (this.WindowState == WindowState.Maximized)
                {
                    this.WindowState = WindowState.Normal;
                }
                else
                {
                    this.WindowState = WindowState.Maximized;
                }
            }
            else
            {
                Console.WriteLine("key down: " + e.Key);
            }

        }

        private void WindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            
        }

        private void mouse_move(object sender, MouseEventArgs e)
        {
            this.mousePosition = Mouse.GetPosition(this);

            this.spawnPoint = new Point3D(mousePosition.X-(this.Width/2), (this.Height/2)-mousePosition.Y, 0.0);
        }


        private void initParticleSystem()
        {
            frameTimer = new System.Windows.Threading.DispatcherTimer();
            frameTimer.Tick += OnFrame;
            frameTimer.Interval = TimeSpan.FromSeconds(1.0 / 100.0);
            frameTimer.Start();

            this.spawnPoint = new Point3D(0.0, 0.0, 0.0);
            this.lastTick = Environment.TickCount;

            pm = new ParticleSystemManager();

            //this.ParticleHost.Children.Add
            this.WorldModels.Children.Add(pm.CreateParticleSystem(1000, Colors.Gray));
            this.WorldModels.Children.Add(pm.CreateParticleSystem(1000, Colors.Red));
            this.WorldModels.Children.Add(pm.CreateParticleSystem(1000, Colors.Silver));
            this.WorldModels.Children.Add(pm.CreateParticleSystem(1000, Colors.Orange));
            this.WorldModels.Children.Add(pm.CreateParticleSystem(1000, Colors.Yellow));

            rand = new Random(this.GetHashCode());

            //this.Cursor = System.Windows.Input.Cursors.None;
        }

        private void initDrawingData()
        {
            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            // Display the drawing using our image control
            MouseImage.Source = this.imageSource;
        }

        private void initBrushes(Color color)
        {
            RadialGradientBrush b = new RadialGradientBrush();
            b.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(0xFF, color.R, color.G, color.B), 0.25));
            b.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(0x00, color.R, color.G, color.B), 1.0));
            MouseBrush = b;
        }

        private void OnFrame(object sender, EventArgs e)
        {
            // Calculate frame time;
            this.currentTick = Environment.TickCount;
            this.elapsed = (double)(this.currentTick - this.lastTick) / 1000.0;
            this.totalElapsed += this.elapsed;
            this.lastTick = this.currentTick;

            frameCount++;
            frameCountTime += elapsed;
            if (frameCountTime >= 1.0)
            {
                frameCountTime -= 1.0;
                frameRate = frameCount;
                frameCount = 0;
                this.FrameRateLabel.Content = "FPS: " + frameRate.ToString() + "  Particles: " + pm.ActiveParticleCount.ToString();
            }

            pm.Update((float)elapsed);
            pm.SpawnParticle(this.spawnPoint, 10.0, Colors.Red, particleSizeMultiplier * rand.NextDouble(), particleLifeMultiplier * rand.NextDouble());
            pm.SpawnParticle(this.spawnPoint, 10.0, Colors.Orange, particleSizeMultiplier * rand.NextDouble(), particleLifeMultiplier * rand.NextDouble());
            pm.SpawnParticle(this.spawnPoint, 10.0, Colors.Silver, particleSizeMultiplier * rand.NextDouble(), particleLifeMultiplier * rand.NextDouble());
            pm.SpawnParticle(this.spawnPoint, 10.0, Colors.Gray, particleSizeMultiplier * rand.NextDouble(), particleLifeMultiplier * rand.NextDouble());
            pm.SpawnParticle(this.spawnPoint, 10.0, Colors.Red, particleSizeMultiplier * rand.NextDouble(), particleLifeMultiplier * rand.NextDouble());
            pm.SpawnParticle(this.spawnPoint, 10.0, Colors.Orange, particleSizeMultiplier * rand.NextDouble(), particleLifeMultiplier * rand.NextDouble());
            pm.SpawnParticle(this.spawnPoint, 10.0, Colors.Silver, particleSizeMultiplier * rand.NextDouble(), particleLifeMultiplier * rand.NextDouble());
            pm.SpawnParticle(this.spawnPoint, 10.0, Colors.Gray, particleSizeMultiplier * rand.NextDouble(), particleLifeMultiplier * rand.NextDouble());
            pm.SpawnParticle(this.spawnPoint, 10.0, Colors.Red, particleSizeMultiplier * rand.NextDouble(), particleLifeMultiplier * rand.NextDouble());
            pm.SpawnParticle(this.spawnPoint, 10.0, Colors.Yellow, particleSizeMultiplier * rand.NextDouble(), particleLifeMultiplier * rand.NextDouble());
            pm.SpawnParticle(this.spawnPoint, 10.0, Colors.Silver, particleSizeMultiplier * rand.NextDouble(), particleLifeMultiplier * rand.NextDouble());
            pm.SpawnParticle(this.spawnPoint, 10.0, Colors.Yellow, particleSizeMultiplier * rand.NextDouble(), particleLifeMultiplier * rand.NextDouble());

            double c = Math.Cos(this.totalElapsed);
            double s = Math.Sin(this.totalElapsed);

            //this.spawnPoint = new Point3D(s * 400.0, c * 200.0, 0.0);

            //using (DrawingContext dc = this.drawingGroup.Open())
            //{
            //    // Draw a transparent background to set the render size
            //    dc.DrawRectangle(Brushes.Transparent, null, new Rect(0.0, 0.0, this.Width, this.Height));

            //    for (int i = 0; i < 100; i++)
            //    {
            //        Point tempPoint = new Point(this.mousePosition.X + rand.NextDouble()*50, this.mousePosition.Y + rand.NextDouble()*50);
            //        dc.DrawEllipse(this.MouseBrush, null, tempPoint, MouseThickness, MouseThickness);
            //    }

            //    // prevent drawing outside of our render area
            //    //this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
            //}
        }
    }
}
