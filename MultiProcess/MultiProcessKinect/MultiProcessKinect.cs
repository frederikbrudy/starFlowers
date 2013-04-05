using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Kinect;

namespace MultiProcessKinect
{
    class MultiProcessKinect
    {
        public static int testValue = 2;
        private static Mutex mutex;
        private static MemoryMappedFile file;
        private static MemoryMappedViewAccessor writer;

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
                file = MemoryMappedFile.OpenExisting("SkeletonExchange");
                writer = file.CreateViewAccessor();
                int[] randomNumbers = new int[8];
                var rndGenerator = new Random();
                // zum Debuggen wird der Prozess mit einer Zufallszahl versehen, die er sendet
                var processID = rndGenerator.Next(1000);

                while (true)
                {
                    
                    for (int x = 0; x < randomNumbers.Length; x++)
                    {   
                        //randomNumbers[x] = (int)(rndGenerator.Next(10));
                        randomNumbers[x] = (int)processID;
                    }

                    try
                    {
                        mutex.WaitOne();
                        writer.WriteArray(0, randomNumbers, 0, randomNumbers.Length);
                        mutex.ReleaseMutex();
                        Thread.Sleep(200);
                    }
                    catch (Exception ex)
                    {
                        mutex.ReleaseMutex();
                    }

                }
            }
            catch (FileNotFoundException e)
            {
                Console.WriteLine("Memory-mapped file does not exist.");
            }
  
        }

        /*    private void SensorSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
            {

                //SerializedSkeleton[] skeletons = new SerializedSkeleton[6];

                using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
                {
                    mutex.WaitOne();
                    writer.WriteArray(0, skeletons, 0, skeletons.Length);
                    mutex.ReleaseMutex();
                }
            }*/
    } 
}
