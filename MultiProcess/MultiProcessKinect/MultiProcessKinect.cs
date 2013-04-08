using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Threading;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.Kinect;
using System.Collections.Generic;

namespace MultiProcessKinect
{
    static class MultiProcessKinect
    {
        private static Mutex mutex;
        private static MemoryMappedFile file;
        private static MemoryMappedViewAccessor writer;
        private static KinectSensor sensor;
        private static Skeleton skeleton;
        private static Skeleton[] skeletons;

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
                file = MemoryMappedFile.OpenExisting("SkeletonExchange"+processID);
                writer = file.CreateViewAccessor();

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
                    sensor.SkeletonStream.Enable();
                    sensor.SkeletonFrameReady += SensorSkeletonFrameReady;
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
                //Console.WriteLine((sensor != null));
                //Console.WriteLine(processID);
                //Console.WriteLine(kinectID);
                //Thread.Sleep(30);
            }
  
        }

        private static void SensorSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            
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

                    try
                    {
                        if (skeletons.Length > 0)
                        {
                            skeleton = skeletons[0]; // erstes Skelett nehmen...
                            toSend = ObjectToByteArray(skeleton); // ...und serialisieren
                        }
                        else // wenn kein Skeleton getracked wurde, schickt er nur Nullen
                        {
                            for (int i = 0; i < toSend.Length; i++ )
                            {
                                toSend[i] = 0;
                            }
                        }
                        mutex.WaitOne(); // blockt den Zugriff
                        writer.WriteArray<byte>(0, toSend, 0, toSend.Length); // schickt es weg
                        mutex.ReleaseMutex(); // gibt den Zugriff frei
                    }
                    catch (Exception ex)
                    {
                        mutex.ReleaseMutex();
                        Console.WriteLine("Fehler in beim Schreiben in MMF: " + ex.ToString());
                    }
                    Thread.Sleep(50); // wartet 50 ms (TODO: in korrekte Zeit aendern)
                }

            }
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
