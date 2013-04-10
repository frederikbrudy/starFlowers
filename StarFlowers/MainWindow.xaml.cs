﻿using Particles;
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


        

        private DrawingGroup drawingGroup;
        private DrawingImage imageSource;
        
        /// <summary>
        /// dispatcher for frame events
        /// </summary>
        System.Windows.Threading.DispatcherTimer frameTimer;

        /// <summary>
        /// the spawn point of the particlesystems
        /// </summary>
        //private Point3D spawnPoint;

        private Dictionary<Color, Point3D> spawnPoints;

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
        //private Brush MouseBrush = Brushes.Blue;

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

        private List<Point3D>[] rightHandPoints = new List<Point3D>[2];
        private List<Point3D>[] leftHandPoints = new List<Point3D>[2];
        private List<Point3D>[] handCenterPoints = new List<Point3D>[2];
        private List<Point3D>[] playerCenterPoints = new List<Point3D>[2];
        private double[] handCenterThicknesses = new double[2];

        private Point playerCenterPoint; // Mittelpunkt des Shapes von einem Player
        Point rightHandPoint = new Point();
        Point leftHandPoint = new Point();
        Point handCenterPoint = new Point();
        private double HandCenterThickness = 10;

        private Color colorLeftHand = Colors.Orchid;
        private Color colorRightHand = Colors.Blue;
        private Color colorBodyCenter = Colors.Turquoise;
        private Color colorMousePoint = Colors.Green;
        private Color colorHandCenterPoint = Colors.Yellow;
        private Brush brushLeftHand;
        private Brush brushRightHand;
        private Brush brushMousePoint;
        private Brush brushBodyCenter;
        private Brush brushHandCenterPoint;

        /// <summary>
        /// brush for the center point of a tracked user
        /// </summary>
        private Brush brushCenterPoint;
        private Color colorCenterPointTracked = Colors.Green;
        private Color colorCenterPointNotTracked = Colors.Red;
        private Brush brushCenterPointTracked;
        private Brush brushCenterPointNotTracked;
        private bool trackingSkeleton;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            //make window as big as possible, not covering the task bar
            Rect size = System.Windows.SystemParameters.WorkArea;
            this.Width = size.Width;
            this.Height = size.Height;
            this.Left = size.Left;
            this.Top = size.Top;
            this.WindowStyle = WindowStyle.None;
            this.WindowState = WindowState.Maximized;
            this.Background = Brushes.Black;
            this.Cursor = System.Windows.Input.Cursors.None;

            this.initParticleSystem();
            this.initDrawingData();
            this.initBrushes();

            drawingGroup = new DrawingGroup();
            imageSource = new DrawingImage(drawingGroup);
            OverlayImage.Source = imageSource;

            //this.saveCurrentSize();

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
            //this.saveCurrentSize();
        }

        private void mouse_move(object sender, MouseEventArgs e)
        {
            //this.mousePosition = Mouse.GetPosition(this);
            //this.setMouseSpawnPoint(this.mousePosition);
        }

        private void setMouseSpawnPoint(Point newPoint2D)
        {
            this.spawnPoints[this.colorMousePoint] = this.pointTo3DPoint(newPoint2D);
        }

        /// <summary>
        /// converts a 2D point to its corresponding point in a 3D world
        /// </summary>
        /// <param name="point2D"></param>
        /// <returns></returns>
        private Point3D pointTo3DPoint(Point point2D){
            return new Point3D(point2D.X - (this.Width / 2), (this.Height / 2) - point2D.Y, 0.0);
        }

        /// <summary>
        /// Strecht a Kinect’s Depth Point to your window’s size
        /// </summary>
        /// <param name="depthPoint"></param>
        /// <returns></returns>
        private Point stretchDepthPointToScreen(Point depthPoint)
        {
            Point screenPoint = new Point();
            screenPoint.X = depthPoint.X * this.Width / 640.0;
            screenPoint.Y = depthPoint.Y * this.Height / 480.0;
            return screenPoint;
        }

        /// <summary>
        /// initializes the various particle systems
        /// </summary>
        private void initParticleSystem()
        {
            frameTimer = new System.Windows.Threading.DispatcherTimer();
            frameTimer.Tick += OnFrame;
            frameTimer.Interval = TimeSpan.FromSeconds(1.0 / 60.0);
            //frameTimer.Start();

            this.spawnPoints = new Dictionary<Color,Point3D>();
            this.lastTick = Environment.TickCount;

            pm = new ParticleSystemManager();

            this.WorldModels.Children.Add(pm.CreateParticleSystem(maxParticleCountPerSystem, this.colorBodyCenter));
            this.spawnPoints.Add(this.colorBodyCenter, new Point3D(0, 0, 0));

            this.WorldModels.Children.Add(pm.CreateParticleSystem(maxParticleCountPerSystem, this.colorLeftHand));
            this.spawnPoints.Add(this.colorLeftHand, new Point3D(0, 0, 0));
            this.WorldModels.Children.Add(pm.CreateParticleSystem(maxParticleCountPerSystem, this.colorRightHand));
            this.spawnPoints.Add(this.colorRightHand, new Point3D(0, 0,0));
            this.WorldModels.Children.Add(pm.CreateParticleSystem(maxParticleCountPerSystem, this.colorMousePoint));
            this.spawnPoints.Add(this.colorMousePoint, new Point3D(0, 0, 0));
            this.WorldModels.Children.Add(pm.CreateParticleSystem(maxParticleCountPerSystem, this.colorHandCenterPoint));
            this.spawnPoints.Add(this.colorHandCenterPoint, new Point3D(0, 0, 0));

            rand = new Random();

            
            for(int i = 0; i < this.handCenterThicknesses.Length; i++){
                this.playerCenterPoints[i] = new List<Point3D>();
                this.rightHandPoints[i] = new List<Point3D>();
                this.leftHandPoints[i] = new List<Point3D>();
                this.handCenterPoints[i] = new List<Point3D>();
                this.handCenterThicknesses[i] = 10.0;
            }

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

        private void initBrushes()
        {
            this.brushLeftHand = ColorsAndBrushes.getBrushFromColorRadialGradient(this.colorLeftHand);
            this.brushRightHand = ColorsAndBrushes.getBrushFromColorRadialGradient(this.colorRightHand);
            this.brushMousePoint = ColorsAndBrushes.getBrushFromColorRadialGradient(this.colorMousePoint);
            this.brushBodyCenter = ColorsAndBrushes.getBrushFromColorRadialGradient(this.colorBodyCenter);
            this.brushCenterPointTracked = ColorsAndBrushes.getBrushFromColorRadialGradient(this.colorCenterPointTracked);
            this.brushCenterPointNotTracked = ColorsAndBrushes.getBrushFromColorRadialGradient(this.colorCenterPointNotTracked);
            this.brushHandCenterPoint = ColorsAndBrushes.getBrushFromColorRadialGradient(this.colorHandCenterPoint);
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
            foreach (KeyValuePair<Color, Point3D> sp in this.spawnPoints)
            {
                // do something with entry.Value or entry.Key
                if (sp.Value.X > 0.01 || sp.Value.X < -0.01)
                {
                    if (sp.Key != this.colorBodyCenter || (sp.Key == this.colorBodyCenter && !this.trackingSkeleton))
                    {
                        pm.SpawnParticle(sp.Value, 10.0, sp.Key, particleSizeMultiplier * rand.NextDouble(), particleLifeMultiplier * rand.NextDouble());
                    }
                }
            }

            //setting spawn points
            //for (int screenCounter = 0; screenCounter < 1; screenCounter++)
            //{ //TODO
            for(int screenCounter = 0; screenCounter < 1; screenCounter++){
                //foreach (Point3D point in this.playerCenterPoints[screenCounter])
                //{
                //    //spawn the points
                //    if (point.X > 0.01 || point.X < -0.01)
                //    {
                //        ////only for body center
                //        //if (sp.Key != this.colorBodyCenter || (sp.Key == this.colorBodyCenter && !this.trackingSkeleton))
                //        //{
                //            pm.SpawnParticle(point, 10.0, this.colorBodyCenter, particleSizeMultiplier * rand.NextDouble(), particleLifeMultiplier * rand.NextDouble());
                //        //}

                //    }
                //}



                if (!this.trackingSkeleton)
                {
                    this.spawnThosePoints(this.playerCenterPoints[screenCounter], this.colorBodyCenter);
                }
                this.spawnThosePoints(this.rightHandPoints[screenCounter], this.colorRightHand);
                this.spawnThosePoints(this.leftHandPoints[screenCounter], this.colorLeftHand);
                this.spawnThosePoints(this.handCenterPoints[screenCounter], this.colorHandCenterPoint);

                //foreach (KeyValuePair<Color, Point3D> sp in this.spawnPoints)
                //{
                //    //spawn the points
                //    if (sp.Value.X > 0.01 || sp.Value.X < -0.01)
                //    {
                //        if (sp.Key != this.colorBodyCenter || (sp.Key == this.colorBodyCenter && !this.trackingSkeleton))
                //        {
                //            pm.SpawnParticle(sp.Value, 10.0, sp.Key, particleSizeMultiplier * rand.NextDouble(), particleLifeMultiplier * rand.NextDouble());
                //        }
                //    }
                //}
            }

        }

        private void spawnThosePoints(List<Point3D> list, Color color)
        {
            foreach (Point3D point in list)
            {
                //spawn the points
                if (point.X > 0.01 || point.X < -0.01)
                {
                    //for everything else
                    pm.SpawnParticle(point, 10.0, color, particleSizeMultiplier * rand.NextDouble(), particleLifeMultiplier * rand.NextDouble());
                }
            }
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
            
            int screenCounter = 0; //0 left screen, 1 right screen
            //do loop over all screens / kinects
            this.rightHandPoints[screenCounter].Clear();
            this.leftHandPoints[screenCounter].Clear();
            this.handCenterPoints[screenCounter].Clear();
            this.playerCenterPoints[screenCounter].Clear();

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
                brushCenterPoint = (skeletonTracked == true) ? this.brushCenterPointTracked : this.brushCenterPointNotTracked;
                this.trackingSkeleton = skeletonTracked;

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
                        if (skel.TrackingState == SkeletonTrackingState.PositionOnly)
                        {
                            playerCenterPoint = SkeletonPointToScreen(skel.Position);
                        }
                        else if (skel.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            playerCenterPoint = SkeletonPointToScreen(skel.Position);
                            JointCollection joints = skel.Joints;
                            // get the joint
                            Joint rightHand = skel.Joints[JointType.HandRight];
                            Joint leftHand = skel.Joints[JointType.HandLeft];
                            if (rightHand.TrackingState == JointTrackingState.Tracked
                                || rightHand.TrackingState == JointTrackingState.Inferred)
                            {
                                rightHandPoint = SkeletonPointToScreen(rightHand.Position);
                                this.rightHandPoints[screenCounter].Add(pointTo3DPoint(this.stretchDepthPointToScreen(rightHandPoint)));
                                this.spawnPoints[this.colorRightHand] = pointTo3DPoint(this.stretchDepthPointToScreen(rightHandPoint));
                            }

                            if (leftHand.TrackingState == JointTrackingState.Tracked
                                || leftHand.TrackingState == JointTrackingState.Inferred)
                            {
                                leftHandPoint = SkeletonPointToScreen(leftHand.Position);
                                this.leftHandPoints[screenCounter].Add(pointTo3DPoint(this.stretchDepthPointToScreen(leftHandPoint)));
                                this.spawnPoints[this.colorLeftHand] = pointTo3DPoint(this.stretchDepthPointToScreen(leftHandPoint));
                                
                            }

                            if (leftHandPoint != null && rightHandPoint != null)
                            {
                                handCenterPoint.X = leftHandPoint.X + Math.Abs(rightHandPoint.X - leftHandPoint.X) / 2;
                                handCenterPoint.Y = leftHandPoint.Y + Math.Abs(rightHandPoint.Y - leftHandPoint.Y) / 2;
                                handCenterPoint.X = (rightHandPoint.X + leftHandPoint.X) / 2;
                                handCenterPoint.Y = (rightHandPoint.Y + leftHandPoint.Y) / 2;

                                double distance = Math.Sqrt(Math.Pow(rightHandPoint.X - leftHandPoint.X, 2) + Math.Pow(rightHandPoint.Y - leftHandPoint.Y, 2));
                                this.HandCenterThickness = distance / 8;
                                this.handCenterThicknesses[screenCounter] = distance / 8;
                                this.handCenterPoints[screenCounter].Add(pointTo3DPoint(this.stretchDepthPointToScreen(handCenterPoint)));
                                this.spawnPoints[this.colorHandCenterPoint] = pointTo3DPoint(this.stretchDepthPointToScreen(handCenterPoint));
                                
                            }
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

                /*
                using (DrawingContext dc = drawingGroup.Open()) 
                {
                     
                    dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, 640.0, 480.0));

                    if (!this.trackingSkeleton)
                    {
                        dc.DrawEllipse(this.brushCenterPoint, null, playerCenterPoint, 10, 10);
                    }

                    if (handCenterPoint.X > 0.0)
                    {
                        dc.DrawEllipse(this.brushHandCenterPoint, null, handCenterPoint, HandCenterThickness, HandCenterThickness);
                    }
                    if (leftHandPoint.X > 0.0)
                    {
                        dc.DrawEllipse(this.brushLeftHand, null, leftHandPoint, MouseThickness, MouseThickness);
                    }
                    if (rightHandPoint.X > 0.0)
                    {
                        dc.DrawEllipse(this.brushRightHand, null, rightHandPoint, MouseThickness, MouseThickness);
                    }
                    
                }
                */

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

            if (playerCenterPoint != null)
            {
                if (!this.trackingSkeleton)
                {
                    this.playerCenterPoints[screenCounter].Add(this.pointTo3DPoint(this.stretchDepthPointToScreen(playerCenterPoint)));
                    this.spawnPoints[this.colorBodyCenter] = this.pointTo3DPoint(this.stretchDepthPointToScreen(playerCenterPoint));
                }
            }
        }

        private Point SkeletonPointToScreen(SkeletonPoint skeletonPoint)
        {
            DepthImagePoint depthPoint = sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skeletonPoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }
    }
}
