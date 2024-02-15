
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
using static Microsoft.Graph.Constants;
using System.Runtime.CompilerServices;

namespace appsvc_fnc_dev_userstats
{
    class SiteStorage
    {
        [FunctionName("SiteStorage")]

        public static async Task Run([TimerTrigger("0 12 * * 0")] TimerInfo myTimer, ILogger log, ExecutionContext context)
       // public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.System, "get", "post", Route = null)] HttpRequest req, ILogger log, ExecutionContext context)

        {
            IConfiguration config = new ConfigurationBuilder()


            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();


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


            var groupData = await GetGroupsAsync(log);

            foreach (var group in groupData)
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
                             GetFolderListsAsync(groupId, driveId, log)
                        };

                        await Task.WhenAll(driveListItems);

                        folderListItems = new List<Folders>();

                        foreach (var listItemsTask in driveListItems) 
                        {
                            dynamic listItems = await listItemsTask;

                            if (listItems != null ) {

                                try
                                {
                                    foreach (var listItem in listItems)
                                    {
                                        log.LogInformation($"listItem:{listItem}");
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
                                catch(Exception ex)
                                {
                                    log.LogError($"Error:{ ex.Message}");
                                    log.LogError($"Stack Trace:{ex.StackTrace}");
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

            // return new OkResult();

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
            //log.LogInformation($"DriveURL2:{requestUri}");

            return await SendGraphRequestAsync(requestUri, "2", log);
        }

        private static async Task<dynamic> GetFolderListsAsync(string groupId, string driveId, ILogger log)
        {
            var requestUri = $"https://graph.microsoft.com/v1.0/groups/{groupId}/Drives/{driveId}/list/items?$select=id,createdDateTime,lastModifiedDateTime,contentType";
            log.LogInformation($"Folder List 3:{requestUri}");
            
            return await SendGraphRequestAsync(requestUri, "3", log);
        }

      

        private static async Task<dynamic> SendGraphRequestAsync(string requestUri, string batchId, ILogger log)
        {
            int maxRetryCount = 3;
            int retryDelayInSeconds = 3000;

            var batch = new BatchRequestContent();
            BatchResponseContent batchResponse = null;

            Auth auth = new Auth();
            var graphAPIAuth = auth.graphAuth(log);
            BatchRequestStep step = new BatchRequestStep(batchId, new HttpRequestMessage(HttpMethod.Get, requestUri));

            JArray valueAll;

            try
            {
                batch.AddBatchRequestStep(step);

                Dictionary<string, HttpResponseMessage> response = null;

                for (int retryCount = 0; retryCount <= maxRetryCount; retryCount++)
                {
                    batchResponse = await graphAPIAuth.Batch.Request().PostAsync(batch);
                    response = await batchResponse.GetResponsesAsync();

                    if (response[batchId].Headers.Contains("Retry-After"))
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
                    var responseBody = await new StreamReader(response[batchId].Content.ReadAsStreamAsync().Result).ReadToEndAsync();

                    dynamic responseData = JsonConvert.DeserializeObject<dynamic>(responseBody);
                    log.LogInformation($"{responseBody}");
                    var nextPageLink = responseData["@odata.nextLink"];
                    //log.LogInformation($"1{responseData}");
                    log.LogInformation($"{nextPageLink}");
                    JArray value = responseData["value"];

                    valueAll = value;

                    while (nextPageLink != null)
                    {
                        step.Request.RequestUri = nextPageLink;
                        batch.AddBatchRequestStep(step);
                        batchResponse = await graphAPIAuth.Batch.Request().PostAsync(batch);

                        if (batchResponse != null)
                        {
                            response = await batchResponse.GetResponsesAsync();
                            responseBody = await new StreamReader(response[batchId].Content.ReadAsStreamAsync().Result).ReadToEndAsync();

                            responseData = JsonConvert.DeserializeObject<dynamic>(responseBody);
                            nextPageLink = responseData["@odata.nextLink"];
                            log.LogInformation($"WHILE LOOP_RESDATA:{responseData}");
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

    
        private static async void CreateContainerIfNotExists(ExecutionContext executionContext, string containerName, ILogger log)
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
            public string remainingStorage;
            public string usedStorage;
            public string totalStorage;

            public List<Drives> drivesList;


            public Group(string groupId, string displayName,  string remainingStorage, string usedStorage, string totalStorage,  List<Drives> drivesList ) 
            {
                this.groupId = groupId;
                this.displayName = displayName;
                this.remainingStorage = remainingStorage;
                this.usedStorage = usedStorage;
                this.totalStorage = totalStorage;
                this.drivesList = drivesList ?? new List<Drives>();

            }
        }

        public class Folders
        {
            public string fileId;
            public string fileName;
            public string createdDate { get; set; }
            public string lasModifiedDate { get; set; }


            public Folders(string fileId, string fileName , string createdDate, string lastModifiedDate)
            {
                this.fileId = fileId;
                this.fileName = fileName;
                this.createdDate = createdDate;
                this.lasModifiedDate = lastModifiedDate;

            }
        }

        public class Drives
        {
            public string driveId;
            public string driveName;
            public string driveType;
            public List <Folders> folderListItems;


            public Drives(string driveId, string driveName, string driveType, List<Folders> folderListItems)
            {
                this.driveId = driveId;
                this.driveName = driveName;
                this.driveType = driveType;
                this.folderListItems = folderListItems;

               
            }
        }

        

    }
}