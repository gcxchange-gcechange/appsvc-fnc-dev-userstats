using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Collections.Generic;
using Microsoft.Graph;

namespace appsvc_fnc_dev_userstats
{
    public static class StoreData
    {
        [FunctionName("StoreData")]
        //Run everyday at 3am
        public static async Task Run([TimerTrigger("0 0 3 * * *")]TimerInfo myTimer, ILogger log, ExecutionContext context )
        {
            log.LogInformation($"C# Http trigger function executed at: {DateTime.Now}");

            //Get UserStats
            var userdata = new UserStats();
            var usersdata = await userdata.UserStatsDataAsync(log);
            var ResultUsersStore = await StoreDataUserFile(context, usersdata, "userstats", log);

            //Get GroupStats
            var groupdata = new GroupStats();
            var groupsdata = await groupdata.GroupStatsDataAsync(log);
            var ResultGroupsStore = await StoreDataGroupFile(context, groupsdata, "groupstats", log);

            //Get ActiveUsers
            var activeuserdata = new ActiveUsers();
            var activeusersdata = await activeuserdata.ActiveUsersDataAsync(log);
            var ResultAcvtiveUsersStore = await StoreDataActiveUsersFile(context, activeusersdata, "activeusers", log);

            //   var ResultGroupsStore = await StoreDataGroupFile(context, usersdata, "userstats", log);


            var groupSiteStorage = new SiteStorage();
            var groupSiteStorageData = await groupSiteStorage.groupSiteStorageDataAsync(log, context);

            string responseMessage = ResultUsersStore
                ? "Work as it should"
                : $"Something went wrong. Check the logs";

            //return new OkObjectResult(responseMessage);
        }



        public static async Task<bool> StoreDataUserFile(ExecutionContext context, List<appsvc_fnc_dev_userstats.usersData> usersdata, string containerName, ILogger log)
        {
            CreateContainerIfNotExists(log, context, containerName);

            CloudStorageAccount storageAccount = GetCloudStorageAccount(context);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(containerName);

            //CreateFileTitle with date
            DateTime now = DateTime.Now;
            string FileTitle = now.ToString("dd-MM-yyyy") + "-" + containerName + ".json";
            //log.LogInformation($"File {FileTitle}");

            CloudBlockBlob blob = container.GetBlockBlobReference(FileTitle);

            //Create file with userData
            List<usersData> listUsersData = new List<usersData>();
            foreach (var user in usersdata)
            {
                log.LogInformation($"In storeFile function {user.Id} - {user.creationDate}");

                listUsersData.Add(new usersData()
                {
                    Id = user.Id,
                    creationDate = user.creationDate,
                });
            }


            string json = JsonConvert.SerializeObject(listUsersData.ToArray());

            blob.Properties.ContentType = "application/json";

            using (var ms = new MemoryStream())
            {
                LoadStreamWithJson(ms, json);
                await blob.UploadFromStreamAsync(ms);
            }
            log.LogInformation($"Bolb {FileTitle} is uploaded to container {container.Name}");
            await blob.SetPropertiesAsync();

            return true;
        }

        public static async Task<bool> StoreDataGroupFile(ExecutionContext context, List<SingleGroup> groupsdata, string containerName, ILogger log)
        {
            CreateContainerIfNotExists(log, context, containerName);

            CloudStorageAccount storageAccount = GetCloudStorageAccount(context);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(containerName);

            //CreateFileTitle with date
            DateTime now = DateTime.Now;
            string FileTitle = now.ToString("dd-MM-yyyy") + "-" + containerName + ".json";
           // log.LogInformation($"File {FileTitle}");

            CloudBlockBlob blob = container.GetBlockBlobReference(FileTitle);

            //Create file with userData
            List<groupsData> listGroupsData = new List<groupsData>();
            foreach (var group in groupsdata)
            {
                log.LogInformation($"In storeFile function {group.groupId} - {group.creationDate}");

                listGroupsData.Add(new groupsData()
                {
                    displayName = group.displayName,
                    countMember = group.countMember,
                    groupId = group.groupId,
                    creationDate = group.creationDate,
                    description = group.description,
                    groupType = group.groupType,
                    userlist = group.userlist
                });
            }

            string json = JsonConvert.SerializeObject(listGroupsData.ToArray());

            blob.Properties.ContentType = "application/json";

            using (var ms = new MemoryStream())
            {
                LoadStreamWithJson(ms, json);
                await blob.UploadFromStreamAsync(ms);
            }
            log.LogInformation($"Bolb {FileTitle} is uploaded to container {container.Name}");
            await blob.SetPropertiesAsync();

            return true;
        }

        public static async Task<bool> StoreDataActiveUsersFile(ExecutionContext context, List<appsvc_fnc_dev_userstats.countactiveuserData> activeusersdata, string containerName, ILogger log)
        {
            CreateContainerIfNotExists(log, context, containerName);

            CloudStorageAccount storageAccount = GetCloudStorageAccount(context);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(containerName);

            //CreateFileTitle with date
            DateTime now = DateTime.Now;
            string FileTitle = now.ToString("dd-MM-yyyy") + "-" + containerName + ".json";
           // log.LogInformation($"File {FileTitle}");

            CloudBlockBlob blob = container.GetBlockBlobReference(FileTitle);

            //Create file with userData
            List<countactiveuserData> allactiveusersdata = new List<countactiveuserData>();
            foreach (var user in activeusersdata)
            {
                log.LogInformation($"In storeFile function {user.name} - {user.countActiveusers}");

                allactiveusersdata.Add(new countactiveuserData()
                {
                    name = user.name,
                    countActiveusers = user.countActiveusers,
                });
            }


            string json = JsonConvert.SerializeObject(allactiveusersdata.ToArray());

            blob.Properties.ContentType = "application/json";

            using (var ms = new MemoryStream())
            {
                LoadStreamWithJson(ms, json);
                await blob.UploadFromStreamAsync(ms);
            }
            log.LogInformation($"Bolb {FileTitle} is uploaded to container {container.Name}");
            await blob.SetPropertiesAsync();

            return true;
        }

        private static async void CreateContainerIfNotExists(ILogger logger, ExecutionContext executionContext, string ContainerName)
        {
            CloudStorageAccount storageAccount = GetCloudStorageAccount(executionContext);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            string[] containers = new string[] { ContainerName };
            logger.LogInformation("Create container");
            foreach (var item in containers)
            {
                CloudBlobContainer blobContainer = blobClient.GetContainerReference(item);
               await blobContainer.CreateIfNotExistsAsync();
            }
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
        private static void LoadStreamWithJson(Stream ms, object obj)
        {
            StreamWriter writer = new StreamWriter(ms);
            writer.Write(obj);
            writer.Flush();
            ms.Position = 0;
        }
    }
}