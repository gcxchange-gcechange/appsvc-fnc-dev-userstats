using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Collections.Generic;

namespace appsvc_fnc_dev_userstats
{
    public static class StoreData
    {
        [FunctionName("StoreData")]
        // Run everyday at 3am
        public static async Task Run([TimerTrigger("0 0 3 * * *")]TimerInfo myTimer, ILogger log, ExecutionContext context )
        {
            log.LogInformation($"StoreData function received a request at: {DateTime.Now}");
            log.LogInformation(" ");

            //Get UserStats
            var userdata = new UserStats();
            var usersdata = await userdata.UserStatsDataAsync(log);
            var ResultUsersStore = await StoreDataUserFile(context, usersdata, "userstats", log);
            log.LogInformation(" ");

            //Get GroupStats
            var groupdata = new GroupStats();
            var groupsdata = await groupdata.GroupStatsDataAsync(log);
            var ResultGroupsStore = await StoreDataGroupFile(context, groupsdata, "groupstats", log);
            log.LogInformation(" ");

            //Get ActiveUsers
            var activeuserdata = new ActiveUsers(usersdata, log);
            var activeusersdata = await activeuserdata.GetActiveUserCount();
            var ResultAcvtiveUsersStore = await StoreDataActiveUsersFile(context, activeusersdata, "activeusers", log);
            log.LogInformation(" ");

            log.LogInformation($"StoreData function processed a request at: {DateTime.Now}");
        }

        public static async Task<bool> StoreDataUserFile(ExecutionContext context, List<appsvc_fnc_dev_userstats.usersData> usersdata, string containerName, ILogger log)
        {
            CreateContainerIfNotExists(log, context, containerName);

            CloudStorageAccount storageAccount = GetCloudStorageAccount(context);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(containerName);

            // CreateFileTitle with date
            DateTime now = DateTime.Now;
            string FileTitle = now.ToString("dd-MM-yyyy") + "-" + containerName + ".json";

            CloudBlockBlob blob = container.GetBlockBlobReference(FileTitle);

            // Create file with userData
            List<usersData> listUsersData = new List<usersData>();
            foreach (var user in usersdata)
            {
                listUsersData.Add(new usersData()
                {
                    Id = user.Id,
                    creationDate = user.creationDate,
                    mail = user.mail
                });
            }

            string json = JsonConvert.SerializeObject(listUsersData.ToArray());

            blob.Properties.ContentType = "application/json";

            using (var ms = new MemoryStream())
            {
                LoadStreamWithJson(ms, json);
                await blob.UploadFromStreamAsync(ms);
            }
            log.LogInformation($"Blob {FileTitle} is uploaded to container: {container.Name}");
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

            CloudBlockBlob blob = container.GetBlockBlobReference(FileTitle);

            //Create file with userData
            List<groupsData> listGroupsData = new List<groupsData>();
            foreach (var group in groupsdata)
            {
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
            log.LogInformation($"Blob {FileTitle} is uploaded to container: {container.Name}");
            await blob.SetPropertiesAsync();

            return true;
        }

        public static async Task<bool> StoreDataActiveUsersFile(ExecutionContext context, List<countactiveuserData> activeusersdata, string containerName, ILogger log)
        {
            CreateContainerIfNotExists(log, context, containerName);

            CloudStorageAccount storageAccount = GetCloudStorageAccount(context);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(containerName);

            // Create file title with date
            DateTime now = DateTime.Now;
            string FileTitle = now.ToString("dd-MM-yyyy") + "-" + containerName + ".json";

            // Create file with userData
            CloudBlockBlob blob = container.GetBlockBlobReference(FileTitle);
            blob.Properties.ContentType = "application/json";

            string json = JsonConvert.SerializeObject(activeusersdata.ToArray());

            using (var ms = new MemoryStream())
            {
                LoadStreamWithJson(ms, json);
                await blob.UploadFromStreamAsync(ms);
            }

            await blob.SetPropertiesAsync();

            log.LogInformation($"Blob {FileTitle} is uploaded to container: {container.Name}");

            return true;
        }

        private static async void CreateContainerIfNotExists(ILogger logger, ExecutionContext executionContext, string ContainerName)
        {
            CloudStorageAccount storageAccount = GetCloudStorageAccount(executionContext);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            string[] containers = new string[] { ContainerName };
            logger.LogInformation($"Create container check: {ContainerName}");
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