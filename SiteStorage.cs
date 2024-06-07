using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Microsoft.Azure.Functions.Worker;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace appsvc_fnc_dev_userstats
{
    class SiteStorage
    {
        private readonly ILogger<SiteStorage> _logger;

        public SiteStorage(ILogger<SiteStorage> logger)
        {
            _logger = logger;
        }

        [Function("SiteStorage")]
        public async Task Run([TimerTrigger("0 12 * * 0")] TimerInfo myTimer)
        {
            IConfiguration config = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: true, reloadOnChange: true).AddEnvironmentVariables().Build();

            string connectionString = config["AzureWebJobsStorage"];
            string containerName = "groupsitestorage";

            List<Group> GroupList = new List<Group>();
            List<Drives> drivesList = new List<Drives>();
            List<Folders> folderListItems = new List<Folders>();

            string groupId;
            string groupDisplayName;
            string quotaRemaining = "";
            string quotaTotal = "";
            string quotaUsed = "";
            string driveId;
            string driveName;
            string driveType;
            string fileId;
            string fileName;
            string createdDate;
            string lastModifiedDateTime;

            var groupData = await GetGroupsAsync(_logger);

            foreach (var group in groupData)
            {
                groupId = group.id;
                groupDisplayName = group.displayName;

                var groups = new List<Task<dynamic>>
                {
                    GetDriveDataAsync(groupId, _logger)
                };

                await Task.WhenAll(groups);

                foreach (var driveData in groups)
                {
                    dynamic drive = await driveData;

                    quotaRemaining = drive[0].quota.remaining;
                    quotaTotal = drive[0].quota.total;
                    quotaUsed = drive[0].quota.used;


                    drivesList = new List<Drives>();

                    foreach (var item in drive)
                    {

                        driveId = item.id;
                        driveName = item.name;
                        driveType = item.driveType;

                        var driveListItems = new List<Task<dynamic>>
                        {
                             GetFolderListsAsync(groupId, driveId, _logger)
                        };

                        await Task.WhenAll(driveListItems);

                        folderListItems = new List<Folders>();

                        foreach (var listItemsTask in driveListItems)
                        {
                            dynamic listItems = await listItemsTask;

                            if (listItems != null)
                            {

                                try
 
                                {
                                    foreach (var listItem in listItems)
                                    {
                                         fileId = listItem.id;
                                        fileName = listItem.contentType.name;
                                        createdDate = listItem.createdDateTime;
                                        lastModifiedDateTime = listItem.lastModifiedDateTime;

                                        if (listItem != null)
                                        {
                                            folderListItems.Add(new Folders(fileId, fileName, createdDate, lastModifiedDateTime));
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError($"Error:{ex.Message}");
                                    _logger.LogError($"Stack Trace:{ex.StackTrace}");
                                }
                            }
                        }
                        drivesList.Add(new Drives(driveId, driveName, driveType, folderListItems));
                    }
                }

                GroupList.Add(new Group(
                    groupId,
                    groupDisplayName,
                    quotaRemaining,
                    quotaUsed,
                    quotaTotal,
                    drivesList
               ));
            }

            await CreateContainerIfNotExists(connectionString, containerName);

            string FileTitle = DateTime.Now.ToString("dd-MM-yyyy") + "-" + containerName + ".json";
            _logger.LogInformation($"File {FileTitle}");

            BlobClient blobClient = new BlobClient(connectionString, containerName, FileTitle);

            string jsonFile = JsonConvert.SerializeObject(GroupList, Formatting.Indented);

            using (MemoryStream ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(jsonFile)))
            {
                await blobClient.UploadAsync(ms, true); // overwrite existing = true
            }

            BlobHttpHeaders headers = new BlobHttpHeaders();
            headers.ContentType = "application/json";
            await blobClient.SetHttpHeadersAsync(headers);
        }

        private static async Task<dynamic> GetGroupsAsync(ILogger log)
        {
            var unified = "groupTypes/any(c:c eq 'Unified')";
            var requestUri = $"https://graph.microsoft.com/v1.0/groups?$filter={unified}&$select=id,displayName&$top=20";

            return await SendGraphRequestAsync(requestUri, "1", log);
        }

        private static async Task<dynamic> GetDriveDataAsync(string groupId, ILogger log)
        {
            var requestUri = $"https://graph.microsoft.com/v1.0/groups/{groupId}/Drives";
            return await SendGraphRequestAsync(requestUri, "2", log);
        }

        private static async Task<dynamic> GetFolderListsAsync(string groupId, string driveId, ILogger log)
        {
            var requestUri = $"https://graph.microsoft.com/v1.0/groups/{groupId}/Drives/{driveId}/list/items?$select=id,createdDateTime,lastModifiedDateTime,contentType";
            return await SendGraphRequestAsync(requestUri, "3", log);
        }

        private static async Task<dynamic> SendGraphRequestAsync(string requestUri, string batchId, ILogger log)
        {
            int maxRetryCount = 3;
            int retryDelayInSeconds = 3000;

            Auth auth = new Auth();
            var graphAPIAuth = auth.graphAuth(log);

            BatchRequestContentCollection batchCollection = new BatchRequestContentCollection(graphAPIAuth);
            BatchResponseContentCollection myResponse = null;
            BatchRequestStep step = new BatchRequestStep(batchId, new HttpRequestMessage(HttpMethod.Get, requestUri));

            JArray valueAll;

            try
            {
                batchCollection.AddBatchRequestStep(step);
                HttpResponseMessage response = null;

                for (int retryCount = 0; retryCount <= maxRetryCount; retryCount++)
                {
                    myResponse = await graphAPIAuth.Batch.PostAsync(batchCollection);
                    response = await myResponse.GetResponseByIdAsync(batchId);

                    if (response.Headers.Contains("Retry-After"))
                    {
                        log.LogInformation($"Received a throttle response. Retrying in {retryDelayInSeconds} seconds.");
                        await Task.Delay(TimeSpan.FromSeconds(retryDelayInSeconds)); // Sleep for the specified delay before retrying
                        retryDelayInSeconds *= 2; // Exponential backoff for retry delay
                    }
                    else
                    {
                        break;
                    }
                }

                if (myResponse != null)
                {
                    var responseBody = await new StreamReader(response.Content.ReadAsStreamAsync().Result).ReadToEndAsync();
                    dynamic responseData = JsonConvert.DeserializeObject<dynamic>(responseBody);
                    var nextPageLink = responseData["@odata.nextLink"];

                    JArray value = responseData["value"];

                    valueAll = value;

                    while (nextPageLink != null)
                    {
                        step.Request.RequestUri = nextPageLink;

                        batchCollection = new BatchRequestContentCollection(graphAPIAuth);
                        batchCollection.AddBatchRequestStep(step);

                        myResponse = await graphAPIAuth.Batch.PostAsync(batchCollection);

                        if (myResponse != null)
                        {
                            response = await myResponse.GetResponseByIdAsync(batchId);
                            responseBody = await new StreamReader(response.Content.ReadAsStreamAsync().Result).ReadToEndAsync();

                            responseData = JsonConvert.DeserializeObject<dynamic>(responseBody);
                            nextPageLink = responseData["@odata.nextLink"];
                            value = responseData["value"];

                            valueAll.Merge(value);
                        }
                        else
                        {
                            nextPageLink = null;
                        }
                    }

                    return valueAll;
                }
                else
                {
                    log.LogError("Max retry count reached. Unable to proceed.");
                    log.LogError($"batchResponse null: {myResponse == null}");
                    return null;
                }
            }
            catch (Exception e)
            {
                log.LogError($"Message: {e.Message}");
                if (e.InnerException is not null) log.LogError($"InnerException: {e.InnerException.Message}");
                log.LogError($"StackTrace: {e.StackTrace}");
                return null;
            }
        }

        public async Task CreateContainerIfNotExists(string connectionString, string containerName)
        {
            _logger.LogInformation($"Create container check: {containerName}");
            BlobContainerClient container = new BlobContainerClient(connectionString, containerName);
            await container.CreateIfNotExistsAsync();
            _logger.LogInformation($"Container check complete.");
        }
    }
}