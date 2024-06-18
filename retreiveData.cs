using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;
using Azure.Storage.Blobs;
using System;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace appsvc_fnc_dev_userstats
{
    public class RetreiveData
    {
        private readonly ILogger<RetreiveData> _logger;

        public RetreiveData(ILogger<RetreiveData> logger)
        {
            _logger = logger;
        }

        [Function("RetreiveData")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req, ExecutionContext context)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);

            string containerName = req.Query["containerName"];
            containerName = containerName ?? data?.containerName;
            string date = data?.selectedDate;

            _logger.LogInformation($"containerName:{containerName}");
            _logger.LogInformation($"date:{date}");

            string fileName = date.ToString() + "-" + containerName + ".json";

            var Getdata = GetBlob(containerName, fileName, context);

            return new OkObjectResult(Getdata);
        }

        private string GetBlob(string containerName, string fileName, ExecutionContext executionContext)
        {
            string contents = string.Empty;

            try
            {
                IConfiguration config = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: true, reloadOnChange: true).AddEnvironmentVariables().Build();
                BlobClient blobClient = new BlobClient(config["CloudStorageAccount"], containerName, fileName);
                contents = blobClient.DownloadContentAsync().Result.Value.Content.ToString();
            }
            catch (Exception e)
            {
                _logger.LogError("!! Exception !!");
                _logger.LogError(e.Message);
                _logger.LogError("!! StackTrace !!");
                _logger.LogError(e.StackTrace);
            }

            return contents;
        }
    }
}