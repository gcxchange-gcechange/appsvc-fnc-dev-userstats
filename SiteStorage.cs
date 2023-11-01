
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;

namespace appsvc_fnc_dev_userstats
{
    class SiteStorage
    {
        [FunctionName("SiteStorage")]

        public static async Task  Run([TimerTrigger("0 0 3 * * 1")] TimerInfo myTimer, ILogger log, ExecutionContext context)

        {
            IConfiguration config = new ConfigurationBuilder()


            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();


            List<Group> GroupList = new List<Group>();
            List<Folders> folderListItems = new List<Folders>();

            string groupId;
            string groupDisplayName;
            string driveId = "";
            string quotaRemaining = "";
            string quotaTotal = "";
            string quotaUsed = "";
            string siteId;
            string listId;
            string itemId;
            string fileId;
            string fileName;
            string fileSize;
            string createdDate;
            string createdBy;
            string lastModifiedDate;
            string lastModifiedBy;


            var groupData = await GetGroupsAsync(log);

            foreach (var group in groupData.value)
            {
                groupId = group.id;
                groupDisplayName = group.displayName;

                var groups = new List<Task<dynamic>> 
                {
                    GetDriveDataAsync(groupId, log)
                };

                await Task.WhenAll(groups);


                foreach (var driveData in groups)

                {
                    dynamic drive = await driveData;

                    driveId = drive.id;
                    quotaRemaining = drive.quota.remaining;
                    quotaTotal = drive.quota.total;
                    quotaUsed = drive.quota.used;


                    var driveList = new List<Task<dynamic>> {
                        GetFolderListsAsync(groupId, driveId, log)
                    };

                    await Task.WhenAll(driveList);

                    foreach (var lists in driveList)
                    {
                        dynamic list = await lists;
                        listId = list.id;
                        siteId = list.parentReference.siteId;

                        var listItems= new List<Task<dynamic>>
                        {
                            GetAllFolderListItemsAsync(groupId, driveId, log)
                        };

                        await Task.WhenAll(listItems);

                        folderListItems = new List<Folders>();

                        foreach (var item in listItems)
                        {
                            dynamic ids = await item;

                            foreach( var id in ids.value)
                            {
                                itemId = id.id;

                                var itemIds = new List<Task<dynamic>>
                                {

                                GetFileDetailsAsync(siteId, listId, itemId, log)

                                };

                                await Task.WhenAll(itemIds);

                                foreach (var listItem in itemIds)
                                {

                                    dynamic fileDetails = await listItem;
                                      fileId = fileDetails.id;
                                      fileName = fileDetails.name;
                                      fileSize = fileDetails.size;
                                      createdDate = fileDetails.createdDateTime;
                                      createdBy = fileDetails.createdBy.user.displayName;
                                      lastModifiedDate = fileDetails.lastModifiedDateTime;
                                      lastModifiedBy = fileDetails.lastModifiedBy.user.displayName;

                                    folderListItems.Add(new Folders(fileId, fileName, fileSize, createdDate, createdBy, lastModifiedDate, lastModifiedBy));     
                             
                                }
                            }  
                        }
                    }
                }

                GroupList.Add(new Group(
                    groupId,
                    groupDisplayName,
                    driveId,
                    quotaRemaining,
                    quotaUsed,
                    quotaTotal,
                    folderListItems
                   
               ));
            }


           CreateContainerIfNotExists(context, "groupsitestorage", log);

            CloudStorageAccount storageAccount = GetCloudStorageAccount(context);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("groupsitestorage");

            string FileTitle = DateTime.Now.ToString("dd-MM-yyyy") + "-" + "groupsitestorage" + ".json";
            log.LogInformation($"File {FileTitle}");

            CloudBlockBlob blob = container.GetBlockBlobReference(FileTitle);

            string jsonFile = JsonConvert.SerializeObject(GroupList, Formatting.Indented);


            log.LogInformation($"JSON: {jsonFile}");

            blob.Properties.ContentType = "application/json";

            using (MemoryStream ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(jsonFile)))
            {
                await blob.UploadFromStreamAsync(ms);
            }

            await blob.SetPropertiesAsync();


        }

        private static async Task<dynamic> GetGroupsAsync(ILogger log)
        {
            var unified = "groupTypes/any(c:c eq 'Unified')";
            var requestUri = $"https://graph.microsoft.com/v1.0/groups?$filter={unified}&$select=id,displayName";

            return await SendGraphRequestAsync(requestUri, "1", log);
        }

