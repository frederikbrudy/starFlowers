using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Threading;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.Kinect;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MultiProcessKinect
{
    static class MultiProcessKinect
    {
        private const DepthImageFormat DepthFormat = DepthImageFormat.Resolution640x480Fps30;
        private const ColorImageFormat ColorFormat = ColorImageFormat.RgbResolution640x480Fps30;      
        
        private static Mutex mutex;
        private static MemoryMappedFile skeletonFile, depthFile, colorFile;
        private static MemoryMappedViewAccessor skeletonWriter, depthWriter, colorWriter;
        private static KinectSensor sensor;
        private static Skeleton skeleton;
        private static Skeleton[] skeletons;

        private static byte[] colorPixels;
        private static DepthImagePixel[] depthPixels;

        static void Main(string[] args)
        {
            String processID = args[0];
            String kinectID = args[1];

            try
            {
                mutex = Mutex.OpenExisting("mappedfilemutex");
                mutex.ReleaseMutex();
            }
            catch (Exception ex) {}

            try
            {
                // Jeder Prozess hat eigene MMF fuer Skeleton-Austausch
                skeletonFile = MemoryMappedFile.OpenExisting("SkeletonExchange" + processID);
                skeletonWriter = skeletonFile.CreateViewAccessor();

                depthFile = MemoryMappedFile.OpenExisting("DepthExchange" + processID);
                depthWriter = depthFile.CreateViewAccessor();

                colorFile = MemoryMappedFile.OpenExisting("ColorExchange" + processID);
                colorWriter = colorFile.CreateViewAccessor();

                sensor = null;

                foreach (var potentialSensor in KinectSensor.KinectSensors)
                {
                    // nimmt den Sensor, dessen UniqueID von der Hauptanwendung uebergeben wurde
                    if (potentialSensor.UniqueKinectId == kinectID && potentialSensor.Status == KinectStatus.Connected)
                    {
                        sensor = potentialSensor;
                        break;
                    }
                }

                if (sensor != null)
                {
                    sensor.DepthStream.Enable(DepthFormat);
                    sensor.ColorStream.Enable(ColorFormat);
                    sensor.SkeletonStream.Enable();

                    depthPixels = new DepthImagePixel[sensor.DepthStream.FramePixelDataLength];
                    colorPixels = new byte[sensor.ColorStream.FramePixelDataLength];

                    sensor.AllFramesReady += SensorAllFramesReady;

                    try
                    {
                        sensor.Start();
                    }
                    catch (IOException)
                    {
                        sensor = null;
                    }
                }
      
            }
            catch (Exception e)
            {
            }

            while (true) /// Endlosschleife, damit der Prozess offen bleibt
            {            
                Thread.Sleep(1000);
            }
  
        }

        private static void SensorAllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            
            bool skeletonFound = false;
            
            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    Skeleton[] tempSkeletons = new Skeleton[skeletonFrame.SkeletonArrayLength]; // alle 6 Skeletons
                    List<Skeleton> trackedSkeletons = new List<Skeleton>(); // alle Skeletons mit Status "tracked"

                    skeletonFrame.CopySkeletonDataTo(tempSkeletons);
                    foreach (Skeleton potentialSkeleton in tempSkeletons)
                    {
                        if (potentialSkeleton.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            trackedSkeletons.Add(potentialSkeleton);
                        }
                    }
                    // in skeletons sind nun nur noch Skeletons mit Status "tracked"
                    skeletons = trackedSkeletons.ToArray();
                    byte[] toSend = new byte[2255]; // 2255 byte ist die Laenge eines serialisierten Skeletons

                    
                    if (skeletons.Length > 0)
                    {
                        skeleton = skeletons[0]; // erstes Skelett nehmen...
                        toSend = ObjectToByteArray(skeleton); // ...und serialisieren
                        skeletonFound = true;
                    }
                    else // wenn kein Skeleton getracked wurde, schickt er nur Nullen
                    {
                        for (int i = 0; i < toSend.Length; i++)
                        {
                            toSend[i] = 0;
                        }
                    }

                    try
                    {
                        mutex.WaitOne(); // blockt den Zugriff
                        skeletonWriter.WriteArray<byte>(0, toSend, 0, toSend.Length); // schickt es weg
                        mutex.ReleaseMutex(); // gibt den Zugriff frei
                    }
                    catch (Exception ex)
                    {
                        mutex.ReleaseMutex();
                        Console.WriteLine("Fehler in beim Schreiben in MMF: " + ex.ToString());
                    }
                }

            }

            if (true) // ehem. if !skeletonFound
            {
                using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
                {
                    if (null != depthFrame)
                    {
                        depthFrame.CopyDepthImagePixelDataTo(depthPixels);
                        try
                        {
                            mutex = Mutex.OpenExisting("mappedfilemutex");
                            mutex.WaitOne(); // blockt den Zugriff
                            depthWriter.WriteArray<DepthImagePixel>(0, depthPixels, 0, depthPixels.Length); // schickt es weg            
                            mutex.ReleaseMutex(); // gibt den Zugriff frei
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Fehler in beim Schreiben in Depth-MMF: " + ex.ToString());
                        }
                    }
                }
            }

            if (true) // ehem. if skeletonfound
            {
                using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
                {
                    if (null != colorFrame)
                    {
                        colorFrame.CopyPixelDataTo(colorPixels);
                        try
                        {
                            mutex = Mutex.OpenExisting("mappedfilemutex");
                            mutex.WaitOne(); // blockt den Zugriff
                            colorWriter.WriteArray<byte>(0, colorPixels, 0, colorPixels.Length); // schickt es weg
                            mutex.ReleaseMutex(); // gibt den Zugriff frei
                        }
                        catch (Exception ex)
                        {
                            mutex.ReleaseMutex(); // gibt den Zugriff frei
                            Console.WriteLine("Fehler in beim Schreiben in Color-MMF: " + ex.ToString());
                        }
                    }
                }  
            }
            Thread.Sleep(25);

        }

        // serialisiert Object in Byte-Array
        private static byte[] ObjectToByteArray(Object obj)
        {
            if (obj == null)
                return null;
            BinaryFormatter bf = new BinaryFormatter();
            MemoryStream ms = new MemoryStream();
            bf.Serialize(ms, obj);
            return ms.ToArray();
        }

    } 
}
