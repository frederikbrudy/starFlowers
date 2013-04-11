using System;
using System.Collections.Generic;
using System.IO;
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
using System.Windows.Shapes;

namespace StarFlowers
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class Window1 : Window
    {
        
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
        System.Windows.Threading.DispatcherTimer frameTimer;

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

        Random random = new Random();
        private bool resetAvailable;
        private DateTime oldTime;
        private const int frameTime = 30;

        public Window1()
        {
            InitializeComponent();
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            this.initSprites();
            this.initTriggerArea();
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
            frameTimer = new System.Windows.Threading.DispatcherTimer();
            frameTimer.Tick += OnFrame;
            frameTimer.Interval = TimeSpan.FromSeconds(1.0 / frameTime);

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
            if (frameTimer.IsEnabled && !forceRunning)
            {
                frameTimer.Stop();
            }
            else
            {
                frameTimer.Start();
            }
        }

        private void keydown(object sender, KeyEventArgs e)
        {
            if (e.Key.Equals(System.Windows.Input.Key.Escape))
            {
                System.Windows.Application.Current.Shutdown();
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
                    double opacity = random.NextDouble()+0.25;
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
            int futurePlantCount = this.activePlants.Count + this.seedPosition.Count+1;
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
                this.currentFrameCount += 5;
                millisSinceLast -= frameTime;
                this.oldTime = currentTime;
            }
        }

        private void OnFrame(object sender, EventArgs e)
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

        private void WindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.initTriggerArea();
        }
    }
}
