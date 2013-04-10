using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Collections;
using System.Diagnostics;
using System.Threading;
using System.IO.MemoryMappedFiles;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using Microsoft.Kinect;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Drawing;

namespace MultiProcessMain
{
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
        private byte[] colorPixels;

        private const float RenderWidth = 640.0f;
        private const float RenderHeight = 480.0f;
        private const double JointThickness = 3;
        private const double BodyCenterThickness = 10;
        private const double ClipBoundsThickness = 10;
        private readonly Brush centerPointBrush = Brushes.Blue;
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));
        private readonly Brush inferredJointBrush = Brushes.Yellow;
        private readonly Pen trackedBonePen = new Pen(Brushes.Green, 6);
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);
        private DrawingGroup leftDrawingGroup, rightDrawingGroup;
        private WriteableBitmap[] images;
        private DrawingImage leftImageSource, rightImageSource;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded_1(object sender, RoutedEventArgs e)
        {
            // Zeug initialisieren:
            processes = new List<Process>();
            sensors = new List<KinectSensor>();
            leftDrawingGroup = new DrawingGroup();
            rightDrawingGroup = new DrawingGroup();
            leftImageSource = new DrawingImage(leftDrawingGroup);
            rightImageSource = new DrawingImage(rightDrawingGroup);
            colorPixels = new byte[ColorAndDepthPixelArrayByteLength];


            images = new WriteableBitmap[4];

            for (int i = 0; i < 4; i++)
            {
                images[i] = new WriteableBitmap(640, 480, 96.0, 96.0, PixelFormats.Bgr32, null);
            }

            images[1] = new WriteableBitmap(100, 100, 96.0, 96.0, PixelFormats.Bgr32, null);

            LeftImage.Source = images[0];
            RightImage.Source = images[1];
            LeftDepthStream.Source = images[2];
            //RightDepthStream.Source = images[3];
            RightDepthStream.Source = rightImageSource;
            

            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    sensors.Add(potentialSensor); 
                }
            }
            
            // erzeugt und Startet Thread fuer Behandlung von Daten der Konsolenanwendungen
            var mappedfileThread = new Thread(this.MemoryMapData);
            mappedfileThread.SetApartmentState(ApartmentState.STA);
            mappedfileThread.Start();
        }

        private void MemoryMapData()
        {
            this.runningGameThread = true;

            byte[] receivedBytes;
            object receivedObj;
            MemoryMappedViewAccessor accessor;

            skeletonFiles = new MemoryMappedFile[sensors.Count]; // es werden so viele MMFs wie angeschlossene Kinects erstellt
            depthFiles = new MemoryMappedFile[sensors.Count]; // es werden so viele MMFs wie angeschlossene Kinects erstellt
            colorFiles = new MemoryMappedFile[sensors.Count]; // es werden so viele MMFs wie angeschlossene Kinects erstellt
            try
            {
                Mutex mutex = new Mutex(true, "mappedfilemutex");
                mutex.ReleaseMutex(); //Freigabe gleich zu Beginn
            }
            catch (Exception e) {}

            for (int i = 0; i < sensors.Count; i++) // startet 2 Prozesse
            {
                skeletonFiles[i] = MemoryMappedFile.CreateNew("SkeletonExchange" + i, SkeletonObjektByteLength);
                depthFiles[i] = MemoryMappedFile.CreateNew("DepthExchange" + i, ColorAndDepthPixelArrayByteLength);
                colorFiles[i] = MemoryMappedFile.CreateNew("ColorExchange" + i, ColorAndDepthPixelArrayByteLength);
                
                Process p = new Process();
                p.StartInfo.CreateNoWindow = false; // laesst das Fenster fuer jeden Prozess angezeigt (fuer Debugging)
                p.StartInfo.FileName = Environment.CurrentDirectory + "\\..\\..\\..\\MultiProcessKinect\\bin\\Debug\\MultiProcessKinect.exe"; // die 

                p.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Normal;
                p.StartInfo.Arguments = ""+i + " " + sensors[i].UniqueKinectId;
                p.Start();

                processes.Add(p); // zu Liste der Prozesse hinzufuegen
            }

            while (this.runningGameThread)
            {   
                for (int x = 0; x < sensors.Count; x++) // fuer alle Kinects:
                {
                    bool skeletonFound = false;
                    int kopfXPos = 0;
                    int kopfYPos = 0;
                    int distanzKopfZuHals = 0;
                    
                    // ------------- Skeleton holen ------------------                            
                    try
                    {
                        mutex = Mutex.OpenExisting("mappedfilemutex");
                        mutex.WaitOne(); // sichert alleinigen Zugriff

                        receivedBytes = new byte[SkeletonObjektByteLength]; // Byte-Array mit Laenge von serialized Skeleton
                        accessor = skeletonFiles[x].CreateViewAccessor(); // Reader fuer aktuelle MMF
                        accessor.ReadArray<byte>(0, receivedBytes, 0, receivedBytes.Length); // liest Nachricht von Konsolenanwendung

                        receivedObj = ByteArrayToObject(receivedBytes); // wandelt sie in Object oder null um
                        if (receivedObj != null)
                        {
                            skeletonFound = true;        
                            Skeleton receivedSkel = (Skeleton)(receivedObj); // Tadaa: hier ist das Skeleton

                            Joint halsJoint = receivedSkel.Joints[JointType.ShoulderCenter];
                            Joint kopfJoint = receivedSkel.Joints[JointType.Head];

                            if (
                                (halsJoint.TrackingState == JointTrackingState.Tracked
                              || halsJoint.TrackingState == JointTrackingState.Inferred)
                              && (kopfJoint.TrackingState == JointTrackingState.Tracked
                              || kopfJoint.TrackingState == JointTrackingState.Inferred))
                            {

                               //var p1 = this.SkeletonPointToScreen(halsJoint.Position).X;

                                int kopfX = (int)(this.SkeletonPointToScreen(kopfJoint.Position).X);
                                int kopfY = (int)(this.SkeletonPointToScreen(kopfJoint.Position).Y);
                                int halsX = (int)(this.SkeletonPointToScreen(halsJoint.Position).X);
                                int halsY = (int)(this.SkeletonPointToScreen(halsJoint.Position).Y);

                                kopfXPos = (kopfX + halsX) / 2;
                                kopfYPos = (kopfY + halsY) / 2;

                                distanzKopfZuHals = euklDistanz(kopfX, kopfY, halsX, halsY);
                            }

                            // Testausgaben
                            Console.WriteLine("Tracking ID:   " + receivedSkel.TrackingId);
                            //Console.WriteLine("Hash-Code:   " + receivedSkel.GetHashCode());
                            //Console.WriteLine("Tracking State:   "+ receivedSkel.TrackingState);
                            //var receivedSkelPos = receivedSkel.Position;
                            //Console.WriteLine("Position:   " + receivedSkelPos.X + " " + receivedSkelPos.Y + " " + receivedSkelPos.Z);
                            Console.WriteLine("------------");
                        }
                        else
                        {
                            Console.WriteLine("Received Object ist null");
                        }

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

                    if (!skeletonFound)
                    {
                        // ------------- DepthStream holen ------------------
                        try
                        {
                            mutex = Mutex.OpenExisting("mappedfilemutex");
                            mutex.WaitOne();
                            var receivedDepthPixels = new DepthImagePixel[307200]; // 307200 = 640 x 480

                            accessor = depthFiles[x].CreateViewAccessor(); // Reader fuer aktuelle MMF
                            accessor.ReadArray<DepthImagePixel>(0, receivedDepthPixels, 0, receivedDepthPixels.Length); // liest Nachricht von Konsolenanwendung
                            mutex.ReleaseMutex();

                            int colorPixelIndex = 0;
                            for (int i = 0; i < receivedDepthPixels.Length; ++i)
                            {
                                // Get the depth for this pixel
                                short depth = receivedDepthPixels[i].Depth;
                                byte intensity = (byte)(depth >= 800 && depth <= 4000 ? depth : 0);
                                colorPixels[colorPixelIndex++] = intensity;
                                colorPixels[colorPixelIndex++] = intensity;
                                colorPixels[colorPixelIndex++] = intensity;
                                ++colorPixelIndex;
                            }

                            Dispatcher.Invoke(DispatcherPriority.Send, new Action(() => DrawImage(x + 2, colorPixels)));

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
                    if (skeletonFound)
                    {
                        try
                        {
                            mutex = Mutex.OpenExisting("mappedfilemutex");
                            mutex.WaitOne();
                            receivedBytes = new byte[ColorAndDepthPixelArrayByteLength]; // Byte-Array mit Laenge von serialized Skeleton

                            accessor = colorFiles[x].CreateViewAccessor(); // Reader fuer aktuelle MMF
                            accessor.ReadArray<byte>(0, receivedBytes, 0, receivedBytes.Length); // liest Nachricht von Konsolenanwendung

                            mutex.ReleaseMutex();

                            //Dispatcher.Invoke(DispatcherPriority.Send, new Action(() => DrawImage(x, receivedBytes)));
                            Dispatcher.Invoke(DispatcherPriority.Send, new Action(() => DrawSmallImage(x, receivedBytes, kopfXPos, kopfYPos, distanzKopfZuHals*2)));
                            //Dispatcher.Invoke(DispatcherPriority.Send, new Action(() => DrawImage(x, receivedBytes)));

                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Fehler beim Zeichnen" + ex.ToString());
                            //colorMutex.ReleaseMutex(); // gibt Mutex wieder frei
                            mutex.ReleaseMutex(); // gibt Mutex wieder frei
                            foreach (Process p in processes)
                            {
                                p.Kill(); // beendet alle Konsolenanwendungen
                            }
                        }
                    }

                    Thread.Sleep(25); // wartet ein bisschen
                }  
            }

        }

        private void DrawLine(Point p1, Point p2)
        {
            using (DrawingContext dc = this.rightDrawingGroup.Open())
            {
                dc.DrawRectangle(null, new Pen(Brushes.Gray, 1), new Rect(0.0, 0.0, 640.0, 480.0));
                
                dc.DrawLine(
                    new Pen(Brushes.Green, 6),
                    p1,
                    p2
                );
            }
        }

        void DrawImage(int image, byte[] imageBytes)
        {
            images[image].WritePixels(
                new Int32Rect(0, 0, 640, 480),
                imageBytes,
                images[image].PixelWidth * sizeof(int),
                0);
        }

        void DrawSmallImage(int imagePos, byte[] imageBytes, int xPos, int yPos, int size)
        {
            WriteableBitmap wb = new WriteableBitmap(640, 480, 96.0, 96.0, PixelFormats.Bgr32, null);
            wb.WritePixels(
                new Int32Rect(0, 0, 640, 480),
                imageBytes,
                wb.PixelWidth * sizeof(int),
                0);
            int cropXPos = xPos - size / 2;
            int cropYPos = yPos - size / 2;

            if (cropXPos < 0) cropXPos  = 0;
            if (cropXPos > 640 - size) cropXPos = 640 - size;
            if (cropYPos < 0) cropYPos = 0;
            if (cropYPos > 480 - size) cropYPos = 480 - size;

            CroppedBitmap chained = new CroppedBitmap(wb, new Int32Rect(cropXPos, cropYPos, size, size));
            RightDepthStream.Source = chained;
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

        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            DepthImagePoint depthPoint = sensors[0].CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }

        private void Window_Closing_1(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // TODO: Kinect Sensoren beenden
            
            foreach (Process p in processes)
            {
                p.Kill(); // beendet auch die Konsolenanwendungen
            }
        }
    }
}
