using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage;
using System.IO;
using Newtonsoft.Json;

namespace appsvc_fnc_dev_userstats
{
    public class GroupsFile
    {
        [FunctionName("getJsonFile")]

        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] 
        HttpRequest req, ExecutionContext context, ILogger log)

        {
            log.LogInformation("Processed request");

            string containerName = "groupstats";
            log.LogInformation($"CN: {containerName}");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            containerName = containerName ?? data.ContainerName;


            string fileName = DateTime.Now.ToString("dd-MM-yyyy") + "-" + containerName + ".json";
          

            var config = new ConfigurationBuilder()
                .SetBasePath(context.FunctionAppDirectory)
                .AddJsonFile("local.settings.json", true, true)
                .AddEnvironmentVariables()
                .Build();


            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(config["CloudStorageAccount"]);

            //// Connect to the blob storage
            CloudBlobClient serviceClient = storageAccount.CreateCloudBlobClient();
            //// Connect to the blob container
            CloudBlobContainer container = serviceClient.GetContainerReference($"{containerName}");
            //// Connect to the blob file
            CloudBlockBlob blob = container.GetBlockBlobReference($"{fileName}");
            log.LogInformation($"Blob:{blob}");
            //download the file to Text
            string contents = blob.DownloadTextAsync().Result;

            getGroupDetails(contents, log );

            return new OkObjectResult(contents);

        }

        public static void getGroupDetails(string contents, ILogger log)
        {
            var obj = JsonConvert.DeserializeObject<dynamic>(contents);

            string groupId = "";
            string displayName = "";

            List<Group> grouplist = new List<Group>();

            var unified = new List<dynamic>();


            foreach (var groupItem in obj)
            {

                var groupTypes = ((JArray)groupItem.groupType).ToArray();
                if (groupTypes.Contains("Unified") )
                {
                    unified.Add(groupItem);
                    log.LogInformation("unified");
                }

            }

            foreach (var group in unified)
            {
                groupId= group.groupId;
                displayName = group.displayName;
            }
                grouplist.Add(new Group(groupId, displayName));
           

        }

        public class Group
        {
            public string groupId;
            public string displayName;

            public Group (string groupId, string displayName)
            {
                this.groupId = groupId;
                this.displayName = displayName;
            }   
        }
       











    }


}
