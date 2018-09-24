using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace ScreenRecorder.Converter
{
    class Program
    {
        public static bool IsDebug
        {
            get
            {
                bool isDebug = false;
#if DEBUG
                isDebug = true;
#endif
                return isDebug;
            }
        }

        static void Main(string[] args)
        {
            var config = new ConfigurationBuilder()
                            .AddJsonFile($"appsettings{(IsDebug ? ".local" : "")}.json", optional: false, reloadOnChange: true)
                            .Build();

            ConnectionFactory factory = new ConnectionFactory();
            factory.Uri = new Uri(config["RabbitMQ"]);
  
            IConnection conn = factory.CreateConnection();
            IModel channel = conn.CreateModel();
            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += async (ch, ea) =>
            {
                var body = ea.Body;
                var message = JsonConvert.DeserializeObject<Message>(System.Text.Encoding.UTF8.GetString(body));

                var azure = CloudStorageAccount.Parse(config["Storage"]);

                var blobClient = azure.CreateCloudBlobClient();
                var container = blobClient.GetContainerReference(message.Container);
                await container.CreateIfNotExistsAsync();
                var reference = container.GetBlockBlobReference(message.FileName);
                await reference.DownloadToFileAsync(message.FileName, FileMode.OpenOrCreate);

                var newName = message.FileName.Substring(0, message.FileName.Length - 4) + ".mp4";

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "ffmpeg.exe",
                        Arguments = $"-i {message.FileName} {newName}"
                    }
                };
                process.Start();
                process.WaitForExit();

                var convertedReference = container.GetBlockBlobReference(newName);
                await convertedReference.UploadFromFileAsync(newName);

                await reference.DeleteAsync();

                File.Delete(message.FileName);
                File.Delete(newName);

                channel.BasicAck(ea.DeliveryTag, false);
            };

            channel.ExchangeDeclare("ScreenRecorder.Converter", ExchangeType.Direct);
            channel.QueueDeclare("ScreenRecorder.Converter", false, false, false, null);
            channel.QueueBind("ScreenRecorder.Converter", "ScreenRecorder.Converter", "", null);

            string consumerTag = channel.BasicConsume("ScreenRecorder.Converter", false, consumer);
        }
    }
}