        private static async Task<dynamic> GetDriveDataAsync(string groupId, ILogger log)
        {
            var requestUri = $"https://graph.microsoft.com/v1.0/groups/{groupId}/Drive/?$select=id,quota";
           // log.LogInformation($"DriveURL2:{requestUri}");

            return await SendGraphRequestAsync(requestUri, "2", log);
        }

        private static async Task<dynamic> GetFolderListsAsync(string groupId, string driveId, ILogger log)
        {
            var requestUri = $"https://graph.microsoft.com/v1.0/groups/{groupId}/Drives/{driveId}/list?select=id,name,displayName,createdDateTime,createdBy,lastModifiedDateTime,lastModifiedBy,parentReference";
           // log.LogInformation($"Folder List 3:{requestUri}");
            return await SendGraphRequestAsync(requestUri, "3", log);
        }

        private static async Task<dynamic> GetAllFolderListItemsAsync(string groupId, string driveId, ILogger log)
        {

            var requestUri = $"https://graph.microsoft.com/v1.0/groups/{groupId}/Drives/{driveId}/list/items?select=id";
           // log.LogInformation($"LIST IDS:{requestUri}");
            return await SendGraphRequestAsync(requestUri, "4", log);
        }

        private static async Task<dynamic> GetFileDetailsAsync(string siteId, string listId, string itemId, ILogger log)
        {
            var requestUri = $"https://graph.microsoft.com/v1.0/sites/{siteId}/lists/{listId}/items/{itemId}/driveItem?select=createdDateTime,id,lastModifiedDateTime,name,webUrl,size,createdBy,lastModifiedBy";
            //log.LogInformation($"ITEMDETAILS 5:{requestUri}");
            return await SendGraphRequestAsync(requestUri, "5", log);
        }

        private static async Task<dynamic> SendGraphRequestAsync(string requestUri, string batchId, ILogger log)
        {
            Auth auth = new Auth();
            var graphAPIAuth = auth.graphAuth(log);
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            var batch = new BatchRequestContent();
            var batchRequest = new BatchRequestStep(batchId, request);
            batch.AddBatchRequestStep(batchRequest);

            BatchResponseContent batchResponse = null;
             
            int maxRetryCount = 3;  
            int retryDelayInSeconds = 3000;  

            for (int retryCount = 0; retryCount <= maxRetryCount; retryCount++)
            {
                batchResponse = await graphAPIAuth.Batch.Request().PostAsync(batch);
                var responses = await batchResponse.GetResponsesAsync();

                if (responses[batchId].Headers.Contains("Retry-After"))
                {
                    log.LogInformation($"Received a throttle response. Retrying in {retryDelayInSeconds} seconds.");
                    // Sleep for the specified delay before retrying
                    await Task.Delay(TimeSpan.FromSeconds(retryDelayInSeconds));
                    retryDelayInSeconds *= 2; // Exponential backoff for retry delay
                }
                else
                {
                    break;  
                }
            }

            if (batchResponse != null)
            {

                var response = await batchResponse.GetResponsesAsync();
                var responseBody = await new StreamReader(response[batchId].Content.ReadAsStreamAsync().Result).ReadToEndAsync();
               // log.LogInformation($"RB: {responseBody}");
                return JsonConvert.DeserializeObject(responseBody);
            }
            else
            {
                log.LogError("Max retry count reached. Unable to proceed.");
                return null;
            }
        }

        private static async void CreateContainerIfNotExists( ExecutionContext executionContext, string containerName, ILogger log)
        {
            CloudStorageAccount storageAccount = GetCloudStorageAccount(executionContext);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            string[] containers = new string[] { containerName };
            log.LogInformation("Create container");
            foreach (var item in containers)
            {
                log.LogInformation($"ITEM:{item}");
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

        public class Group
        {
            public string groupId;
            public string displayName;
            public string driveId;
            public string remainingStorage;
            public string usedStorage;
            public string totalStorage;

            public List<Folders> folderlist;




            public Group(string groupId, string displayName, string driveId, string remainingStorage, string usedStorage, string totalStorage,


                List<Folders> folderlist)
            {
                this.groupId = groupId;
                this.displayName = displayName;
                this.driveId = driveId;
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
            public string createdDate;
            public string createdBy;
            public string lastModifiedDateTime;
            public string lastModifiedBy;

            public Folders(string fileId, string fileName, string fileSize, string createdDate, string createdBy, string lastModifiedDateTime ,string lastModifiedBy)
            {
                this.fileId = fileId;
                this.fileName = fileName;
                this.fileSize = fileSize;
                this.createdDate = createdDate;
                this.createdBy = createdBy;
                this.lastModifiedDateTime = lastModifiedDateTime;
                this.lastModifiedBy = lastModifiedBy;
            }
        }


    }
}
