using System;
using System.Collections.Generic;
using System.Windows;
using System.Diagnostics;
using System.Threading;
using System.IO.MemoryMappedFiles;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using Microsoft.Kinect;
using Particles;
using System.Windows.Threading;

namespace StarFlowers
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool runningGameThread = false;

        public static List<KinectSensor> sensors; // alle angeschlossenen Sensoren
        List<Process> processes; // Prozesse der Kinect-Konsolenanwendung
        // Mutual Exclusion verhindert gleichzeitig Zugriff der Prozesse auf zu lesende Daten
        static Mutex mutex;
        // enthaelt zu lesende Daten, die die Prozesse generieren
        static MemoryMappedFile[] skeletonFiles;
        static MemoryMappedFile[] depthFiles;
        static MemoryMappedFile[] colorFiles;

        private const long SkeletonObjektByteLength = 2255;
        private const long ColorAndDepthPixelArrayByteLength = 1228800; // 640 x 480 x 4 bytes

        private const float RenderWidth = 640.0f;
        private const float RenderHeight = 480.0f;
        private const double JointThickness = 3;
        private const double BodyCenterThickness = 10;
        private const double ClipBoundsThickness = 10;
        
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

        /// <summary>
        /// dispatcher for frame events
        /// </summary>
        System.Windows.Threading.DispatcherTimer frameTimer;

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
        private const double particleSizeMultiplier = 10.0;
        private const double particleLifeMultiplier = 5.0;

        private List<Point3D>[] rightHandPoints = new List<Point3D>[2];
        private List<Point3D>[] leftHandPoints = new List<Point3D>[2];
        private List<Point3D>[] handCenterPoints = new List<Point3D>[2];
        private List<Point3D>[] playerCenterPoints = new List<Point3D>[2];
        private double[] handCenterThicknesses = new double[2];

        private System.Windows.Point playerCenterPoint; // Mittelpunkt des Shapes von einem Player
        System.Windows.Point rightHandPoint = new System.Windows.Point();
        System.Windows.Point leftHandPoint = new System.Windows.Point();

        private System.Windows.Media.Color colorLeftHand = Colors.Orchid;
        private System.Windows.Media.Color colorRightHand = Colors.Blue;
        private System.Windows.Media.Color colorBodyCenter = Colors.Turquoise;
        private System.Windows.Media.Color colorMousePoint = Colors.Green;
        private System.Windows.Media.Color colorHandCenterPoint = Colors.Yellow;
        private System.Windows.Media.Brush brushLeftHand;
        private System.Windows.Media.Brush brushRightHand;
        private System.Windows.Media.Brush brushMousePoint;
        private System.Windows.Media.Brush brushBodyCenter;
        private System.Windows.Media.Brush brushHandCenterPoint;

        /// <summary>
        /// brush for the center point of a tracked user
        /// </summary>
        private System.Windows.Media.Brush brushCenterPoint;
        private System.Windows.Media.Color colorCenterPointTracked = Colors.Green;
        private System.Windows.Media.Color colorCenterPointNotTracked = Colors.Red;
        private System.Windows.Media.Brush brushCenterPointTracked;
        private System.Windows.Media.Brush brushCenterPointNotTracked;
        private bool trackingSkeleton;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            
            this.initWindowSize();
            this.initParticleSystem();
            this.initDrawingData();
            this.initBrushes();
            this.initKinect();

            var mappedfileThread = new Thread(this.MemoryMapData);
            mappedfileThread.SetApartmentState(ApartmentState.STA);
            mappedfileThread.Start();
        }

        private void MemoryMapData()
        {
            this.runningGameThread = true;
            MemoryMappedViewAccessor accessor;

            object receivedSkeleton;
            DepthImagePixel[] receivedDepthPixels;
            byte[] receivedColorStream;

            skeletonFiles = new MemoryMappedFile[sensors.Count]; // es werden so viele MMFs wie angeschlossene Kinects erstellt
            depthFiles = new MemoryMappedFile[sensors.Count]; // es werden so viele MMFs wie angeschlossene Kinects erstellt
            colorFiles = new MemoryMappedFile[sensors.Count]; // es werden so viele MMFs wie angeschlossene Kinects erstellt
            try
            {
                Mutex mutex = new Mutex(true, "mappedfilemutex");
                mutex.ReleaseMutex(); //Freigabe gleich zu Beginn
            }
            catch (Exception e) { }

            for (int i = 0; i < sensors.Count; i++) // startet 2 Prozesse
            {
                skeletonFiles[i] = MemoryMappedFile.CreateNew("SkeletonExchange" + i, SkeletonObjektByteLength);
                depthFiles[i] = MemoryMappedFile.CreateNew("DepthExchange" + i, ColorAndDepthPixelArrayByteLength);
                colorFiles[i] = MemoryMappedFile.CreateNew("ColorExchange" + i, ColorAndDepthPixelArrayByteLength);

                Process p = new Process();
                p.StartInfo.CreateNoWindow = false; // laesst das Fenster fuer jeden Prozess angezeigt (fuer Debugging)
                p.StartInfo.FileName = Environment.CurrentDirectory + "\\..\\..\\..\\MultiProcessKinect\\bin\\Debug\\MultiProcessKinect.exe"; // die 
                Console.WriteLine(p.StartInfo.FileName);

                p.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Normal;
                p.StartInfo.Arguments = "" + i + " " + sensors[i].UniqueKinectId;
                p.Start();

                processes.Add(p); // zu Liste der Prozesse hinzufuegen
            }

            while (this.runningGameThread)
            {
                for (int x = 0; x < sensors.Count; x++) // fuer alle Kinects:
                {
                    receivedSkeleton = null;
                    receivedDepthPixels = new DepthImagePixel[ColorAndDepthPixelArrayByteLength / 4];
                    receivedColorStream = new byte[ColorAndDepthPixelArrayByteLength];
                    
                    // ------------- Skeleton holen ------------------                            
                    try
                    {
                        mutex = Mutex.OpenExisting("mappedfilemutex");
                        mutex.WaitOne(); // sichert alleinigen Zugriff

                        var receivedSkeletonBytes = new byte[SkeletonObjektByteLength]; // Byte-Array mit Laenge von serialized Skeleton
                        accessor = skeletonFiles[x].CreateViewAccessor(); // Reader fuer aktuelle MMF
                        accessor.ReadArray<byte>(0, receivedSkeletonBytes, 0, receivedSkeletonBytes.Length); // liest Nachricht von Konsolenanwendung

                        receivedSkeleton = ByteArrayToObject(receivedSkeletonBytes); // wandelt sie in Object oder null um

                        if (receivedSkeleton != null)
                        {
                            Console.WriteLine("Tracking ID:   " + ((Skeleton)receivedSkeleton).TrackingId);
                        }
                        else
                        {
                            Console.WriteLine("Kein Skeleton empfangen!");
                        }
                        Console.WriteLine("------------");

                        mutex.ReleaseMutex(); // gibt Mutex wieder frei

                    }
                    catch (Exception ex)
                    {
                        mutex.ReleaseMutex(); // gibt Mutex wieder frei
                        foreach (Process p in processes)
                        {
                            p.Kill(); // beendet alle Konsolenanwendungen
                        }
                    }

                    if (true) // ehem. if !skeletonFound, aber jetzt brauchen wir immer den depthStream
                    {
                        // ------------- DepthStream holen ------------------
                        try
                        {
                            mutex = Mutex.OpenExisting("mappedfilemutex");
                            mutex.WaitOne();
                            receivedDepthPixels = new DepthImagePixel[307200]; // 307200 = 640 x 480

                            accessor = depthFiles[x].CreateViewAccessor(); // Reader fuer aktuelle MMF
                            accessor.ReadArray<DepthImagePixel>(0, receivedDepthPixels, 0, receivedDepthPixels.Length); // liest Nachricht von Konsolenanwendung
                            mutex.ReleaseMutex();

                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Fehler beim Holen des DepthStreams" + ex.ToString());
                            //colorMutex.ReleaseMutex(); // gibt Mutex wieder frei
                            mutex.ReleaseMutex(); // gibt Mutex wieder frei
                            foreach (Process p in processes)
                            {
                                p.Kill(); // beendet alle Konsolenanwendungen
                            }
                        }
                    }

                    // ------------- ColorStream holen ------------------
                    if (true) // ehem if skeletonFound, aber jetzt brauchen wir den ColorStream für das Keying
                    {
                        try
                        {
                            mutex = Mutex.OpenExisting("mappedfilemutex");
                            mutex.WaitOne();
                            receivedColorStream = new byte[ColorAndDepthPixelArrayByteLength]; // Byte-Array mit Laenge von serialized Skeleton
                            accessor = colorFiles[x].CreateViewAccessor(); // Reader fuer aktuelle MMF
                            accessor.ReadArray<byte>(0, receivedColorStream, 0, receivedColorStream.Length); // liest Nachricht von Konsolenanwendung

                            mutex.ReleaseMutex();

                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Fehler beim Zeichnen" + ex.ToString());
                            mutex.ReleaseMutex(); // gibt Mutex wieder frei
                            foreach (Process p in processes)
                            {
                                p.Kill(); // beendet alle Konsolenanwendungen
                            }
                        }
                    }

                    Dispatcher.Invoke(DispatcherPriority.Send, new Action(() => processSensorData(x, receivedSkeleton, receivedDepthPixels, receivedColorStream)));
                    
                }

                Thread.Sleep(25); // wartet ein bisschen
            }

        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.shutDown();
        }

        private void shutDown()
        {
            this.runningGameThread = false; // beendet den Empfangs-Thread
            foreach (Process p in processes)
            {
                p.Kill(); // beendet auch die Konsolenanwendungen
            }
            System.Windows.Application.Current.Shutdown();
        }

        private void key_down(object sender, KeyEventArgs e)
        {
            if (e.Key.Equals(System.Windows.Input.Key.Escape))
            {
                this.shutDown();
            }
            else if (e.Key.Equals(System.Windows.Input.Key.F11))
            {
                WindowState = (WindowState == WindowState.Maximized) ? WindowState = WindowState.Normal : WindowState = WindowState.Maximized;
            }
            else
            {
                Console.WriteLine("key down: " + e.Key);
            }
        }

        private void WindowSizeChanged(object sender, SizeChangedEventArgs e) {}

        private void mouse_move(object sender, MouseEventArgs e) {}

        /// <summary>
        /// converts a 2D point to its corresponding point in a 3D world
        /// </summary>
        /// <param name="point2D"></param>
        /// <returns></returns>
        private Point3D pointTo3DPoint(System.Windows.Point point2D)
        {
            return new Point3D(point2D.X - (this.Width / 2), (this.Height / 2) - point2D.Y, 0.0);
        }

        /// <summary>
        /// Strecht a Kinect’s Depth Point to your window’s size
        /// </summary>
        /// <param name="depthPoint"></param>
        /// <returns></returns>
        private System.Windows.Point stretchDepthPointToScreen(System.Windows.Point depthPoint)
        {
            System.Windows.Point screenPoint = new System.Windows.Point();
            screenPoint.X = depthPoint.X * this.Width / 640.0;
            screenPoint.Y = depthPoint.Y * this.Height / 480.0;
            return screenPoint;
        }

        private System.Windows.Point SkeletonPointToScreen(SkeletonPoint skeletonPoint)
        {
            DepthImagePoint depthPoint = sensors[0].CoordinateMapper.MapSkeletonPointToDepthPoint(skeletonPoint, DepthImageFormat.Resolution640x480Fps30);
            return new System.Windows.Point(depthPoint.X, depthPoint.Y);
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

            //this.spawnPoints = new Dictionary<Color,Point3D>();
            this.lastTick = Environment.TickCount;

            pm = new ParticleSystemManager();

            this.WorldModels.Children.Add(pm.CreateParticleSystem(maxParticleCountPerSystem, this.colorBodyCenter));

            this.WorldModels.Children.Add(pm.CreateParticleSystem(maxParticleCountPerSystem, this.colorLeftHand));
            this.WorldModels.Children.Add(pm.CreateParticleSystem(maxParticleCountPerSystem, this.colorRightHand));
            this.WorldModels.Children.Add(pm.CreateParticleSystem(maxParticleCountPerSystem, this.colorMousePoint));
            this.WorldModels.Children.Add(pm.CreateParticleSystem(maxParticleCountPerSystem, this.colorHandCenterPoint));

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

        private void initWindowSize()
        {
            this.WindowStyle = WindowStyle.None;
            this.WindowState = WindowState.Maximized;
            this.Background = System.Windows.Media.Brushes.Black;
            this.Cursor = System.Windows.Input.Cursors.None;

            Rect size = System.Windows.SystemParameters.WorkArea; //size of the display, minus the task bar
            double primaryWidth = System.Windows.SystemParameters.FullPrimaryScreenWidth;
            double desiredWidth = primaryWidth;
            desiredWidth = 1366; //1366 , 3412 , 1706
            //this.Width = size.Width;
            //this.Height = size.Height;
            this.Width = desiredWidth;
            this.Height = 480;
            this.Left = size.Left;
            this.Top = size.Top;

            double scaleFactor = 1 / (desiredWidth / primaryWidth);
            this.Grid.LayoutTransform = new ScaleTransform(scaleFactor, 1);
            Console.WriteLine("primaryWidth: " + primaryWidth + ", desiredWidth: " + desiredWidth
                + ", this.Width: " + this.Width + ", scaleFactor: " + scaleFactor + ", this.ActualWidth: " + this.ActualWidth);

        }

        private void initKinect()
        {
            processes = new List<Process>();
            sensors = new List<KinectSensor>();
            // Look through all sensors and start the first connected one.
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                //Status should e.g. not be "Initializing" or "NotPowered"
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    sensors.Add(potentialSensor); 
                }
            }

            depthWidth = 640;
            depthHeight = 480;

            int colorWidth = 640;
            int colorHeight = 480;

            colorToDepthDivisor = colorWidth / depthWidth;

            depthPixels = new DepthImagePixel[ColorAndDepthPixelArrayByteLength/4];
            colorPixels = new byte[ColorAndDepthPixelArrayByteLength];
            greenScreenPixelData = new int[ColorAndDepthPixelArrayByteLength/4];
            colorCoordinates = new ColorImagePoint[ColorAndDepthPixelArrayByteLength/4];
            colorBitmap = new WriteableBitmap(colorWidth, colorHeight, 96.0, 96.0, PixelFormats.Bgr32, null);
            MyImage.Source = colorBitmap;
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

            //setting spawn points
            //for (int screenCounter = 0; screenCounter < 1; screenCounter++)
            //{ //TODO
            for(int screenCounter = 0; screenCounter < 1; screenCounter++){
                if (!this.trackingSkeleton)
                {
                    //not tracking skel. should spawn at body center
                    this.spawnThosePoints(this.playerCenterPoints[screenCounter], this.colorBodyCenter);
                }
                this.spawnThosePoints(this.rightHandPoints[screenCounter], this.colorRightHand);
                this.spawnThosePoints(this.leftHandPoints[screenCounter], this.colorLeftHand);
                this.spawnThosePoints(this.handCenterPoints[screenCounter], this.colorHandCenterPoint);
            }
        }

        private void spawnThosePoints(List<Point3D> list, System.Windows.Media.Color color)
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

        void DrawImage(WriteableBitmap image, byte[] imageBytes)
        {
            image.WritePixels(
                new Int32Rect(0, 0, 640, 480),
                imageBytes,
                image.PixelWidth * sizeof(int),
                0);
        }

        CroppedBitmap GetCroppedImage(int imagePos, byte[] imageBytes, int xPos, int yPos, int size)
        {
            WriteableBitmap wb = new WriteableBitmap(640, 480, 96.0, 96.0, PixelFormats.Bgr32, null);
            wb.WritePixels(
                new Int32Rect(0, 0, 640, 480),
                imageBytes,
                wb.PixelWidth * sizeof(int),
                0);
            int cropXPos = xPos - size / 2;
            int cropYPos = yPos - size / 2;

            if (cropXPos < 0) cropXPos = 0;
            if (cropXPos > 640 - size) cropXPos = 640 - size;
            if (cropYPos < 0) cropYPos = 0;
            if (cropYPos > 480 - size) cropYPos = 480 - size;

            return new CroppedBitmap(wb, new Int32Rect(cropXPos, cropYPos, size, size));
        }

        int euklDistanz(int x1, int y1, int x2, int y2)
        {
            double a = (double)(x2 - x1);
            double b = (double)(y2 - y1);
            return (int)(Math.Sqrt(a * a + b * b));
        }

        // deserialisiert byte-Array in Object
        public static object ByteArrayToObject(byte[] _ByteArray)
        {
            try
            {
                MemoryStream ms = new MemoryStream(_ByteArray);
                BinaryFormatter bf = new BinaryFormatter();
                ms.Position = 0;
                return bf.Deserialize(ms);
            }
            catch (Exception _Exception)
            {
                Console.WriteLine("Kein Skelett übertragen.");
            }

            // Error occured, return null
            return null;
        }

        private void processSensorData(int screenCounter, object skeletonData, DepthImagePixel[] depthStream, byte[] colorStream)
        {
            // Variablen für Head Tracking
            int kopfXPos = 0;
            int kopfYPos = 0;
            int distanzKopfZuHals = 0;
            
            this.rightHandPoints[screenCounter].Clear();
            this.leftHandPoints[screenCounter].Clear();
            this.handCenterPoints[screenCounter].Clear();
            this.playerCenterPoints[screenCounter].Clear();
            
            bool skeletonTracked = false;
            Skeleton skeleton = new Skeleton();                     
   
            if (skeletonData != null)
            {
                skeletonTracked = true;
                skeleton = (Skeleton)(skeletonData); // Tadaa: hier ist das Skeleton
            }

            // wenn Skeleton da: gruener Mittelpunkt, sonst Rot
            brushCenterPoint = (skeletonTracked == true) ? this.brushCenterPointTracked : this.brushCenterPointNotTracked;
            this.trackingSkeleton = skeletonTracked;
            long playerXPoints = 0, playerYPoints = 0, playerPointCount = 0;
            bool somethingToTrackExists = false;

            sensors[0].CoordinateMapper.MapDepthFrameToColorFrame(DepthFormat,
                                                                    depthStream,
                                                                    ColorFormat,
                                                                    colorCoordinates);


            // --------- Overlay aus Tiefenbild generieren ------------
            Array.Clear(greenScreenPixelData, 0, greenScreenPixelData.Length);

            for (int y = 0; y < depthHeight; ++y)
            {
                for (int x = 0; x < depthWidth; ++x)
                {
                    int depthIndex = x + (y * depthWidth);
                    DepthImagePixel depthPixel = depthStream[depthIndex];

                    if (skeletonTracked)
                    {
                        int player = depthPixel.PlayerIndex;
                        somethingToTrackExists = (player > 0); // Pixel ist einem Spieler zugeordnet
                    }
                    else
                    {
                        somethingToTrackExists = (depthPixel.Depth > 500 && depthPixel.Depth < 2500);
                        // Pixel ist zwischen 0,5m und 2,5m entfernt
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
            }

            // ------- Punkte zeichnen (Body Center und ggfs. Hände) ------------

            if (!skeletonTracked) // nur Tiefendaten verfügbar, kein Skeleton
            {
                if (playerPointCount > 0) // wenn überhaupt etwas im Zielbereich getrackt wurde
                {
                    // Mittelpunkt aus Depth Shape berechnen
                    playerCenterPoint = new System.Windows.Point((double)playerXPoints / playerPointCount,
                                                    (double)playerYPoints / playerPointCount);
                }
                else // wenn gar nichts getrackt wurde
                {
                    // Mittelpunkt ist ganz oben links
                    playerCenterPoint = new System.Windows.Point(0.0, 0.0);
                }
            }
            else // Skeleton verfügbar
            {     
                if (skeleton.TrackingState == SkeletonTrackingState.PositionOnly)
                {
                    // wenn nur Position vorhanden, dann diese als Mittelpunkt nehmen
                    playerCenterPoint = SkeletonPointToScreen(skeleton.Position);
                }
                else if (skeleton.TrackingState == SkeletonTrackingState.Tracked)
                {
                    // wenn auch Bones erkannt wurden
                    
                    playerCenterPoint = SkeletonPointToScreen(skeleton.Position);

                    // Position von den Händen holen und Punkte dafür hinzufügen
                    JointCollection joints = skeleton.Joints;
                    // get the joint
                    Joint rightHand = skeleton.Joints[JointType.HandRight];
                    Joint leftHand = skeleton.Joints[JointType.HandLeft];
                    if (rightHand.TrackingState == JointTrackingState.Tracked
                        || rightHand.TrackingState == JointTrackingState.Inferred)
                    {
                        rightHandPoint = SkeletonPointToScreen(rightHand.Position);
                        this.rightHandPoints[screenCounter].Add(pointTo3DPoint(this.stretchDepthPointToScreen(rightHandPoint)));
                    }

                    if (leftHand.TrackingState == JointTrackingState.Tracked
                        || leftHand.TrackingState == JointTrackingState.Inferred)
                    {
                        leftHandPoint = SkeletonPointToScreen(leftHand.Position);
                        this.leftHandPoints[screenCounter].Add(pointTo3DPoint(this.stretchDepthPointToScreen(leftHandPoint)));
                    }

                    if (rightHandPoint != null && this.stretchDepthPointToScreen(rightHandPoint).Y > this.Height * 0.9)
                    {
                        Console.WriteLine("seeding for right hand on skel " + screenCounter);
                    }

                    if (leftHandPoint != null && this.stretchDepthPointToScreen(leftHandPoint).Y > this.Height * 0.9)
                    {
                        Console.WriteLine("seeding for left hand on skel " + screenCounter);
                    }

                    // wenn beide Hände erkannt wurden, zusätzlich Mittelpunkt zwischen den Händen anzeigen
                    if (leftHandPoint != null && rightHandPoint != null)
                    {
                        System.Windows.Point handCenterPoint = new System.Windows.Point((rightHandPoint.X + leftHandPoint.X) / 2,
                                                                                        (rightHandPoint.Y + leftHandPoint.Y) / 2);

                        double distance = Math.Sqrt(Math.Pow(rightHandPoint.X - leftHandPoint.X, 2) 
                                                    + Math.Pow(rightHandPoint.Y - leftHandPoint.Y, 2));

                        this.handCenterThicknesses[screenCounter] = distance / 8;
                        this.handCenterPoints[screenCounter].Add(pointTo3DPoint(this.stretchDepthPointToScreen(handCenterPoint)));
                    }
                }

                // Head tracking, wenn Skeleton verfügbar

       
                Joint halsJoint = skeleton.Joints[JointType.ShoulderCenter];
                Joint kopfJoint = skeleton.Joints[JointType.Head];
                if (
                    (halsJoint.TrackingState == JointTrackingState.Tracked
                    || halsJoint.TrackingState == JointTrackingState.Inferred)
                  && (kopfJoint.TrackingState == JointTrackingState.Tracked
                    || kopfJoint.TrackingState == JointTrackingState.Inferred))
                {

                    int kopfX = (int)(this.SkeletonPointToScreen(kopfJoint.Position).X);
                    int kopfY = (int)(this.SkeletonPointToScreen(kopfJoint.Position).Y);
                    int halsX = (int)(this.SkeletonPointToScreen(halsJoint.Position).X);
                    int halsY = (int)(this.SkeletonPointToScreen(halsJoint.Position).Y);

                    kopfXPos = (kopfX + halsX) / 2;
                    kopfYPos = (kopfY + halsY) / 2;

                    distanzKopfZuHals = euklDistanz(kopfX, kopfY, halsX, halsY);

                    HeadTrackedImage.Source = GetCroppedImage(screenCounter, colorStream, kopfXPos, kopfYPos, distanzKopfZuHals * 2);
                }
                 
                    
            }

            // ------- Overlay zeichnen ------------

            for (int x = 0; x < colorStream.Length; x++)
            {
                // setzt alle Pixel auf dunkelgrau statt dem Kamerabild
                colorStream[x] = (byte)80;
            }

            int centerPointPixel = ((int)playerCenterPoint.X + ((int)playerCenterPoint.Y * depthWidth));

            colorBitmap.WritePixels(
                new Int32Rect(0, 0, colorBitmap.PixelWidth, colorBitmap.PixelHeight),
                colorStream,
                colorBitmap.PixelWidth * sizeof(int),
                0);

            if (null == playerOpacityMaskImage)
            {
                playerOpacityMaskImage = new WriteableBitmap(depthWidth, depthHeight, 96, 96, PixelFormats.Bgra32, null);
                MyImage.OpacityMask = new ImageBrush(playerOpacityMaskImage);
            }

            playerOpacityMaskImage.WritePixels(
                new Int32Rect(0, 0, depthWidth, depthHeight),
                greenScreenPixelData,
                depthWidth * ((playerOpacityMaskImage.Format.BitsPerPixel + 7) / 8),
                0);

            if (playerCenterPoint != null)
            {
                if (!this.trackingSkeleton)
                {
                    this.playerCenterPoints[screenCounter].Add(this.pointTo3DPoint(this.stretchDepthPointToScreen(playerCenterPoint)));
                }
            }

            this.doFrameCalculation();
        }

    }
}
