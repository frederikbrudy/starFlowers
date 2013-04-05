using System;
using System.Collections.Generic;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Collections;
using System.Diagnostics;
using System.Threading;
using System.IO.MemoryMappedFiles;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace MultiProcessMain
{
    public partial class MainWindow : Window
    {
        private bool runningGameThread = false;
        List<Process> processes; // Prozesse der Kinect-Konsolenanwendung
        static long MemoryMappedFileCapacitySkeleton = 168; //10MB in Byte
        // Mutual Exclusion verhinder gleichzeitig Zugriff der Prozesse auf zu lesende Daten
        static Mutex mutex; 
        // enthaelt zu lesende Daten, die die Prozesse generieren
        static MemoryMappedFile file1;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded_1(object sender, RoutedEventArgs e)
        {
            processes = new List<Process>();
            
            // erzeugt und Startet Thread fuer Behandlung von Daten der Konsolenanwendungen
            var mappedfileThread = new Thread(this.MemoryMapData);
            mappedfileThread.SetApartmentState(ApartmentState.STA);
            mappedfileThread.Start();
        }

        private void MemoryMapData()
        {
            this.runningGameThread = true;

            file1 = MemoryMappedFile.CreateNew("SkeletonExchange", MemoryMappedFileCapacitySkeleton);
            Mutex mutex = new Mutex(true, "mappedfilemutex");
            mutex.ReleaseMutex(); //Freigabe gleich zu Beginn

            ArrayList processes = new ArrayList();
            for (int i = 0; i < 2; i++) // startet 2 Prozesse (TODO: durch Anzahl Kinect-Sensoren ersetzen)
            {
                Process p = new Process();
                p.StartInfo.CreateNoWindow = false; // laesst das Fenster fuer jeden Prozess angezeigt (fuer Debugging)
                p.StartInfo.FileName = "C:\\Users\\Janko\\Documents\\Visual Studio 2012\\Projects\\MultiProcess\\MultiProcessKinect\\bin\\Debug\\MultiProcessKinect.exe"; // die exe im bin-ordner von der Konsolenapplikation (TODO: durch relativen Pfad ersetzen)

                p.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Normal;
                p.Start();
                processes.Add(p); // zu Liste der Prozesse hinzufuegen
            }

                using (var accessor = file1.CreateViewAccessor())
                {
                    int[] someNumbers = new int[8]; // zum Test weren erstmal Zahlen uebertragen

                    while (this.runningGameThread)
                    {   
                        try
                        {
                            mutex = Mutex.OpenExisting("mappedfilemutex");
                            mutex.WaitOne(); // sichert alleinigen Zugriff
                            // liest Daten aus der MemoryMappedFiles
                            String xmlObject = getStringFromMemoryMappedFile(accessor);

                            //accessor.ReadArray(0, someNumbers, 0, someNumbers.Length); 
                            /*var output="";
                            foreach (int someNumber in someNumbers)
                            {
                                output += (someNumber + " ");
                            }*/
                            Console.WriteLine(xmlObject);                           
                            mutex.ReleaseMutex(); // gibt Mutex wieder frei
                            Thread.Sleep(50); // wartet 50 ms                     
                        }
                        catch (Exception ex)
                        {
                            mutex.ReleaseMutex(); // gibt Mutex wieder frei
                            foreach (Process p in processes)
                            {
                                p.Kill(); // beendet alle Konsolenanwendungen
                            }
                        }
                        
                    }
                }


        }

        private String getStringFromMemoryMappedFile(MemoryMappedViewAccessor accessor)
        {
            ushort Size = accessor.ReadUInt16(54);
            byte[] Buffer = new byte[Size];
            accessor.ReadArray(54 + 2, Buffer, 0, Buffer.Length);
            return ASCIIEncoding.ASCII.GetString(Buffer);
        }

        public static object DeserializeBase64(string s)
        {
            // We need to know the exact length of the string - Base64 can sometimes pad us by a byte or two
            int p = s.IndexOf(':');
            int length = Convert.ToInt32(s.Substring(0, p));

            // Extract data from the base 64 string!
            byte[] memorydata = Convert.FromBase64String(s.Substring(p + 1));
            MemoryStream rs = new MemoryStream(memorydata, 0, length);
            BinaryFormatter sf = new BinaryFormatter();
            object o = sf.Deserialize(rs);
            return o;
        }

        private void Window_Closing_1(object sender, System.ComponentModel.CancelEventArgs e)
        {
            foreach (Process p in processes)
            {
                p.Kill(); // beendet auch die Konsolenanwendungen
            }
        }
    }
}
