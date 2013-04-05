using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Kinect;
using System.Xml.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace MultiProcessKinect
{
    static class MultiProcessKinect
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
                String[] serializedSkeletons = new String[1];


                var rndGenerator = new Random();
                // zum Debuggen wird der Prozess mit einer Zufallszahl versehen, die er sendet
                var processID = rndGenerator.Next(1000);
                Skeleton skel = new Skeleton();

                skel.TrackingState = SkeletonTrackingState.PositionOnly;
                SkeletonPoint testPkt = new SkeletonPoint();
                testPkt.X = 10;
                testPkt.Y = 20;
                testPkt.Z = 30;
                skel.Position = testPkt;
                serializedSkeletons[0] = SerializeObject<Skeleton>(skel);

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
                        writeStringToMemoryMappedFile(SerializeBase64(skel), writer);
                        //writer.WriteArray(0, serializedSkeletons, 0, serializedSkeletons.Length);
                        //writer.WriteArray(0, randomNumbers, 0, randomNumbers.Length);
                        
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

        public static string SerializeObject<T>(this T toSerialize)
        {
            //try
            //{
                XmlSerializer xmlSerializer = new XmlSerializer(toSerialize.GetType());
                StringWriter textWriter = new StringWriter();

                xmlSerializer.Serialize(textWriter, toSerialize);
                return textWriter.ToString();
            //}
            //catch (Exception e)
            //{
            //    Console.WriteLine(e.ToString());
            //    return "";
            //}

        }

        public static string SerializeBase64(object o)
        {
            // Serialize to a base 64 string
            byte[] bytes;
            long length = 0;
            MemoryStream ws = new MemoryStream();
            BinaryFormatter sf = new BinaryFormatter();
            sf.Serialize(ws, o);
            length = ws.Length;
            bytes = ws.GetBuffer();
            string encodedData = bytes.Length + ":" + Convert.ToBase64String(bytes, 0, bytes.Length, Base64FormattingOptions.None);
            return encodedData;
        }

        public static void writeStringToMemoryMappedFile(String stringToWrite, MemoryMappedViewAccessor accessor)
        {
            try
            {
                ASCIIEncoding encoding = new ASCIIEncoding();
                byte[] Buffer = encoding.GetBytes(stringToWrite);
                accessor.Write(54, (ushort)Buffer.Length);
                accessor.WriteArray(54 + 2, Buffer, 0, Buffer.Length);
            }
            catch (Exception e)
            {

            }
            

        }
    } 
}
