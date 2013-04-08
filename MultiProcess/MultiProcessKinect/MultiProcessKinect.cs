using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Threading;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.Kinect;

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
            
            try
            {
                mutex = Mutex.OpenExisting("mappedfilemutex");
                mutex.ReleaseMutex();
            }
            catch (Exception ex) {}

            try
            {
                String processID = args[0];
                String kinectID = args[1];

                file = MemoryMappedFile.OpenExisting("SkeletonExchange+processID");
                writer = file.CreateViewAccessor();

                foreach (var potentialSensor in KinectSensor.KinectSensors)
                {
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
            catch (FileNotFoundException e)
            {
                Console.WriteLine("Memory-mapped file does not exist.");
            }
  
        }

        private static void SensorSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            
            skeleton = new Skeleton();

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                    skeleton = skeletons[0];

                    try
                    {  
                        mutex.WaitOne();
                        byte[] toSend = ObjectToByteArray(skeleton);
                        writer.WriteArray<byte>(0, toSend, 0, toSend.Length);
                        mutex.ReleaseMutex();
                    }
                    catch (Exception ex)
                    {
                        mutex.ReleaseMutex();
                        Console.WriteLine("Fehler in beim Schreiben in MMF: " + ex.ToString());
                    }
                    Thread.Sleep(50);
                }

            }
        }

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
