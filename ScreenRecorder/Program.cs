using System;
using SharpAvi;
using SharpAvi.Output;
using SharpAvi.Codecs;
using System.Drawing;
using System.Windows.Forms;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using System.Linq;
using RabbitMQ.Client;
using Newtonsoft.Json;

namespace ScreenRecorder
{
    class Program
    {
        [DllImport("kernel32.dll", ExactSpelling = true)]
        public static extern IntPtr GetConsoleWindow();
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [STAThreadAttribute]
        static void Main(string[] args)
        {
            Console.WriteLine("Initiate Login");
            
            var identity = new Identity();
            var user = identity.SignIn().GetAwaiter().GetResult();

            SetForegroundWindow(GetConsoleWindow());
            Console.WriteLine($"Logged in as: {user.Identity.Name}");
            Console.WriteLine("Press ENTER to start recording");
            Console.ReadLine();

            var recorder = new Recorder();

            var thread = new Thread(recorder.Record)
            {
                IsBackground = true
            };

            thread.Start();
            Console.WriteLine("Recording..." +
                Environment.NewLine +
                "Press ENTER to stop recording");

            Console.ReadLine();
            recorder.WaitHandler.Set();
            thread.Join();

            recorder.WaitHandler.Close();

            var stored = recorder.SaveFile();
            var azure = CloudStorageAccount.Parse(System.Configuration.ConfigurationManager.AppSettings["Storage"]);

            var blobClient = azure.CreateCloudBlobClient();
            var container = blobClient.GetContainerReference("videos-" + user.Claims.Single(x => x.Type == "sub").Value);
            container.CreateIfNotExists();

            var reference = container.GetBlockBlobReference(stored);
            reference.UploadFromFile(stored);

            ConnectionFactory factory = new ConnectionFactory();
            factory.Uri = new Uri(System.Configuration.ConfigurationManager.AppSettings["RabbitMQ"]);

            IConnection conn = factory.CreateConnection();
            IModel channel = conn.CreateModel();
            byte[] messageBodyBytes = System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new Message()
            {
                Container = "videos-" + user.Claims.Single(x => x.Type == "sub").Value,
                FileName = stored
            }));
            channel.BasicPublish("ScreenRecorder.Converter", "", null, messageBodyBytes);

            Console.WriteLine($"{stored} uploaded, it will be available very soon on: "
                + System.Configuration.ConfigurationManager.AppSettings["Web"]);
            Console.WriteLine("Press ENTER to quit");

            Console.ReadLine();
        }
    }
}
