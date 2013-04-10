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
using System.Windows.Media.Animation;
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

        //int currentRow = 0;
        //int currentColumn = 0;

        /// <summary>
        /// width of a single sprite frame
        /// </summary>
        int spriteWidth = 600;

        /// <summary>
        /// height of a single sprite frame
        /// </summary>
        int spriteHeight = 480;

        /// <summary>
        /// the array which holds all sprite images. first index shows an individual sprite animation, second index points to each frame
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
        List<Image> availableSpriteContainers;

        /// <summary>
        /// list of sprite containers, which are currently in use. they will be redrawn on each frame
        /// </summary>
        List<Image> activeSpriteContainers;

        /// <summary>
        /// maximum plant count, meaning the maximum sprite animations which is possible in this application
        /// </summary>
        private int maximumPlantCount;

        /// <summary>
        /// number of currently available containers, indicating how many sprite containers are free to use.
        /// </summary>
        private int currentlyAvailableContainersCount;

        /// <summary>
        /// number of how many different sprite animations there are. 
        /// </summary>
        private int spriteCountUnique;

        Random random = new Random();
        private int xOffset;
        private bool alertBlinking = false;
        private int alertBlinkingSeconds;
        private bool triggerBlinking;
        private double triggerDiff;
        private int xOffsetDiff;

        public Window1()
        {
            InitializeComponent();

        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            this.initSprites();
            this.TriggerArea.Fill = ColorsAndBrushes.getBrushFromColorLinearGradient(Colors.Red);
            this.TriggerArea.Margin = new Thickness(0, this.Height - this.TriggerArea.Height, 0, 0);
        }

        private void initSprites()
        {
            frameTimer = new System.Windows.Threading.DispatcherTimer();
            frameTimer.Tick += OnFrame;
            frameTimer.Interval = TimeSpan.FromSeconds(1.0 / 30.0);

            String[] spriteFilenames = new String[5];
            spriteFilenames[0] = "C:/Users/Frederik/Dropbox/Uni/Kinect/src/starFlowers/StarFlowers/Images/Baum1/";
            spriteFilenames[1] = "C:/Users/Frederik/Dropbox/Uni/Kinect/src/starFlowers/StarFlowers/Images/Baum2/";
            spriteFilenames[2] = "C:/Users/Frederik/Dropbox/Uni/Kinect/src/starFlowers/StarFlowers/Images/Blume1/";
            spriteFilenames[3] = "C:/Users/Frederik/Dropbox/Uni/Kinect/src/starFlowers/StarFlowers/Images/Gras1/";
            spriteFilenames[4] = "C:/Users/Frederik/Dropbox/Uni/Kinect/src/starFlowers/StarFlowers/Images/Gras2/";

            //this.spriteFilenames[0] = "C:/Users/Frederik/Dropbox/Uni/Kinect/src/starFlowers/StarFlowers/Images/sprite_blume_1.png";
            //this.spriteFilenames[1] = "C:/Users/Frederik/Dropbox/Uni/Kinect/src/starFlowers/StarFlowers/Images/sprite_gras_1.png";
            //this.spriteFilenames[2] = "C:/Users/Frederik/Dropbox/Uni/Kinect/src/starFlowers/StarFlowers/Images/sprite_blume_1.png";
            //this.spriteFilenames[3] = "C:/Users/Frederik/Dropbox/Uni/Kinect/src/starFlowers/StarFlowers/Images/sprite_blume_2.png";

            this.spriteImages = new BitmapSource[spriteFilenames.Length, ximages * yimages];

            this.createCroppedImages(spriteFilenames);

            this.availableSpriteContainers = new List<Image>();

            this.addSpriteContainer();

            this.activeSpriteContainers = new List<Image>(); 
            
            this.spriteCountUnique = this.spriteImages.Length / this.numberFrames;
            this.maximumPlantCount = this.spriteCountUnique * 4;
            //this.currentlyAvailableContainers = this.availableSpriteContainers.Count;

            //init seeds
            this.seedPosition = new List<int>();
            this.seedColor = new List<Color>();
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

        private void triggerAreaBlink(bool blinking)
        {
            this.triggerBlinking = blinking;
        }

        /// <summary>
        /// adds a new image as a child to the main grid of the application
        /// </summary>
        private void addSpriteContainer()
        {
            Image img = new Image();
            img.Width = 600;
            img.Height = 480;
            img.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            img.VerticalAlignment = System.Windows.VerticalAlignment.Top;
            this.FlowerGrid.Children.Add(img);
            this.availableSpriteContainers.Add(img);
            this.currentlyAvailableContainersCount = this.currentlyAvailableContainersCount + this.availableSpriteContainers.Count;
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
        /// loads the sprite images. the images must be organized in folders (one folder per sprite animation) numbered sequentially.
        /// </summary>
        /// <param name="spriteFilenames">array of folder names (each with a trailing /)</param>
        private void createCroppedImages(String[] spriteFilenames)
        {
            this.numberFrames = this.ximages * this.yimages;
            for (int spriteCount = 0; spriteCount < spriteFilenames.Length; spriteCount++)
            {
                Console.WriteLine("loading image folder: " + spriteFilenames[spriteCount]);
                string[] files = Directory.GetFiles(spriteFilenames[spriteCount]); //, SearchOption.AllDirectories
                for (int frameCount = 0; frameCount < files.Length; frameCount++)
                {
                    String file = files[frameCount];
                    this.spriteImages[spriteCount, frameCount] = new BitmapImage(new Uri(file));
                }
                Console.WriteLine("loading done");
            }

            /*
            //creating sprites from spritesheet
            for (int spriteCount = 0; spriteCount < this.spriteFilenames.Length; spriteCount++)
            {
                System.Drawing.Rectangle cropRect = new System.Drawing.Rectangle(0, 0, this.spriteWidth, this.spriteHeight);
                System.Drawing.Bitmap src = System.Drawing.Image.FromFile(this.spriteFilenames[spriteCount]) as System.Drawing.Bitmap;
                System.Drawing.Bitmap target;// = new System.Drawing.Bitmap(cropRect.Width, cropRect.Height);
                //this.frames = new BitmapSource[this.numberFrames];

                for (int row = 0; row < this.ximages; row++)
                {
                    for (int col = 0; col < this.yimages; col++)
                    {
                        int currentX = row * this.spriteWidth;
                        int currentY = col * this.spriteHeight;

                        cropRect.X = currentX;
                        cropRect.Y = currentY;

                        target = new System.Drawing.Bitmap(cropRect.Width, cropRect.Height);

                        using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(target))
                        {
                            g.DrawImage(src, new System.Drawing.Rectangle(0, 0, target.Width, target.Height),
                                cropRect, System.Drawing.GraphicsUnit.Pixel);
                        }

                        BitmapSource frame = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                           target.GetHbitmap(), IntPtr.Zero,
                           System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(target.Width, target.Height));

                        int index = row + col * this.ximages;
                        Console.WriteLine("row: " + row + ", col: " + col + ", index: " + index);
                        //this.frames[index] = frame;
                        this.sprites[spriteCount, index] = frame;

                    }
                }
                //this.sprites[spriteCount] = this.frames;
                GC.Collect();
            }
            */
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
                frameTimer.Start();
            }
            else if (e.Key.Equals(System.Windows.Input.Key.T))
            {
                this.triggerAreaBlink(!this.triggerBlinking);
                frameTimer.Start();
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
                //Color color = Color.FromArgb(random.Next(256), random.Next(256), random.Next(256), random.Next(256));
                this.queueSeed(randomNumber, Colors.Red);
            }
            else if (e.Key.Equals(System.Windows.Input.Key.Enter))
            {
                if (!this.frameTimer.IsEnabled)
                {
                    this.seedSeeds();

                    frameTimer.Start();

                    //this.startAnimation(this.sprite0, 480, 0, 0);
                }
                else
                {
                    frameTimer.Stop();
                }
            }
        }

        /// <summary>
        /// grows new plants from previously seeded seeds. if there are no seeds to plant available this method return without changes. 
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

                    Image spriteImg = this.availableSpriteContainers[0];
                    this.availableSpriteContainers.RemoveAt(0);
                    spriteImg.Margin = new Thickness(xPos, this.Height, 0, 0);
                    spriteImg.Height = 0;
                    //Random rand = new Random();
                    double opacity = random.NextDouble()+0.25;
                    opacity = (opacity > 0.9) ? (0.75) : opacity;
                    spriteImg.Opacity = opacity;
                    this.activeSpriteContainers.Add(spriteImg);

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
            int futurePlantCount = this.activeSpriteContainers.Count + this.seedPosition.Count+1;
            if (this.currentlyAvailableContainersCount < futurePlantCount //there are not enough containers for all future plants
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

        private void OnFrame(object sender, EventArgs e)
        {
            //this.MainGrid.Margin
            this.currentFrameCount++;
            if (this.triggerBlinking)
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

                //if (this.TriggerArea.Opacity > 0.1 && this.TriggerArea.Opacity < 0.9)
                //{

                //}
                //if (this.TriggerArea.Opacity < 1.0)
                //{
                //    this.TriggerArea.Opacity += 0.1;
                //}
                //else
                //{

                //}
            }

            if (alertBlinking)
            {
                if (this.alertBlinkingSeconds > 0)
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
                        if (this.currentFrameCount % 60 == 0)
                        {
                            this.alertBlinkingSeconds--;
                        }
                    }
                }
            }
            else
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
                        //this.FlowerGrid.Margin = new Thickness(this.FlowerGrid.Margin.Left - 1, 0, 0, 0);
                        this.xOffsetDiff = 1;
                        this.xOffset--;
                    }
                    else if (this.xOffset < 0)
                    {
                        //if there is an offset to the left, do it
                        //this.FlowerGrid.Margin = new Thickness(this.FlowerGrid.Margin.Left - 1, 0, 0, 0);
                        this.xOffsetDiff = -1;
                        this.xOffset++;
                    }
                }
                else
                {
                    this.xOffsetDiff = 0;
                }

                this.seedSeeds();

                for (int spriteCount = 0; spriteCount < this.activeSpriteContainers.Count; spriteCount++)
                {
                    Image currentSprite = this.activeSpriteContainers[spriteCount];
                    currentSprite.Source = this.spriteImages[spriteCount % this.spriteCountUnique, this.currentFrameCount % this.numberFrames];
                    if (currentSprite.Height < this.spriteHeight)
                    {
                        currentSprite.Margin = new Thickness(currentSprite.Margin.Left - 1, currentSprite.Margin.Top - 2, 0, 0);
                        currentSprite.Height = currentSprite.Height + 2;
                    }
                    if (this.xOffsetDiff != 0)
                    {
                        //aply the offset
                        currentSprite.Margin = new Thickness(currentSprite.Margin.Left + xOffsetDiff, currentSprite.Margin.Top, 0, 0);
                    }
                }
            }
        }

        private void startAnimation(Image animatedImage, int oldMarginX, int newMarginX, int globalMarginX)
        {
            // Create a new animation with start and end values, duration and
            // an optional completion handler.
            /*
            DoubleAnimation da = new DoubleAnimation();
            da.From = oldMarginX;
            da.To = newMarginX;
            da.Duration = new Duration(TimeSpan.FromSeconds(1));
            //da.RepeatBehavior = RepeatBehavior.Forever;
            //da.AutoReverse = true;
            //da.Completed += new EventHandler(Animation_Completed);
            da.Completed += (sender, eArgs) => AnimationCompleted(animatedImage, oldMarginX, newMarginX, globalMarginX+newMarginX); // syntax with parameters
            */

            // Start animating the x-translation 
            //TranslateTransform rt = new TranslateTransform();
            //animatedImage.RenderTransform = rt;
            //rt.BeginAnimation(TranslateTransform.YProperty, da);

            //ScaleTransform scaleTransform1 = new ScaleTransform(1.5, 2.0, 50, 50);
            //animatedImage.RenderTransform = scaleTransform1;
            //scaleTransform1.BeginAnimation(ScaleTransform.ScaleYProperty, da);
        }

        private void AnimationCompleted(Image animatedImage, int oldMarginX, int newMarginX, int globalMarginX)
        {
            //Console.WriteLine("margin: " + this.sprite0.Margin);
            //Console.WriteLine("animatedImage: " + animatedImage.Margin);
            //this.sprite0.Margin = new Thickness(globalMarginX, 0, 0, 0);
            //this.startAnimation(this.sprite0, 0, newMarginX, globalMarginX);
            //set new position
            //this.startAnimation(this.sprite0, oldMarginX, newMarginX);
        }

    }
}
