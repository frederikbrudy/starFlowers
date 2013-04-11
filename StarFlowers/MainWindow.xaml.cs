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
using System.Windows.Controls;

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
        private WriteableBitmap[] colorBitmaps = new WriteableBitmap[2];
        // Bitmap mit Opacity-Maske
        private WriteableBitmap[] playerOpacityMaskImages = new WriteableBitmap[2];

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
        //System.Windows.Threading.DispatcherTimer frameTimer;

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

        private Random random;

        /// <summary>
        /// Thickness of body center ellipse
        /// </summary>
        //private const double MouseThickness = 10;

        /// <summary>
        /// Drawing group for mouse position output
        /// </summary>
        //private DrawingGroup drawingGroupMouse;

        //private DrawingImage imageSourceMouse;

        private const int maxParticleCountPerSystem = 500;
        private const double particleSizeMultiplier = 10.0;
        private const double particleLifeMultiplier = 5.0;

        private List<Point3D>[] rightHandPoints = new List<Point3D>[2];
        private List<Point3D>[] leftHandPoints = new List<Point3D>[2];
        private List<Point3D>[] handCenterPoints = new List<Point3D>[2];
        private List<Point3D>[] playerCenterPoints = new List<Point3D>[2];
        private double[] handCenterThicknesses = new double[2];

        private Image[] OverlayImages = new Image[2];
        private Image[] HeadTrackingImages = new Image[2];

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



        /**
         * begin flower area
         * */
        /// <summary>
        /// The number of sprites in each row
        /// </summary>
        int ximages = 10;

        /// <summary>
        /// The number of sprites in each column
        /// </summary>
        int yimages = 12;

        /// <summary>
        /// number of frames per sprite animation. must be the same for each animation.
        /// </summary>
        int numberFrames = -1;

        /// <summary>
        /// width of a single sprite frame
        /// </summary>
        int spriteWidth = 600;

        /// <summary>
        /// height of a single sprite frame
        /// </summary>
        int spriteHeight = 480;

        /// <summary>
        /// the array which holds all sprite images. first index shows references an individual sprite animation, 
        /// second index points to the frames in a specific animation
        /// </summary>
        BitmapSource[,] spriteImages;

        /// <summary>
        /// overall frame count. 
        /// </summary>
        int currentFrameCount;

        /// <summary>
        /// dispatcher for frame events
        /// </summary>
        //System.Windows.Threading.DispatcherTimer frameTimer;

        /// <summary>
        /// xpositions of newly created seeding points. on each frame these seeds will be planted and this list will be cleared.
        /// </summary>
        List<int> seedPosition;

        /// <summary>
        /// colors of newly created seeding points. on each frame these seeds will be planted and this list will be cleared.
        /// the index in this list is corresponding to the ones in seedPosition
        /// </summary>
        List<Color> seedColor;

        /// <summary>
        /// List of currently available (thus unassigned) sprite containers. there will be a maximum of maximumPlantCount containers 
        /// added to the application
        /// </summary>
        List<Plant> availableSpriteContainers;

        /// <summary>
        /// list of sprite containers, which are currently in use. they will be redrawn on each frame
        /// </summary>
        List<Plant> activePlants;

        /// <summary>
        /// maximum plant count, meaning the maximum sprite animations which is possible in this application
        /// </summary>
        private int maximumPlantCount;

        /// <summary>
        /// number of currently available containers, indicating how many sprite containers are free to use.
        /// </summary>
        //private int currentlyAvailableContainersCount;

        /// <summary>
        /// number of how many different sprite animations there are. 
        /// </summary>
        private int spriteCountUnique;

        private int xOffset;

        /// <summary>
        /// indicates if the entire window should be flashing in different colors
        /// </summary>
        private bool alertBlinking = false;

        /// <summary>
        /// indicates the length of the remaining countdown of the flashing window
        /// </summary>
        private int alertBlinkingSeconds;

        /// <summary>
        /// indicates whether the trigger area at the bottom of the screen should be flashing
        /// </summary>
        private bool triggerAreaBlinking;
        private double triggerDiff;
        private int xOffsetDiff;

        /// <summary>
        /// the list of soon to be removed plants. these plants will be removed on the next frame.
        /// </summary>
        private List<Plant> removeCandidates;

        /// <summary>
        /// indicates the global sprite counter, gives the next available sprite index for a new plant
        /// </summary>
        private int spriteIndexGlobal = 0;

        private bool resetAvailable;
        private DateTime oldTime;
        private const int frameTime = 30;

        /*
         * end flower area
         * */

        public MainWindow()
        {
            InitializeComponent();
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            this.initSprites();
            this.initTriggerArea();

            this.initWindowSize();
            this.initParticleSystem();
            this.initDrawingData();
            this.initBrushes();
            this.initKinect();

            var mappedfileThread = new Thread(this.MemoryMapData);
            mappedfileThread.SetApartmentState(ApartmentState.STA);
            mappedfileThread.Start();

            var plantThread = new Thread(this.doPlantDrawing);
            plantThread.SetApartmentState(ApartmentState.STA);
            plantThread.Start();
        }


        /// <summary>
        /// initializes / resets and repositions the trigger area to its default values.
        /// </summary>
        private void initTriggerArea()
        {
            this.TriggerArea.Fill = ColorsAndBrushes.getBrushFromColorLinearGradient(Colors.Red);
            this.TriggerArea.Opacity = 0.0;
            this.TriggerArea.Width = this.Width;
            this.TriggerArea.Margin = new Thickness(0, this.Height - this.TriggerArea.Height, 0, 0);
        }

        /// <summary>
        /// initializes the sprites: loads the images, creates empty sprite containers, etc. 
        /// </summary>
        private void initSprites()
        {
            //frameTimer = new System.Windows.Threading.DispatcherTimer();
            //frameTimer.Tick += OnFrame;
            //frameTimer.Interval = TimeSpan.FromSeconds(1.0 / frameTime);

            String path = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase) + "\\..\\..\\Images\\";
            path = path.Substring(6);
            String[] spriteFilenames = new String[5];
            spriteFilenames[0] = path + "Baum1\\";
            spriteFilenames[1] = path + "Baum2\\";
            spriteFilenames[2] = path + "Blume1\\";
            spriteFilenames[3] = path + "Gras1\\";
            spriteFilenames[4] = path + "Gras2\\";

            this.spriteImages = new BitmapSource[spriteFilenames.Length, ximages * yimages];

            this.createCroppedImages(spriteFilenames);

            this.availableSpriteContainers = new List<Plant>();

            this.addSpriteContainer();

            this.activePlants = new List<Plant>();

            this.spriteCountUnique = this.spriteImages.Length / this.numberFrames;
            this.maximumPlantCount = this.spriteCountUnique * 1;

            //init seeds
            this.seedPosition = new List<int>();
            this.seedColor = new List<Color>();

            this.removeCandidates = new List<Plant>();

            
        }

        /// <summary>
        /// adds an empty image as a child to the main grid of the application
        /// </summary>
        private void addSpriteContainer()
        {
            Image img = new Image();
            img.Width = 600;
            img.Height = 480;
            img.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            img.VerticalAlignment = System.Windows.VerticalAlignment.Top;
            Plant tempPlant = new Plant(img, spriteIndexGlobal++);
            this.FlowerGrid.Children.Add(img);
            this.availableSpriteContainers.Add(tempPlant);
        }

        /// <summary>
        /// adds more than one sprite containers to the grid parent.
        /// </summary>
        /// <param name="numberContainers"></param>
        private void addSpriteContainer(int numberContainers)
        {
            for (int i = 0; i < numberContainers; i++)
            {
                this.addSpriteContainer();
            }
        }

        /// <summary>
        /// loads the sprite images. the images must be organized in folders (one folder per sprite animation) 
        /// numbered sequentially.
        /// </summary>
        /// <param name="spriteFilenames">array of folder names (each with a trailing /)</param>
        private void createCroppedImages(String[] spriteFilenames)
        {
            this.numberFrames = this.ximages * this.yimages;
            for (int spriteCount = 0; spriteCount < spriteFilenames.Length; spriteCount++)
            {
                Console.WriteLine("loading image folder: " + spriteFilenames[spriteCount]);
                string[] files = Directory.GetFiles(spriteFilenames[spriteCount]);
                for (int frameCount = 0; frameCount < files.Length; frameCount++)
                {
                    String file = files[frameCount];
                    this.spriteImages[spriteCount, frameCount] = new BitmapImage(new Uri(file));
                }
                Console.WriteLine("loading done");
            }
        }

        private void toggleFrameTimer(bool forceRunning)
        {
            //if (frameTimer.IsEnabled && !forceRunning)
            //{
            //    frameTimer.Stop();
            //}
            //else
            //{
            //    frameTimer.Start();
            //}
        }


        /// <summary>
        /// seeds new plants from previously seeded seeds. if there are no seeds to plant available this method return without changes. 
        /// </summary>
        private void seedSeeds()
        {
            if (this.seedPosition.Count != 0)
            {
                //only start to grow the plants if they have been seeded previously
                for (int seed = this.seedPosition.Count - 1; seed >= 0; seed--)
                {
                    Console.WriteLine("seed position: " + seed + ", this.seedPosition.Count: " + this.seedPosition.Count);
                    //get all planted seeds and add them to the growing list.
                    int xPos = this.seedPosition[seed];
                    Color color = this.seedColor[seed];
                    //remove from the waiting list

                    Plant tempPlant = this.availableSpriteContainers[0];
                    Image spriteImg = tempPlant.Img;
                    this.availableSpriteContainers.RemoveAt(0);
                    spriteImg.Margin = new Thickness(xPos, this.Height, 0, 0);
                    spriteImg.Height = 0;
                    //Random rand = new Random();
                    double opacity = random.NextDouble() + 0.25;
                    opacity = (opacity > 0.9) ? (0.75) : opacity;
                    spriteImg.Opacity = opacity;
                    this.activePlants.Add(tempPlant);

                    //in onFrame: grow only plants in the growing list.
                }
                this.seedPosition.Clear();
                this.seedColor.Clear();
            }
        }

        /// <summary>
        /// seed a new plant at a certain x Position. seeds will be planted upon the next frame. 
        /// </summary>
        /// <param name="positionX">the horizontal seeding position</param>
        /// <param name="color">the plants color</param>
        private void queueSeed(int positionX, Color color)
        {
            int futurePlantCount = this.activePlants.Count + this.seedPosition.Count + 1;
            if (this.availableSpriteContainers.Count < futurePlantCount //there are not enough containers for all future plants
                && this.maximumPlantCount >= futurePlantCount) //there is still enough capacity for more plants
            {
                //add more capacity for images
                Console.WriteLine("adding more capacity for images");
                this.addSpriteContainer();
            }
            if (futurePlantCount <= this.maximumPlantCount)
            {
                //only seed plant if there are any seeding spots (meaning sprites) left
                this.seedPosition.Add(positionX);
                this.seedColor.Add(color);
            }
        }

        private void blinkAlert(int seconds)
        {
            this.alertBlinking = true;
            this.alertBlinkingSeconds = seconds;
            this.FlowerGrid.Width = this.Width;
            this.FlowerGrid.Height = this.Height;
            this.FlowerGrid.Background = Brushes.Red;
            //TODO clear images and reset to default values
        }

        private void blinkTriggerAreaToggle(bool blinking)
        {
            this.triggerAreaBlinking = blinking;
        }

        private void fadeTriggerArea()
        {
            if (this.triggerAreaBlinking)
            {
                if (this.TriggerArea.Opacity < 0.1)
                {
                    this.triggerDiff = 0.05;
                }
                else if (this.TriggerArea.Opacity > 0.9)
                {
                    this.triggerDiff = -0.05;
                }

                this.TriggerArea.Opacity += this.triggerDiff;
            }
        }

        /// <summary>
        /// flashes the entire window fast in different colors
        /// </summary>
        private void blinkWindow()
        {
            if (this.currentFrameCount % 5 == 0)
            {
                //switch background color
                if (this.FlowerGrid.Background.Equals(Brushes.Red))
                {
                    this.FlowerGrid.Background = Brushes.Black;
                }
                else if (this.FlowerGrid.Background.Equals(Brushes.Black))
                {
                    this.FlowerGrid.Background = Brushes.Green;
                }
                else if (this.FlowerGrid.Background.Equals(Brushes.Green))
                {
                    this.FlowerGrid.Background = Brushes.Blue;
                }
                else
                {
                    this.FlowerGrid.Background = Brushes.Red;
                }

                if (this.currentFrameCount % 30 == 0)
                {
                    this.alertBlinkingSeconds--;
                }
            }
        }

        /// <summary>
        /// tell all plants that they are withering
        /// </summary>
        private void witherAllPlants()
        {
            foreach (Plant plant in this.activePlants)
            {
                plant.IsWithering = true;
            }
        }

        /// <summary>
        /// resets the entire drawing mode to its default values. 
        /// only resets if a reset is needed, meaing a reset is available.
        /// </summary>
        private void resetPlants()
        {
            Console.WriteLine("in reset");
            if (this.resetAvailable)
            {
                Console.WriteLine("doing reset");
                this.resetAvailable = false;

                //reset offset (offset against burning)
                this.xOffset = 0;
                this.xOffsetDiff = 0;

                //reset alert blinking
                this.alertBlinkingSeconds = 0;
                this.alertBlinking = false;
                this.FlowerGrid.Background = Brushes.Black;

                //reset trigger area
                this.triggerAreaBlinking = false;
                this.TriggerArea.Opacity = 0.0;

                this.removeCandidates.Clear();

                for (int i = 0; i < this.activePlants.Count; i++)
                {
                    //this.FlowerGrid.Children.Remove(this.activeSpriteContainers[i].Img);
                    //this.activeSpriteContainers[i].Img = null;
                    //this.activeSpriteContainers.Remove(this.activeSpriteContainers[i]);
                    this.removePlant(this.activePlants[i]);
                }

                this.activePlants.Clear(); //obsolete
                this.FlowerGrid.Children.Clear(); //obsolete

                //reset flowers (remove children from grid, remove , update available count, remove 
                this.seedPosition.Clear();
                this.seedColor.Clear();
                //this.availableSpriteContainers; //TODO reuse containers instead of deleting them. 
                Console.WriteLine("reset done");
            }
        }

        /// <summary>
        /// removes a given plant of the drawing grid and from the list of active plants.
        /// the plants is hard-removed, meaning not withering ist done. 
        /// if you want to wither a plant, set its IsWithering property to true.
        /// </summary>
        /// <param name="plantToRemove"></param>
        private void removePlant(Plant plantToRemove)
        {
            this.FlowerGrid.Children.Remove(plantToRemove.Img);
            plantToRemove.Img = null;
            this.activePlants.Remove(plantToRemove);
        }


        /// <summary>
        /// calculates the global offset for the plants, which will be applied in careForPlants. 
        /// prevents burning of contours. 
        /// </summary>
        private void calcGlobalOffset()
        {
            if (this.currentFrameCount % 120 == 0 && this.xOffset <= 0)
            {
                //every 120 frames: set random offset to left or right
                this.xOffset = random.Next(300);
                this.xOffset = 150 - this.xOffset;
            }

            if (this.currentFrameCount % 2 == 0)
            {
                //check this only every other frame, so the animation seems smooth
                if (this.xOffset > 0)
                {
                    //if there is a offset to the right, do it
                    this.xOffsetDiff = 1;
                    this.xOffset--;
                }
                else if (this.xOffset < 0)
                {
                    //if there is an offset to the left, do it
                    this.xOffsetDiff = -1;
                    this.xOffset++;
                }
            }
            else
            {
                this.xOffsetDiff = 0;
            }
        }

        /// <summary>
        /// takes care that plants are growing, sprite animations are playing and plants are moving left and right
        /// </summary>
        private void careForPlants()
        {
            for (int spriteCount = 0; spriteCount < this.activePlants.Count; spriteCount++)
            {
                Plant currentPlant = this.activePlants[spriteCount];
                Image currentSprite = currentPlant.Img;
                //currentSprite.Source = this.spriteImages[spriteCount % this.spriteCountUnique, this.currentFrameCount % this.numberFrames];
                currentSprite.Source = this.spriteImages[currentPlant.SpriteIndex % this.spriteCountUnique, this.currentFrameCount % this.numberFrames];
                if (currentPlant.IsGrowing && currentSprite.Height < this.spriteHeight)
                {
                    currentSprite.Margin = new Thickness(currentSprite.Margin.Left - 1, currentSprite.Margin.Top - 2, 0, 0);
                    currentSprite.Height = currentSprite.Height + 2;
                    if (currentSprite.Height >= this.spriteHeight)
                    {
                        currentPlant.IsGrowing = false;
                    }
                }
                else if (currentPlant.IsWithering)
                {
                    if (currentSprite.Height > 10)
                    {
                        currentSprite.Margin = new Thickness(currentSprite.Margin.Left + 1, currentSprite.Margin.Top + 2, 0, 0);
                        currentSprite.Height = currentSprite.Height - 2;
                    }
                    else
                    {
                        //mark this plant as a remove candidate
                        this.removeCandidates.Add(currentPlant);
                    }
                }
                //else if (this.currentFrameCount % 10 == 0)
                //{
                //    Console.WriteLine(currentPlant.TimeGrown);
                //    if (currentPlant.TimeGrown > 5)
                //    {
                //        //wither after 5 seconds of being fully grown
                //        currentPlant.IsWithering = true;
                //    }
                //}
                if (this.xOffsetDiff != 0)
                {
                    //aply the offset
                    currentSprite.Margin = new Thickness(currentSprite.Margin.Left + xOffsetDiff, currentSprite.Margin.Top, 0, 0);
                }
            }

            if (this.removeCandidates.Count > 0)
            {
                for (int i = 0; i < this.removeCandidates.Count; i++)
                {
                    this.removePlant(this.removeCandidates[i]);
                }
                this.removeCandidates.Clear();
                this.resetAvailable = true; //indicates that a plant has been removed and that there might be a complete resete necessary
            }
        }

        /// <summary>
        /// increment the time based frame counter
        /// </summary>
        private void doFrameCounter()
        {
            //this.currentFrameCount++;
            DateTime currentTime = DateTime.Now;

            int millisSinceLast = (currentTime - this.oldTime).Milliseconds;
            while (millisSinceLast > frameTime)
            {
                this.currentFrameCount += 1;
                millisSinceLast -= frameTime;
                this.oldTime = currentTime;
            }
        }

        private void WindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.initTriggerArea();
        }

        private void MemoryMapData()
        {
            this.runningGameThread = true;
            MemoryMappedViewAccessor accessor;

            object[] receivedSkeleton = new Object[sensors.Count];
            DepthImagePixel[][] receivedDepthPixels = new DepthImagePixel[sensors.Count][];
            byte[][] receivedColorStream = new byte[sensors.Count][];

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
                    receivedSkeleton[x] = new Skeleton();
                    receivedDepthPixels[x] = new DepthImagePixel[ColorAndDepthPixelArrayByteLength / 4];
                    receivedColorStream[x] = new byte[ColorAndDepthPixelArrayByteLength];
                    
                    // ------------- Skeleton holen ------------------                            
                    try
                    {
                        mutex = Mutex.OpenExisting("mappedfilemutex");
                        mutex.WaitOne(); // sichert alleinigen Zugriff

                        var receivedSkeletonBytes = new byte[SkeletonObjektByteLength]; // Byte-Array mit Laenge von serialized Skeleton
                        accessor = skeletonFiles[x].CreateViewAccessor(); // Reader fuer aktuelle MMF
                        accessor.ReadArray<byte>(0, receivedSkeletonBytes, 0, receivedSkeletonBytes.Length); // liest Nachricht von Konsolenanwendung

                        receivedSkeleton[x] = ByteArrayToObject(receivedSkeletonBytes); // wandelt sie in Object oder null um

                        //if (receivedSkeleton != null)
                        //{
                        //    Console.WriteLine("Tracking ID:   " + ((Skeleton)receivedSkeleton[x]).TrackingId);
                        //}
                        //else
                        //{
                        //    Console.WriteLine("Kein Skeleton empfangen!");
                        //}
                        //Console.WriteLine("------------");

                        mutex.ReleaseMutex(); // gibt Mutex wieder frei

                    }
                    catch (Exception ex)
                    {
                        mutex.ReleaseMutex(); // gibt Mutex wieder frei
                        foreach (Process p in processes)
                        {
                            //p.Kill(); // beendet alle Konsolenanwendungen
                        }
                    }

                    if (true) // ehem. if !skeletonFound, aber jetzt brauchen wir immer den depthStream
                    {
                        // ------------- DepthStream holen ------------------
                        try
                        {
                            mutex = Mutex.OpenExisting("mappedfilemutex");
                            mutex.WaitOne();
                            receivedDepthPixels[x] = new DepthImagePixel[307200]; // 307200 = 640 x 480

                            accessor = depthFiles[x].CreateViewAccessor(); // Reader fuer aktuelle MMF
                            accessor.ReadArray<DepthImagePixel>(0, receivedDepthPixels[x], 0, receivedDepthPixels[x].Length); // liest Nachricht von Konsolenanwendung
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
                            receivedColorStream[x] = new byte[ColorAndDepthPixelArrayByteLength]; // Byte-Array mit Laenge von serialized Skeleton
                            accessor = colorFiles[x].CreateViewAccessor(); // Reader fuer aktuelle MMF
                            accessor.ReadArray<byte>(0, receivedColorStream[x], 0, receivedColorStream[x].Length); // liest Nachricht von Konsolenanwendung

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

                }
                Dispatcher.Invoke(DispatcherPriority.Send, new Action(() => processSensorData(receivedSkeleton, receivedDepthPixels, receivedColorStream)));

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
                try
                {
                    p.Kill(); // beendet auch die Konsolenanwendungen
                }
                catch (Exception ex)
                {
                    Console.WriteLine("prozess beenden: " + ex);
                }
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
            else if (e.Key.Equals(System.Windows.Input.Key.G))
            {
                this.blinkAlert(3);

            }
            else if (e.Key.Equals(System.Windows.Input.Key.T))
            {
                this.blinkTriggerAreaToggle(!this.triggerAreaBlinking);
                this.toggleFrameTimer(true);
            }
            else if (e.Key.Equals(System.Windows.Input.Key.Space))
            {
                int randomNumber = random.Next(0, Convert.ToInt32(this.Width));
                this.queueSeed(randomNumber, Colors.Red);
            }
            else if (e.Key.Equals(System.Windows.Input.Key.W))
            {
                this.witherAllPlants();
            }
            else if (e.Key.Equals(System.Windows.Input.Key.Enter))
            {
                this.seedSeeds();
                this.toggleFrameTimer(false);
                this.toggleFrameTimer(true);
            }
            else
            {
                Console.WriteLine("key down: " + e.Key);
            }
        }

        /// <summary>
        /// converts a 2D point to its corresponding point in a 3D world
        /// </summary>
        /// <param name="point2D"></param>
        /// <returns></returns>
        private Point3D pointTo3DPoint(System.Windows.Point point2D, int offset)
        {
            return new Point3D((point2D.X/2) - (this.Width / 2) + offset, (this.Height / 2) - point2D.Y, 0.0);
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
            //frameTimer = new System.Windows.Threading.DispatcherTimer();
            //frameTimer.Tick += OnFrame;
            //frameTimer.Interval = TimeSpan.FromSeconds(1.0 / 60.0);
            //frameTimer.Start();

            //this.spawnPoints = new Dictionary<Color,Point3D>();
            this.lastTick = Environment.TickCount;

            pm = new ParticleSystemManager();

            this.WorldModels.Children.Add(pm.CreateParticleSystem(maxParticleCountPerSystem, this.colorBodyCenter));

            this.WorldModels.Children.Add(pm.CreateParticleSystem(maxParticleCountPerSystem, this.colorLeftHand));
            this.WorldModels.Children.Add(pm.CreateParticleSystem(maxParticleCountPerSystem, this.colorRightHand));
            this.WorldModels.Children.Add(pm.CreateParticleSystem(maxParticleCountPerSystem, this.colorMousePoint));
            this.WorldModels.Children.Add(pm.CreateParticleSystem(maxParticleCountPerSystem, this.colorHandCenterPoint));

            random = new Random();

            
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
            //this.drawingGroupMouse = new DrawingGroup();

            //// Create an image source that we can use in our image control
            //this.imageSourceMouse = new DrawingImage(this.drawingGroupMouse);

            //// Display the drawing using our image control
            //MouseImage.Source = this.imageSourceMouse;

            OverlayImages[0] = OverlayImage0;
            OverlayImages[1] = OverlayImage1;
            HeadTrackingImages[0] = HeadTrackedImage0;
            HeadTrackingImages[1] = HeadTrackedImage1;
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
            this.Background = System.Windows.Media.Brushes.White;
            this.Cursor = System.Windows.Input.Cursors.None;

            Rect size = System.Windows.SystemParameters.WorkArea; //size of the display, minus the task bar
            double primaryWidth = System.Windows.SystemParameters.FullPrimaryScreenWidth;
            double desiredWidth = primaryWidth;
            //desiredWidth = 1366; //1366 , 3412 , 1706
            this.Camera.Width = desiredWidth;
            this.World.Width = desiredWidth;
            //this.Width = size.Width;
            //this.Height = size.Height;
            this.Width = desiredWidth;
            this.Height = System.Windows.SystemParameters.PrimaryScreenHeight; //480
            this.Left = size.Left;
            this.Top = size.Top;

            double scaleFactor = 1 / (desiredWidth / primaryWidth);
            this.Grid.LayoutTransform = new ScaleTransform(scaleFactor, 1);
            Console.WriteLine("primaryWidth: " + primaryWidth + ", desiredWidth: " + desiredWidth
                + ", this.Width: " + this.Width + ", scaleFactor: " + scaleFactor + ", this.ActualWidth: " + this.ActualWidth);

            this.FlowerGrid.Height = this.Height;
            Console.WriteLine(this.FlowerGrid.Height);

            Color bgColor = Colors.DarkGray;
            RadialGradientBrush b = new RadialGradientBrush();
            b.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(0xFF, bgColor.R, bgColor.G, bgColor.B), 0.05));
            b.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(0x00, bgColor.R, bgColor.G, bgColor.B), 1.0));
            this.Background = b;
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

            greenScreenPixelData = new int[ColorAndDepthPixelArrayByteLength/4];
            colorCoordinates = new ColorImagePoint[ColorAndDepthPixelArrayByteLength/4];
            colorBitmaps[0] = new WriteableBitmap(colorWidth, colorHeight, 96.0, 96.0, PixelFormats.Bgr32, null);
            colorBitmaps[1] = new WriteableBitmap(colorWidth, colorHeight, 96.0, 96.0, PixelFormats.Bgr32, null);
            OverlayImages[0].Source = colorBitmaps[0];
            OverlayImages[1].Source = colorBitmaps[1];
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
            for(int screenCounter = 0; screenCounter < sensors.Count; screenCounter++){
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

        private void doPlantDrawing()
        {
            bool plantThreadRunning = true;

            while (plantThreadRunning)
            {
                Dispatcher.Invoke(DispatcherPriority.Send, new Action(doActualPlantDrawing));

                Thread.Sleep(30);
            }
        }

        private void doActualPlantDrawing()
        {
            this.doFrameCounter();

            this.fadeTriggerArea();

            if (alertBlinking && this.alertBlinkingSeconds > 0)
            {
                this.blinkWindow();
            }
            else
            {
                this.alertBlinking = false;

                this.calcGlobalOffset();

                this.seedSeeds();

                this.careForPlants();
            }

            if (this.resetAvailable //only if a reset is necessary, meaning that a plant has been removed previously
                && this.currentFrameCount % 10 == 0 //only every so often. for performance
                && this.activePlants.Count + this.seedPosition.Count <= 0 //only if there are no more plants left
                )
            {
                this.resetPlants();
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
                    pm.SpawnParticle(point, 10.0, color, particleSizeMultiplier * random.NextDouble(), particleLifeMultiplier * random.NextDouble());
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

        private void processSensorData(object[] skeletons, DepthImagePixel[][] depthStreams, byte[][] colorStreams)
        {
            
            for (int screenCounter = 0; screenCounter < sensors.Count; screenCounter++)
            {

                // Variablen für Head Tracking
                int kopfXPos = 0;
                int kopfYPos = 0;
                int distanzKopfZuHals = 0;

                object skeletonData = skeletons[screenCounter];
                DepthImagePixel[] depthStream = depthStreams[screenCounter];
                byte[] colorStream = colorStreams[screenCounter];

                this.rightHandPoints[screenCounter].Clear();
                this.leftHandPoints[screenCounter].Clear();
                this.handCenterPoints[screenCounter].Clear();
                this.playerCenterPoints[screenCounter].Clear();
                int offset = (screenCounter == 0) ? (0) : ((int)this.Width / 2);

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
                            this.rightHandPoints[screenCounter].Add(pointTo3DPoint(this.stretchDepthPointToScreen(rightHandPoint), offset));
                        }

                        if (leftHand.TrackingState == JointTrackingState.Tracked
                            || leftHand.TrackingState == JointTrackingState.Inferred)
                        {
                            leftHandPoint = SkeletonPointToScreen(leftHand.Position);
                            this.leftHandPoints[screenCounter].Add(pointTo3DPoint(this.stretchDepthPointToScreen(leftHandPoint), offset));
                        }

                        if (rightHandPoint != null && this.stretchDepthPointToScreen(rightHandPoint).Y > this.Height * 0.9)
                        {
                            Console.WriteLine("seeding for right hand on skel " + screenCounter);
                            //int randomNumber = random.Next(0, Convert.ToInt32(this.Width));
                            this.queueSeed((int)this.stretchDepthPointToScreen(rightHandPoint).X/2 + offset, Colors.Red);
                        }

                        if (leftHandPoint != null && this.stretchDepthPointToScreen(leftHandPoint).Y > this.Height * 0.9)
                        {
                            Console.WriteLine("seeding for left hand on skel " + screenCounter);
                            //int randomNumber = random.Next(0, Convert.ToInt32(this.Width));
                            this.queueSeed((int)this.stretchDepthPointToScreen(leftHandPoint).X/2 + offset, Colors.Red);
                        }

                        // wenn beide Hände erkannt wurden, zusätzlich Mittelpunkt zwischen den Händen anzeigen
                        if (leftHandPoint != null && rightHandPoint != null)
                        {
                            System.Windows.Point handCenterPoint = new System.Windows.Point((rightHandPoint.X + leftHandPoint.X) / 2,
                                                                                            (rightHandPoint.Y + leftHandPoint.Y) / 2);

                            double distance = Math.Sqrt(Math.Pow(rightHandPoint.X - leftHandPoint.X, 2)
                                                        + Math.Pow(rightHandPoint.Y - leftHandPoint.Y, 2));

                            this.handCenterThicknesses[screenCounter] = distance / 8;
                            this.handCenterPoints[screenCounter].Add(pointTo3DPoint(this.stretchDepthPointToScreen(handCenterPoint), offset));
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

                        HeadTrackingImages[screenCounter].Source = GetCroppedImage(screenCounter, colorStream, kopfXPos, kopfYPos, distanzKopfZuHals * 2);
                    }


                }

                // ------- Overlay zeichnen ------------

                for (int x = 0; x < colorStream.Length; x++)
                {
                    // setzt alle Pixel auf dunkelgrau statt dem Kamerabild
                    colorStream[x] = (byte)80;
                }

                int centerPointPixel = ((int)playerCenterPoint.X + ((int)playerCenterPoint.Y * depthWidth));

                colorBitmaps[screenCounter].WritePixels(
                    new Int32Rect(0, 0, colorBitmaps[screenCounter].PixelWidth, colorBitmaps[screenCounter].PixelHeight),
                    colorStream,
                    colorBitmaps[screenCounter].PixelWidth * sizeof(int),
                    0);

                if (null == playerOpacityMaskImages[screenCounter])
                {
                    playerOpacityMaskImages[screenCounter] = new WriteableBitmap(depthWidth, depthHeight, 96, 96, PixelFormats.Bgra32, null);
                    OverlayImages[screenCounter].OpacityMask = new ImageBrush(playerOpacityMaskImages[screenCounter]);
                }

                playerOpacityMaskImages[screenCounter].WritePixels(
                    new Int32Rect(0, 0, depthWidth, depthHeight),
                    greenScreenPixelData,
                    depthWidth * ((playerOpacityMaskImages[screenCounter].Format.BitsPerPixel + 7) / 8),
                    0);

                if (playerCenterPoint != null)
                {
                    if (!this.trackingSkeleton)
                    {
                        this.playerCenterPoints[screenCounter].Add(this.pointTo3DPoint(this.stretchDepthPointToScreen(playerCenterPoint), offset));
                    }
                }


            } // end for screencounter-loop

            this.doFrameCalculation();
        }

    }
}
