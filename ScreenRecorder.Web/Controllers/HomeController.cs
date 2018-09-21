using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using RabbitMQ.Client;
using ScreenRecorder.Web.Models;

namespace ScreenRecorder.Web.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly IConfiguration Configuration;
        public HomeController(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public async Task<IActionResult> Index()
        {
            var azure = CloudStorageAccount.Parse(Configuration.GetValue<string>("Storage"));

            var blobClient = azure.CreateCloudBlobClient();
            var container = blobClient.GetContainerReference("videos-" + User.Claims.Single(x => x.Type == "sub").Value);
            await container.CreateIfNotExistsAsync();

            var list = new List<CloudBlockBlob>();
            BlobContinuationToken blobContinuationToken = null;
            do
            {
                var results = await container.ListBlobsSegmentedAsync(null, blobContinuationToken);
                blobContinuationToken = results.ContinuationToken;
                foreach (IListBlobItem item in results.Results)
                {
                    var blob = (CloudBlockBlob)item;
                    list.Add(blob);
                }

            } while (blobContinuationToken != null); 

            return View(list.OrderByDescending(x => x.Properties.Created).ToList());
        }
        
        public async Task<IActionResult> ViewFile(string name)
        {
            var azure = CloudStorageAccount.Parse(Configuration.GetValue<string>("Storage"));

            var blobClient = azure.CreateCloudBlobClient();
            var container = blobClient.GetContainerReference("videos-" + User.Claims.Single(x => x.Type == "sub").Value);

            var blob = container.GetBlockBlobReference(name);

            return View(blob);
        }

        public IActionResult ForceProcess(string name)
        {
            ConnectionFactory factory = new ConnectionFactory();
            factory.Uri = new Uri("amqp://cbis:cbrabbitpass@rabbitmq.test.app.citybreak.com:5672/cbis.mq.citybreak.com");

            IConnection conn = factory.CreateConnection();
            IModel channel = conn.CreateModel();
            byte[] messageBodyBytes = System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new Message()
            {
                Container = "videos-" + User.Claims.Single(x => x.Type == "sub").Value,
                FileName = name
            }));
            channel.BasicPublish("ScreenRecorder.Converter", "", null, messageBodyBytes);

            return RedirectToAction("Index");
        }

        public async Task StreamFile(string name)
        {
            var azure = CloudStorageAccount.Parse(Configuration.GetValue<string>("Storage"));

            var blobClient = azure.CreateCloudBlobClient();
            var container = blobClient.GetContainerReference("videos-" + User.Claims.Single(x => x.Type == "sub").Value);

            var blob = container.GetBlockBlobReference(name);

            await blob.DownloadToStreamAsync(HttpContext.Response.Body);
        }
    }
}
