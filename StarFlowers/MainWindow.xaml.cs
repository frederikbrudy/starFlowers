using Particles;
using System;
using System.Collections;
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
using Microsoft.Kinect;
using System.IO;


namespace StarFlowers
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const DepthImageFormat DepthFormat = DepthImageFormat.Resolution640x480Fps30;
        private const ColorImageFormat ColorFormat = ColorImageFormat.RgbResolution640x480Fps30;                
        private KinectSensor sensor;
        // Bitmap mit Farbinformation
        private WriteableBitmap colorBitmap;
        // Bitmap mit Opacity-Maske
        private WriteableBitmap playerOpacityMaskImage;
        // Farbdaten von der Kamera
        private byte[] colorPixels;
        // Tiefendaten von der Kamera
        private DepthImagePixel[] depthPixels;
        private int[] greenScreenPixelData;
        // x- und y-Werte
        private ColorImagePoint[] colorCoordinates;
        // Groesse vom Tiefenbild
        private int depthWidth, depthHeight;

        private int opaquePixelValue = -1;

        private int colorToDepthDivisor;

        private Point playerCenterPoint; // Mittelpunkt des Shapes von einem Player

        private Brush centerPointColor;

        private DrawingGroup drawingGroup;
        private DrawingImage imageSource;
        
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
        private DrawingGroup drawingGroupMouse;

        private DrawingImage imageSourceMouse;

        private const int maxParticleCountPerSystem = 500;

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
            this.WindowStyle = WindowStyle.None;
            this.WindowState = WindowState.Maximized;
            this.Background = Brushes.Black;
            this.Cursor = System.Windows.Input.Cursors.None;

            drawingGroup = new DrawingGroup();
            imageSource = new DrawingImage(drawingGroup);
            OverlayImage.Source = imageSource;

            // Look through all sensors and start the first connected one.
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                //Status should e.g. not be "Initializing" or "NotPowered"
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    sensor = potentialSensor;
                    break;
                }
            }

            if (null != sensor)
            {
                sensor.DepthStream.Enable(DepthFormat);
                sensor.ColorStream.Enable(ColorFormat);
                sensor.SkeletonStream.Enable();

                depthWidth = sensor.DepthStream.FrameWidth;
                depthHeight = sensor.DepthStream.FrameHeight;

                int colorWidth = sensor.ColorStream.FrameWidth;
                int colorHeight = sensor.ColorStream.FrameHeight;

                colorToDepthDivisor = colorWidth / depthWidth;

                depthPixels = new DepthImagePixel[sensor.DepthStream.FramePixelDataLength];
                colorPixels = new byte[sensor.ColorStream.FramePixelDataLength];
                greenScreenPixelData = new int[sensor.DepthStream.FramePixelDataLength];
                colorCoordinates = new ColorImagePoint[sensor.DepthStream.FramePixelDataLength];
                colorBitmap = new WriteableBitmap(colorWidth, colorHeight, 96.0, 96.0, PixelFormats.Bgr32, null);
                MyImage.Source = colorBitmap;

                sensor.AllFramesReady += SensorAllFramesReady;

                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }
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
            this.setSpawnPoint(this.mousePosition);
            
        }

        private void setSpawnPoint(Point newPoint2D)
        {
            this.spawnPoint = new Point3D(newPoint2D.X - (this.Width / 2), (this.Height / 2) - newPoint2D.Y, 0.0);
        }

        private void initParticleSystem()
        {
            frameTimer = new System.Windows.Threading.DispatcherTimer();
            frameTimer.Tick += OnFrame;
            frameTimer.Interval = TimeSpan.FromSeconds(1.0 / 60.0);
            //frameTimer.Start();

            this.spawnPoint = new Point3D(0.0, 0.0, 0.0);
            this.lastTick = Environment.TickCount;

            pm = new ParticleSystemManager();

            //this.ParticleHost.Children.Add
            this.WorldModels.Children.Add(pm.CreateParticleSystem(maxParticleCountPerSystem, Colors.Gray));
            this.WorldModels.Children.Add(pm.CreateParticleSystem(maxParticleCountPerSystem, Colors.Red));
            this.WorldModels.Children.Add(pm.CreateParticleSystem(maxParticleCountPerSystem, Colors.Silver));
            this.WorldModels.Children.Add(pm.CreateParticleSystem(maxParticleCountPerSystem, Colors.Orange));
            this.WorldModels.Children.Add(pm.CreateParticleSystem(maxParticleCountPerSystem, Colors.Yellow));

            rand = new Random(this.GetHashCode());

            //this.Cursor = System.Windows.Input.Cursors.None;
        }

        private void initDrawingData()
        {
            // Create the drawing group we'll use for drawing
            this.drawingGroupMouse = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSourceMouse = new DrawingImage(this.drawingGroupMouse);

            // Display the drawing using our image control
            MouseImage.Source = this.imageSourceMouse;
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
            this.doFrameCalculation();
        }

        private void doFrameCalculation()
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
            //pm.SpawnParticle(this.spawnPoint, 10.0, Colors.Silver, particleSizeMultiplier * rand.NextDouble(), particleLifeMultiplier * rand.NextDouble());
            //pm.SpawnParticle(this.spawnPoint, 10.0, Colors.Gray, particleSizeMultiplier * rand.NextDouble(), particleLifeMultiplier * rand.NextDouble());
            //pm.SpawnParticle(this.spawnPoint, 10.0, Colors.Red, particleSizeMultiplier * rand.NextDouble(), particleLifeMultiplier * rand.NextDouble());
            //pm.SpawnParticle(this.spawnPoint, 10.0, Colors.Orange, particleSizeMultiplier * rand.NextDouble(), particleLifeMultiplier * rand.NextDouble());
            //pm.SpawnParticle(this.spawnPoint, 10.0, Colors.Silver, particleSizeMultiplier * rand.NextDouble(), particleLifeMultiplier * rand.NextDouble());
            //pm.SpawnParticle(this.spawnPoint, 10.0, Colors.Gray, particleSizeMultiplier * rand.NextDouble(), particleLifeMultiplier * rand.NextDouble());
            //pm.SpawnParticle(this.spawnPoint, 10.0, Colors.Red, particleSizeMultiplier * rand.NextDouble(), particleLifeMultiplier * rand.NextDouble());
            //pm.SpawnParticle(this.spawnPoint, 10.0, Colors.Yellow, particleSizeMultiplier * rand.NextDouble(), particleLifeMultiplier * rand.NextDouble());
            //pm.SpawnParticle(this.spawnPoint, 10.0, Colors.Silver, particleSizeMultiplier * rand.NextDouble(), particleLifeMultiplier * rand.NextDouble());
            //pm.SpawnParticle(this.spawnPoint, 10.0, Colors.Yellow, particleSizeMultiplier * rand.NextDouble(), particleLifeMultiplier * rand.NextDouble());

            double c = Math.Cos(this.totalElapsed);
            double s = Math.Sin(this.totalElapsed);

            //this.spawnPoint = new Point3D(s * 400.0, c * 200.0, 0.0);

            //using (DrawingContext dc = this.drawingGroup2.Open())
            //{
            //    // Draw a transparent background to set the render size
            //    dc.DrawRectangle(Brushes.Transparent, null, new Rect(0.0, 0.0, this.Width, this.Height));

            //    for (int i = 0; i < 100; i++)
            //    {
            //        Point tempPoint = new Point(this.mousePosition.X + rand.NextDouble()*50, this.mousePosition.Y + rand.NextDouble()*50);
            //        dc.DrawEllipse(this.MouseBrush, null, tempPoint, MouseThickness, MouseThickness);
            //    }

            //    // prevent drawing outside of our render area
            //    //this.drawingGroup2.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
            //}
        }

        private void SensorAllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            if (null == sensor)
            {
                return;
            }

            bool depthReceived = false;
            bool colorReceived = false;
            bool skeletonReceived = false;
            Skeleton[] skeletons = new Skeleton[0];

            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {
                if (null != depthFrame)
                {
                    depthFrame.CopyDepthImagePixelDataTo(depthPixels);
                    depthReceived = true;
                }
            }

            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (null != colorFrame)
                {
                    colorFrame.CopyPixelDataTo(colorPixels);
                    colorReceived = true;
                }
            }

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    Skeleton[] tempSkeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    List<Skeleton> trackedSkeletons = new List<Skeleton>();

                    skeletonFrame.CopySkeletonDataTo(tempSkeletons);
                    foreach (Skeleton potentialSkeleton in tempSkeletons )
                    {
                        if (potentialSkeleton.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            trackedSkeletons.Add(potentialSkeleton);
                        }
                    }
                    // in skeletons sind nun nur noch Skeletons mit Status "tracked"
                    skeletons = trackedSkeletons.ToArray();
                    skeletonReceived = true;
                }
            }

            if (true == depthReceived) // Tiefendaten vorhanden
            {
                
                // mindestens ein Skeleton gefunden
                bool skeletonTracked = (skeletonReceived == true
                                        && skeletons.Length > 0 
                                        && skeletons[0].TrackingState == SkeletonTrackingState.Tracked);

                // wenn Skeleton da: gruener Mittelpunkt, sonst Rot
                centerPointColor = (skeletonTracked == true) ? Brushes.Green : Brushes.Red;

                long playerXPoints = 0, playerYPoints = 0, playerPointCount = 0;

                bool somethingToTrackExists = false;

                sensor.CoordinateMapper.MapDepthFrameToColorFrame(DepthFormat,
                                                                     depthPixels,
                                                                     ColorFormat,
                                                                     colorCoordinates);

                Array.Clear(greenScreenPixelData, 0, greenScreenPixelData.Length);

                // iteriert ueber alle Zeilen und Spalten des Tiefenbilds
                for (int y = 0; y < depthHeight; ++y)
                {
                    for (int x = 0; x < depthWidth; ++x)
                    {
                        int depthIndex = x + (y * depthWidth);
                        DepthImagePixel depthPixel = depthPixels[depthIndex];

                        if (skeletonTracked)
                        {
                            int player = depthPixel.PlayerIndex;
                            somethingToTrackExists = (player > 0); // Pixel ist einem Spieler zugeordnet
                        }
                        else
                        {
                            somethingToTrackExists = (depthPixel.Depth > 500 && depthPixel.Depth < 2000);
                            // Pixel ist zwischen 0,8m und 2m entfernt
                        }

                        if (somethingToTrackExists)
                        {
                            ColorImagePoint colorImagePoint = colorCoordinates[depthIndex];
                            int colorInDepthX = colorImagePoint.X / colorToDepthDivisor;
                            int colorInDepthY = colorImagePoint.Y / colorToDepthDivisor;

                            if (colorInDepthX > 0 && colorInDepthX < depthWidth
                                && colorInDepthY > 0 && colorInDepthY < depthHeight)
                            {
                                if (!skeletonTracked)
                                {
                                    /* wenn kein Skeleton erkannt, alle Punkte aus dem Depth-Shape addieren,
                                     * um später Mittelpunkt zu berechnen */
                                    playerXPoints += colorInDepthX;
                                    playerYPoints += colorInDepthY;
                                    playerPointCount++;
                                }
                                    
                                int greenScreenIndex = colorInDepthX + (colorInDepthY * depthWidth);
                                greenScreenPixelData[greenScreenIndex] = opaquePixelValue;
                                greenScreenPixelData[greenScreenIndex - 1] = opaquePixelValue;
                            }
                        }
                    }
                } // end for-loops

                if (!skeletonTracked)
                {
                    // Mittelpunkt aus Depth Shape berechnen
                    if (playerPointCount > 0)
                    {
                        playerCenterPoint = new Point((double)playerXPoints / playerPointCount,
                                                        (double)playerYPoints / playerPointCount);
                    }
                    else
                    {
                        playerCenterPoint = new Point(0.0, 0.0);
                    }
                }
                else
                {
                    // Mittelpunkt als Position vom Skeleton definieren
                    foreach (Skeleton skel in skeletons)
                    {
                        if (skel.TrackingState == SkeletonTrackingState.Tracked
                            || skel.TrackingState == SkeletonTrackingState.PositionOnly)
                        {
                            playerCenterPoint = SkeletonPointToScreen(skel.Position);
                        }
                    }
                }

                this.doFrameCalculation();

            }

            if (colorReceived == true)
            {

                for (int x = 0; x < colorPixels.Length; x++)
                {
                    // setzt alle Pixel auf dunkelgrau statt dem Kamerabild
                    colorPixels[x] = (byte)50;
                }

                int centerPointPixel = ((int)playerCenterPoint.X + ((int)playerCenterPoint.Y * depthWidth));

                //Console.WriteLine("Länge colorPixels: " + colorPixels.Length);
                //Console.WriteLine("PlayerCenterPoint.X: " + playerCenterPoint.X);
                //Console.WriteLine("PlayerCenterPoint.Y: " + playerCenterPoint.Y);
                //Console.WriteLine("CenterPointPixel: " + centerPointPixel);
                //Console.WriteLine("------");

                using (DrawingContext dc = drawingGroup.Open()) 
                {
                     
                    dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, 640.0, 480.0));

                    dc.DrawEllipse(centerPointColor,
                                    null,
                                    playerCenterPoint,
                                    10,
                                    10);                
                }

                //drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, 640.0, 480.0));

                    colorBitmap.WritePixels(
                        new Int32Rect(0, 0, colorBitmap.PixelWidth, colorBitmap.PixelHeight),
                        colorPixels,
                        colorBitmap.PixelWidth * sizeof(int),
                        0);

                if (null == playerOpacityMaskImage)
                {
                    playerOpacityMaskImage = new WriteableBitmap(depthWidth,
                                                                    depthHeight,
                                                                    96,
                                                                    96,
                                                                    PixelFormats.Bgra32,
                                                                    null);

                    MyImage.OpacityMask = new ImageBrush(playerOpacityMaskImage);
                }

                playerOpacityMaskImage.WritePixels(
                    new Int32Rect(0, 0, depthWidth, depthHeight),
                    greenScreenPixelData,
                    depthWidth * ((playerOpacityMaskImage.Format.BitsPerPixel + 7) / 8),
                    0);
            }
        }

        private Point SkeletonPointToScreen(SkeletonPoint skeletonPoint)
        {
            DepthImagePoint depthPoint = sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skeletonPoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }
    }
}
