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
using Microsoft.Graph;
using System.Net.Http;
using System.Runtime.CompilerServices;

namespace appsvc_fnc_dev_userstats
{
    public class DriveDataStorage
    {
        [FunctionName("getDriveData")]

        public static async Task<IActionResult> Run([TimerTrigger("0 12 * * 0")] TimerInfo myTimer,  
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ExecutionContext context, ILogger log)
        // public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] 
        //HttpRequest req, ExecutionContext context, ILogger log)

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

            getGroupDetails(contents, log, context );


            return new OkObjectResult(contents);

        }

        public static async void getGroupDetails(string contents, ILogger log, ExecutionContext context)
        {
            var obj = JsonConvert.DeserializeObject<dynamic>(contents);

            string groupId = "";
            string displayName = "";
            string quotaRemaining = "";
            string quotaTotal = "";
            string quotaUsed = "";
            string driveId = "";
            string driveName ="";
            string driveType="";
            string fileId;
            string fileName; ;
            string createdDate;
            string lastModifiedDateTime;

            List<Group> grouplist = new List<Group>();

            List<DriveData> driveItemsData = new List<DriveData>();
            List<Folders> folderListItems = new List<Folders>();

            var unified = new List<dynamic>();

            //filter out unified group
            foreach (var groupItem in obj)
            {
                var groupTypes = ((JArray)groupItem.groupType).ToArray();
                if (groupTypes.Contains("Unified") )
                {
                    //groupId = groupItem.groupId;
                
                    unified.Add(groupItem);
                } 

            }

            // get the drive for each group

            foreach(var group in unified)
            {
                groupId = group.groupId;
                displayName = group.displayName;

                var groupDrives = new List<Task<dynamic>>
                {
                    GetDriveDataAsync(groupId, log)
                };

                await Task.WhenAll(groupDrives);

                driveItemsData = new List<DriveData>();

                //get each drive and it's data
                foreach (var groupDrive in groupDrives)
                {
                    dynamic drives = await groupDrive;

                   foreach (var drive in drives.value )
                   {
                      
                        driveId = drive.id;
                        driveName = drive.name;
                        driveType = drive.driveType;
                        quotaUsed = drive.quota.used;
                        quotaRemaining = drive.quota.remaining;
                        quotaTotal = drive.quota.total;
                      

                        var driveIds = new List<Task<dynamic>>
                        {
                            GetDriveItemListsAsync(groupId, driveId, log)
                        };

                        await Task.WhenAll(driveIds);

                        folderListItems = new List<Folders>();

                        foreach (var driveItems in driveIds)
                        {
                            dynamic driveItem = await driveItems;

                            foreach(var item in driveItem.value)
                            {
                                log.LogInformation($"ITEM:{item}");
                                fileId = item.id;
                                fileName = item.webUrl;
                                createdDate = item.createdDateTime;
                                lastModifiedDateTime = item.lastModifiedDateTime;  
                                folderListItems.Add(new Folders( fileId, fileName, createdDate, lastModifiedDateTime));
                            }
                           
                        }

                        driveItemsData.Add(new DriveData(driveId, driveName, driveType, quotaUsed, quotaRemaining, quotaTotal, folderListItems));
                   }

                }
                grouplist.Add(new Group(groupId, displayName, driveItemsData));

            }



            CreateContainerIfNotExists(context, "groupsitestorage", log);

            CloudStorageAccount storageAccount = GetCloudStorageAccount(context);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("groupsitestorage");

            string FileTitle = DateTime.Now.ToString("dd-MM-yyyy") + "-" + "groupsitestorage" + ".json";
            log.LogInformation($"File {FileTitle}");

            CloudBlockBlob blob = container.GetBlockBlobReference(FileTitle);



            string jsonFile = JsonConvert.SerializeObject( grouplist, Formatting.Indented);
            log.LogInformation($"JSON: {jsonFile}");

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

        private static async Task<dynamic> GetDriveDataAsync(string groupId, ILogger log)
        {
            var requestUri = $"https://graph.microsoft.com/v1.0/groups/{groupId}/Drives/?$select=id,name,driveType,quota";
            // log.LogInformation($"DriveURL2:{requestUri}");

            return await SendGraphRequestAsync(requestUri, "1", log);
        }

        private static async Task<dynamic> GetDriveItemListsAsync(string groupId, string driveId, ILogger log)
        {
            var requestUri = $"https://graph.microsoft.com/v1.0/groups/{groupId}/Drives/{driveId}/list/items";
             //log.LogInformation($"Folder List 3:{requestUri}");
            return await SendGraphRequestAsync(requestUri, "2", log);
        }

        private static async Task<dynamic> SendGraphRequestAsync(string requestUri, string batchId, ILogger log)
        {
            Auth auth = new Auth();
            var graphAPIAuth = auth.graphAuth(log);
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        try
            {

                //create batch container
                var batch = new BatchRequestContent();

                //create new batch step 
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
                    log.LogInformation($"RB: {responseBody}");
                    return JsonConvert.DeserializeObject(responseBody);
                }
                else
                {
                    log.LogError("Max retry count reached. Unable to proceed.");
                    return null;
                }
            } 
            catch(Exception ex)
            {
                log.LogError($"Message: {ex.Message}");
                if (ex.InnerException is not null) log.LogError($"InnerException: {ex.InnerException.Message}");
                log.LogError($"StackTrace: {ex.StackTrace}");
                return null;
            }
          
        }


        public class Group
        {
            public string groupId;
            public string displayName;
            public List<DriveData> driveItemsData;
          


            public Group(string groupId, string displayName, List<DriveData> driveItemsData)
            {
                this.groupId = groupId;
                this.displayName = displayName;
                this.driveItemsData = driveItemsData;
            }
        }
      
        public class DriveData
        {
            public string driveId;
            public string driveName;
            public string driveType;
            public string quotaUsed;
            public string quotaRemaining;
            public string quotaTotal;

            public List<Folders> folderListItems;

            public DriveData(string driveId, string driveName, string driveType, string quotaUsed, string quotaRemaining, string quotaTotal, List<Folders> folderListItems)
            {
                this.driveId = driveId;
                this.driveName = driveName;
                this.driveType = driveType;
                this.quotaUsed = quotaUsed;
                this.quotaRemaining = quotaRemaining;
                this.quotaTotal = quotaTotal;
                this.folderListItems = folderListItems;


            }
            public override string ToString()
            {
                return this.quotaUsed;
            }
        }

        public class Folders
        {
            public string fileId;
            public string fileName;
            public string createdDate;
            public string lastModifiedDate;


            public Folders(string fileId, string fileName, string createdDate, string lastModifiedDate)
            {
                this.fileId = fileId;
                this.fileName = fileName;
                this.createdDate = createdDate;
                this.lastModifiedDate = lastModifiedDate;

            }
        }
 
    
    }

}



