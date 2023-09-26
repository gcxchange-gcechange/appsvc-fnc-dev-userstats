using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using System.Linq;
using System.Collections.Generic;
using System;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.IO;

namespace appsvc_fnc_dev_userstats
{
    class StorageData
    {
        [FunctionName("SiteStorage")]


        // public async Task<List<Group>> StorageDataAsync(ILogger log)
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log, ExecutionContext context)
        {
            IConfiguration config = new ConfigurationBuilder()

          .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
          .AddEnvironmentVariables()
          .Build();

            

            Auth auth = new Auth();
            var graphAPIAuth = auth.graphAuth(log);
            
            //the report requires Reports.Read.All permisisons
            try
            {
              var response  =  await graphAPIAuth.Reports.GetSharePointSiteUsageStorage("D7").Request().GetAsync();
                string requestBody = await new StreamReader(response.Content).ReadToEndAsync();
                //return new OkObjectResult(report);
                log.LogInformation($"REPORT: {response}");
            }
            catch (Exception ex)
            {
                log.LogInformation($"Error: {ex}");
            }
           

            

            var groups = await graphAPIAuth.Groups
                .Request()
                .Header("ConsistencyLevel", "eventual")
                .Filter("groupTypes/any(c:c eq 'Unified')")
                .GetAsync();


            string groupId;

            List<Group> GroupList = new List<Group>();

            foreach (var group in groups)
            {
                if (group.Id != null)

                {
                    groupId = group.Id;

                    var drives = await graphAPIAuth.Groups[groupId].Drives.Request().GetAsync();

                    
                  

                    foreach (var site in drives)

                    {

                        GroupList.Add(new Group(
                            groupId,
                            group.DisplayName,
                            site.Quota.Remaining.ToString(),
                            site.Quota.Used.ToString(),
                            site.Quota.Total.ToString()
                            ));
                    }


                }
            }

            string FileTitle = DateTime.Now.ToString("dd-MM-yyyy") + "-" + "siteStorage" + ".json";
           // log.LogInformation($"File {FileTitle}");

            string jsonFile = JsonConvert.SerializeObject(GroupList, Formatting.Indented);

            //log.LogInformation(jsonFile);


            //CloudStorageAccount storageAccount = GetCloudStorageAccount(context);
           // CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
           // CloudBlobContainer container = blobClient.GetContainerReference("groupSiteStorage");

           // CloudBlockBlob blob = container.GetBlockBlobReference(FileTitle);



            /*foreach (var group in GroupList)
            {
                log.LogInformation($"Group Info: Display Name: {group.displayName}, Group ID: {group.groupId}, Remaining Storage: {group.remainingStorage}, Used: {group.usedStorage}, Total: {group.totalStorage}");
            }*/

            return new OkResult();


        }

        private static CloudStorageAccount GetCloudStorageAccount(ExecutionContext executionContext)
        {
            var config = new ConfigurationBuilder()
                            .SetBasePath(executionContext.FunctionAppDirectory)
                            .AddJsonFile("local.settings.json", true, true)
                            .AddEnvironmentVariables().Build();
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(config["AzureWebJobsStorage"]);
            return storageAccount;

        }



        public class Group
        {
            public string groupId;
            public string displayName;
            public string remainingStorage;
            public string usedStorage;
            public string totalStorage;

            public Group(string groupId, string displayName, string remainingStorage, string usedStorage, string totalStorage)
            {
                this.groupId = groupId;
                this.displayName = displayName;
                this.remainingStorage = remainingStorage;
                this.usedStorage = usedStorage;
                this.totalStorage = totalStorage;
            }
        }
    }
}
