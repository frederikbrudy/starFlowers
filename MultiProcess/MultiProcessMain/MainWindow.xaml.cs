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

namespace MultiProcessMain
{
    public partial class MainWindow : Window
    {
        private bool runningGameThread = false;

        public static List<KinectSensor> sensors; // alle angeschlossenen Sensoren
        List<Process> processes; // Prozesse der Kinect-Konsolenanwendung
        static long MemoryMappedFileCapacitySkeleton = 2255; // so gross ist die MMF in Byte
        // Mutual Exclusion verhinder gleichzeitig Zugriff der Prozesse auf zu lesende Daten
        static Mutex mutex; 
        // enthaelt zu lesende Daten, die die Prozesse generieren
        static MemoryMappedFile[] files;

        private const float RenderWidth = 640.0f;
        private const float RenderHeight = 480.0f;
        private const double JointThickness = 3;
        private const double BodyCenterThickness = 10;
        private const double ClipBoundsThickness = 10;
        private const int SkeletonObjektByteLength = 2255;
        private readonly Brush centerPointBrush = Brushes.Blue;
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));
        private readonly Brush inferredJointBrush = Brushes.Yellow;
        private readonly Pen trackedBonePen = new Pen(Brushes.Green, 6);
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);
        private DrawingGroup drawingGroup;
        private DrawingImage imageSource;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded_1(object sender, RoutedEventArgs e)
        {
            // Zeug initialisieren:
            processes = new List<Process>();
            sensors = new List<KinectSensor>();
            drawingGroup = new DrawingGroup();
            imageSource = new DrawingImage(drawingGroup);
            MyImage.Source = imageSource;

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

            files = new MemoryMappedFile[sensors.Count]; // es werden so viele MMFs wie angeschlossene Kinects erstellt
            try
            {
                Mutex mutex = new Mutex(true, "mappedfilemutex");
                mutex.ReleaseMutex(); //Freigabe gleich zu Beginn
            }
            catch (Exception e) {}

            for (int i = 0; i < sensors.Count; i++) // startet 2 Prozesse (TODO: durch Anzahl Kinect-Sensoren ersetzen)
            {
                files[i] = MemoryMappedFile.CreateNew("SkeletonExchange"+i, MemoryMappedFileCapacitySkeleton);
                
                Process p = new Process();
                p.StartInfo.CreateNoWindow = false; // laesst das Fenster fuer jeden Prozess angezeigt (fuer Debugging)
                p.StartInfo.FileName = "C:\\Users\\Janko\\Documents\\Visual Studio 2012\\Projects\\MultiProcess\\MultiProcessKinect\\bin\\Debug\\MultiProcessKinect.exe"; // die exe im bin-ordner von der Konsolenapplikation (TODO: durch relativen Pfad ersetzen)

                p.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Normal;
                p.StartInfo.Arguments = ""+i + " " + sensors[i].UniqueKinectId;
                p.Start();

                processes.Add(p); // zu Liste der Prozesse hinzufuegen
            }

                    while (this.runningGameThread)
                    {
                        foreach (var file in files) // fuer alle MMFs / Kinects
                        {
                            try
                            {
                                mutex = Mutex.OpenExisting("mappedfilemutex");
                                mutex.WaitOne(); // sichert alleinigen Zugriff

                                receivedBytes = new byte[SkeletonObjektByteLength]; // Byte-Array mit Laenge von serialized Skeleton
                                accessor = file.CreateViewAccessor(); // Reader fuer aktuelle MMF
                                accessor.ReadArray<byte>(0, receivedBytes, 0, receivedBytes.Length); // liest Nachricht von Konsolenanwendung
                                receivedObj = ByteArrayToObject(receivedBytes); // wandelt sie in Object oder null um
                                if (receivedObj != null)
                                {
                                    Skeleton receivedSkel = (Skeleton)(receivedObj); // Tadaa: hier ist das Skeleton

                                    // Testausgaben
                                    Console.WriteLine("Tracking ID:   " + receivedSkel.TrackingId);
                                    Console.WriteLine("Hash-Code:   " + receivedSkel.GetHashCode());
                                    Console.WriteLine("Tracking State:   "+ receivedSkel.TrackingState);
                                    var receivedSkelPos = receivedSkel.Position;
                                    Console.WriteLine("Position:   " + receivedSkelPos.X + " " + receivedSkelPos.Y + " " + receivedSkelPos.Z);
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

                            Thread.Sleep(100); // wartet 100 ms                     
                        }  
                    }

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

            }

            // Error occured, return null
            return null;
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
