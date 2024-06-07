using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using Microsoft.Azure.Functions.Worker;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace appsvc_fnc_dev_userstats
{
    public class StoreData
    {
        private readonly ILogger<StoreData> _logger;

        public StoreData(ILogger<StoreData> logger)
        {
            _logger = logger;
        }

        [Function("StoreData")]
        // Run everyday at 3am
        public async Task Run([TimerTrigger("0 0 3 * * *")] TimerInfo myTimer)
        {
            _logger.LogInformation($"StoreData function received a request at: {DateTime.Now}");
            _logger.LogInformation(" ");


            var config = new ConfigurationBuilder().AddJsonFile("appsettings.json", true, true).AddEnvironmentVariables().Build();
            string connectionString = config["AzureWebJobsStorage"];

            //Get UserStats
            var userdata = new UserStats();
            var usersdata = await userdata.UserStatsDataAsync(_logger);
            var ResultUsersStore = await StoreDataUserFile(usersdata, connectionString, "userstats");
            _logger.LogInformation(" ");

            //Get GroupStats
            var groupdata = new GroupStats();
            var groupsdata = await groupdata.GroupStatsDataAsync(_logger);
            var ResultGroupsStore = await StoreDataGroupFile(groupsdata, connectionString, "groupstats");
            _logger.LogInformation(" ");

            //Get ActiveUsers
            var activeuserdata = new ActiveUsers(usersdata, _logger);
            var activeusersdata = await activeuserdata.GetActiveUserCount();
            var ResultAcvtiveUsersStore = await StoreDataActiveUsersFile(activeusersdata, connectionString, "activeusers");
            _logger.LogInformation(" ");

            _logger.LogInformation($"StoreData function processed a request at: {DateTime.Now}");
        }

        public async Task<bool> StoreDataUserFile(List<usersData> usersdata, string connectionString, string containerName)
        {

            try {
                await CreateContainerIfNotExists(connectionString, containerName);

                // CreateFileTitle with date
                DateTime now = DateTime.Now;
                string FileTitle = now.ToString("dd-MM-yyyy") + "-" + containerName + ".json";

                _logger.LogInformation($"FileTitle: {FileTitle}");

                // Create file with userData
                BlobClient blobClient = new BlobClient(connectionString, containerName, FileTitle);

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

                using (MemoryStream ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
                {
                    LoadStreamWithJson(ms, json);
                    await blobClient.UploadAsync(ms, true); // overwrite existing = true
                }

                BlobHttpHeaders headers = new BlobHttpHeaders();
                headers.ContentType = "application/json";
                await blobClient.SetHttpHeadersAsync(headers);

                _logger.LogInformation($"Blob {FileTitle} is uploaded to container: {containerName}");

                return true;
            }
            catch (Exception e)
            {
                _logger.LogError("!! Exception !!");
                _logger.LogError(e.Message);
                _logger.LogError("!! StackTrace !!");
                _logger.LogError(e.StackTrace);

                return false;
            }
        }

        public async Task<bool> StoreDataGroupFile(List<SingleGroup> groupsdata, string connectionString, string containerName)
        {
            try {
                await CreateContainerIfNotExists(connectionString, containerName);

                //CreateFileTitle with date
                DateTime now = DateTime.Now;
                string FileTitle = now.ToString("dd-MM-yyyy") + "-" + containerName + ".json";

                BlobClient blobClient = new BlobClient(connectionString, containerName, FileTitle);

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

                using (var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
                {
                    LoadStreamWithJson(ms, json);
                    await blobClient.UploadAsync(ms, true); // overwrite existing = true
                }

                BlobHttpHeaders headers = new BlobHttpHeaders();
                headers.ContentType = "application/json";
                await blobClient.SetHttpHeadersAsync(headers);

                _logger.LogInformation($"Blob {FileTitle} is uploaded to container: {containerName}");

                return true;
            }
            catch (Exception e)
            {
                _logger.LogError("!! Exception !!");
                _logger.LogError(e.Message);
                _logger.LogError("!! StackTrace !!");
                _logger.LogError(e.StackTrace);

                return false;
            }
        }

        public async Task<bool> StoreDataActiveUsersFile(List<countactiveuserData> activeusersdata, string connectionString, string containerName)
        {
            try {
                await CreateContainerIfNotExists(connectionString, containerName);
                
                // Create file title with date
                DateTime now = DateTime.Now;
                string FileTitle = now.ToString("dd-MM-yyyy") + "-" + containerName + ".json";

                // Create file with activeusersdata
                BlobClient blobClient = new BlobClient(connectionString, containerName, FileTitle);

                string json = JsonConvert.SerializeObject(activeusersdata.ToArray());

                using (var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
                {
                    LoadStreamWithJson(ms, json);
                    await blobClient.UploadAsync(ms, true); // overwrite existing = true
                }

                BlobHttpHeaders headers = new BlobHttpHeaders();
                headers.ContentType = "application/json";
                await blobClient.SetHttpHeadersAsync(headers);

                _logger.LogInformation($"Blob {FileTitle} is uploaded to container: {containerName}");

                return true;
            }
            catch (Exception e)
            {
                _logger.LogError("!! Exception !!");
                _logger.LogError(e.Message);
                _logger.LogError("!! StackTrace !!");
                _logger.LogError(e.StackTrace);

                return false;
            }
        }

        public async Task CreateContainerIfNotExists(string connectionString, string containerName)
        {
            _logger.LogInformation($"Create container check: {containerName}");
            BlobContainerClient container = new BlobContainerClient(connectionString, containerName);
            await container.CreateIfNotExistsAsync();
            _logger.LogInformation($"Container check complete.");
        }

         private void LoadStreamWithJson(Stream ms, string obj)
        {
            StreamWriter writer = new StreamWriter(ms);
            writer.Write(obj);
            ms.Position = 0;
        }
    }
}