using Azure;
using Azure.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

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

            //try
            //{
            
            //    var report = await graphAPIAuth.Reports.GetSharePointSiteUsageDetail("D7").Request().GetAsync();

                   
            //}
            //catch (Exception ex)
            //{
            //    log.LogInformation($"ERROR: {ex}");
            //}
            
       

            var groups = await graphAPIAuth.Groups
                .Request()
                .Header("ConsistencyLevel", "eventual")
                .Filter("groupTypes/any(c:c eq 'Unified')")
                .GetAsync();


            string groupId;
            string driveId;
            string quotaRemaining;
            string quotaTotal;
            string quotaUsed;

            List<Group> GroupList = new List<Group>();
            List<Drive> Drive = new List<Drive>();
            List<Folders> folderListItems = new List<Folders>();



            foreach (var group in groups)
            {
                    groupId = group.Id;

                    var drives = await graphAPIAuth.Groups[groupId].Drives.Request().GetAsync();
                    quotaRemaining = "";
                    quotaTotal = "";
                    quotaUsed = "";

                    foreach (var site in drives)

                    {
                        driveId = site.Id;
                        quotaRemaining = site.Quota.Remaining.ToString();
                        quotaTotal = site.Quota.Total.ToString();
                        quotaUsed = site.Quota.Used.ToString();



                        var drive = await graphAPIAuth.Groups[groupId].Drives[driveId].Root.Children.Request().GetAsync();

                        folderListItems = new List<Folders>();

                        foreach (var item in drive)

                        {

                            folderListItems.Add(new Folders(item.Id, item.Name, item.Size.ToString(), item.CreatedDateTime.ToString(), item.LastModifiedDateTime.ToString()));


                        }
                    }

                    GroupList.Add(new Group(
                        groupId,
                        group.DisplayName,
                        quotaRemaining,
                        quotaTotal,
                        quotaUsed,
                        folderListItems
                   ));
            }


            string FileTitle = DateTime.Now.ToString("dd-MM-yyyy") + "-" + "siteStorage" + ".json";
           // log.LogInformation($"File {FileTitle}");

            string jsonFile = JsonConvert.SerializeObject(GroupList, Formatting.Indented);

            log.LogInformation(jsonFile);


            //CloudStorageAccount storageAccount = GetCloudStorageAccount(context);
            // CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            // CloudBlobContainer container = blobClient.GetContainerReference("groupSiteStorage");

            // CloudBlockBlob blob = container.GetBlockBlobReference(FileTitle);



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
            public List<Folders> folderlist;




            public Group(string groupId, string displayName, string remainingStorage, string usedStorage, string totalStorage, List<Folders> folderlist)
            {
                this.groupId = groupId;
                this.displayName = displayName;
                this.remainingStorage = remainingStorage;
                this.usedStorage = usedStorage;
                this.totalStorage = totalStorage;
                this.folderlist = folderlist ?? new List<Folders>();


            }
        }

        public class Folders
        {
            public string fileId;
            public string fileName;
            public string fileSize;
            public string createdDateTime;
            public string lastModifiedDateTime;

            public Folders(string fileId, string fileName, string fileSize, string createdDateTime, string lastModifiedDateTime)
            {
                this.fileId = fileId;
                this.fileName = fileName;   
                this.fileSize = fileSize;
                this.createdDateTime = createdDateTime;
                this.lastModifiedDateTime = lastModifiedDateTime;   
            }
        }

        
    }
}
