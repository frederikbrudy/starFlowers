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
        String[] spriteFilenames;
        int ximages = 10; //6The number of sprites in each row
        int yimages = 12; //5The number of sprites in each column
        int numberFrames = -1;
        int currentRow = 0;
        int currentColumn = 0;
        int spriteWidth = 600;
        int spriteHeight = 480;
        BitmapSource[,] spriteImages;
        int currentFrameCount;
        //Image[] spriteContainer;

        /// <summary>
        /// dispatcher for frame events
        /// </summary>
        System.Windows.Threading.DispatcherTimer frameTimer;

        List<int> seedPosition;
        List<Color> seedColor;
        List<Image> availableSpriteContainers;
        List<Image> activeSpriteContainers;
        private int maximumPlantCount;
        private int currentlyAvailableContainersCount;
        private int spriteCountUnique;

        public Window1()
        {
            InitializeComponent();

            frameTimer = new System.Windows.Threading.DispatcherTimer();
            frameTimer.Tick += OnFrame;
            frameTimer.Interval = TimeSpan.FromSeconds(1.0 / 30.0);

            this.spriteFilenames = new String[5];
            this.spriteFilenames[0] = "C:/Users/Frederik/Dropbox/Uni/Kinect/src/starFlowers/StarFlowers/Images/Baum1/";
            this.spriteFilenames[1] = "C:/Users/Frederik/Dropbox/Uni/Kinect/src/starFlowers/StarFlowers/Images/Baum2/";
            this.spriteFilenames[2] = "C:/Users/Frederik/Dropbox/Uni/Kinect/src/starFlowers/StarFlowers/Images/Blume1/";
            this.spriteFilenames[3] = "C:/Users/Frederik/Dropbox/Uni/Kinect/src/starFlowers/StarFlowers/Images/Gras1/";
            this.spriteFilenames[4] = "C:/Users/Frederik/Dropbox/Uni/Kinect/src/starFlowers/StarFlowers/Images/Gras2/";

            //this.spriteFilenames[0] = "C:/Users/Frederik/Dropbox/Uni/Kinect/src/starFlowers/StarFlowers/Images/sprite_blume_1.png";
            //this.spriteFilenames[1] = "C:/Users/Frederik/Dropbox/Uni/Kinect/src/starFlowers/StarFlowers/Images/sprite_gras_1.png";
            //this.spriteFilenames[2] = "C:/Users/Frederik/Dropbox/Uni/Kinect/src/starFlowers/StarFlowers/Images/sprite_blume_1.png";
            //this.spriteFilenames[3] = "C:/Users/Frederik/Dropbox/Uni/Kinect/src/starFlowers/StarFlowers/Images/sprite_blume_2.png";
            this.spriteImages = new BitmapSource[this.spriteFilenames.Length, ximages * yimages];
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            this.initSprites();
        }

        private void initSprites()
        {
            this.createCroppedImages();
            //this.spriteContainer = new Image[5];
            //this.spriteContainer[0] = this.sprite0;
            //this.spriteContainer[1] = this.sprite1;
            //this.spriteContainer[2] = this.sprite2;
            //this.spriteContainer[3] = this.sprite3;
            //this.spriteContainer[4] = this.sprite4;

            this.availableSpriteContainers = new List<Image>();

            //this.availableSpriteContainers.Add(this.sprite0);
            //this.availableSpriteContainers.Add(this.sprite1);
            //this.availableSpriteContainers.Add(this.sprite2);
            //this.availableSpriteContainers.Add(this.sprite3);
            //this.availableSpriteContainers.Add(this.sprite4);
            this.addSpriteContainer();

            this.activeSpriteContainers = new List<Image>(); 
            
            this.spriteCountUnique = this.spriteImages.Length / this.numberFrames;
            this.maximumPlantCount = this.spriteCountUnique * 2;
            //this.currentlyAvailableContainers = this.availableSpriteContainers.Count;

            //init seeds
            this.seedPosition = new List<int>();
            this.seedColor = new List<Color>();
        }

        /// <summary>
        /// adds a new image as a child to the main grid of the application
        /// </summary>
        private void addSpriteContainer()
        {
            Image img = new Image();
            img.Width = 640;
            img.Height = 480;
            img.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            img.VerticalAlignment = System.Windows.VerticalAlignment.Top;
            this.MainGrid.Children.Add(img);
            this.availableSpriteContainers.Add(img);
            this.currentlyAvailableContainersCount = this.currentlyAvailableContainersCount+this.availableSpriteContainers.Count;
        }

        private void addSpriteContainer(int numberContainers)
        {
            for (int i = 0; i < numberContainers; i++)
            {
                this.addSpriteContainer();
            }
        }

        private void createCroppedImages()
        {
            this.numberFrames = this.ximages * this.yimages;
            for (int spriteCount = 0; spriteCount < this.spriteFilenames.Length; spriteCount++)
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
             * */
        }

        private void keydown(object sender, KeyEventArgs e)
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
            else if (e.Key.Equals(System.Windows.Input.Key.Space))
            {
                Random random = new Random();
                int randomNumber = random.Next(0, Convert.ToInt32(this.Width));
                //Color color = Color.FromArgb(random.Next(256), random.Next(256), random.Next(256), random.Next(256));
                this.seedPlant(randomNumber, Colors.Red);
            }
            else if (e.Key.Equals(System.Windows.Input.Key.Enter))
            {
                if (!this.frameTimer.IsEnabled)
                {
                    //int x = 0;
                    //foreach (Image img in this.spriteContainer)
                    //{
                    //    img.Margin = new Thickness(x, this.Height, 0, 0);
                    //    img.Height = 0;
                    //    x += 240;
                    //}

                    this.growPlants();

                    frameTimer.Start();

                    //this.startAnimation(this.sprite0, 480, 0, 0);
                }
                else
                {
                    frameTimer.Stop();
                }
            }
        }

        private void growPlants()
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
                    this.activeSpriteContainers.Add(spriteImg);

                    //in onFrame: grow only plants in the growing list.
                }
                this.seedPosition.Clear();
                this.seedColor.Clear();
            }
        }

        private void seedPlant(int positionX, Color color)
        {
            int futurePlantCount = this.activeSpriteContainers.Count + this.seedPosition.Count+1;
            //Console.WriteLine("currentlyAvailableContainersCount: " + currentlyAvailableContainersCount
            //    + ", futurePlantCount: " + futurePlantCount
            //    + ", maximumPlantCount: " + maximumPlantCount
            //    + ", futurePlantCount: " + futurePlantCount);
            //if (this.currentlyAvailableContainersCount < futurePlantCount)
            //{
            //    Console.WriteLine("currentlyAvailableContainersCount < futurePlantCount");
            //}
            //if (this.maximumPlantCount >= futurePlantCount)
            //{
            //    Console.WriteLine("maximumPlantCount <= futurePlantCount");
            //}
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

        private void OnFrame(object sender, EventArgs e)
        {
            
            this.currentFrameCount++;

            this.growPlants();

            for (int spriteCount = 0; spriteCount < this.activeSpriteContainers.Count; spriteCount++)
            {
                Image currentSprite = this.activeSpriteContainers[spriteCount];
                currentSprite.Source = this.spriteImages[(spriteCount+this.numberFrames/2) % this.spriteCountUnique, this.currentFrameCount % this.numberFrames];
                if (currentSprite.Height < this.spriteHeight)
                {
                    currentSprite.Margin = new Thickness(currentSprite.Margin.Left - 1, currentSprite.Margin.Top - 2, 0, 0);
                    currentSprite.Height = currentSprite.Height + 2;
                }
            }
        }

        private void AnimationCompleted(Image animatedImage, int oldMarginX, int newMarginX, int globalMarginX)
        {
            Console.WriteLine("margin: " + this.sprite0.Margin);
            Console.WriteLine("animatedImage: " + animatedImage.Margin);
            this.sprite0.Margin = new Thickness(globalMarginX, 0, 0, 0);
            //this.startAnimation(this.sprite0, 0, newMarginX, globalMarginX);
            //set new position
            //this.startAnimation(this.sprite0, oldMarginX, newMarginX);
        }

    }
}
