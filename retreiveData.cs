using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;

namespace appsvc_fnc_dev_userstats
{
    public static class RetreiveData
    {
        [FunctionName("RetreiveData")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log, ExecutionContext context)
        {
            string containerName = req.Query["containerName"];
            //string dateParam = req.Query["selectedDate"];

            log.LogInformation($"name:{containerName}");
           // log.LogInformation($"date:{dateParam}");


            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            log.LogInformation($"body: {data}");
            containerName = containerName ?? data?.containerName;
            string date = data?.selectedDate;
            
            
           // dateParam = dateParam ?? data?.dateParam;

            log.LogInformation($"name:{containerName}");
            log.LogInformation($"date2:{date}");

            // check if date is default (today) or different date
           
            string fileName = date.ToString() + "-" + containerName + ".json";
           // string fileName = DateTime.Now.ToString("dd-MM-yyyy") + "-" + containerName + ".json";
            var Getdata = GetBlob(containerName, fileName, context, log);

            return new OkObjectResult(Getdata);
        }
        public static string GetBlob(string containerName, string fileName, ExecutionContext executionContext, ILogger log)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(executionContext.FunctionAppDirectory)
                .AddJsonFile("local.settings.json", true, true)
                .AddEnvironmentVariables().Build();
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(config["CloudStorageAccount"]);

            // Connect to the blob storage
            CloudBlobClient serviceClient = storageAccount.CreateCloudBlobClient();
            // Connect to the blob container
            CloudBlobContainer container = serviceClient.GetContainerReference($"{containerName}");
            // Connect to the blob file
            CloudBlockBlob blob = container.GetBlockBlobReference($"{fileName}");
            // Get the blob file as text
            string contents = blob.DownloadTextAsync().Result;
            return contents;
        }
    }
}