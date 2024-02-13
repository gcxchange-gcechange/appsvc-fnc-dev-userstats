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

        public static async void getGroupDetails(string contents, ILogger log)
        {
            var obj = JsonConvert.DeserializeObject<dynamic>(contents);

            string groupId = "";
            string displayName = "";
            string quotaRemaining = "";
            string quotaTotal = "";
            string quotaUsed = "";
            string driveId;
            //string driveName;
            //string driveType;
            //string fileId;
            //string fileName;
            //string createdDate;
            //string lastModifiedDateTime;

            List<Group> grouplist = new List<Group>();

            List<DriveQuota> driveQuotaData = new List<DriveQuota>();
            List<Drives> drivesList = new List<Drives>();
            List<Folders> folderListItems = new List<Folders>();

            var unified = new List<string>();

            //filter out unified group
            foreach (var groupItem in obj)
            {

                var groupTypes = ((JArray)groupItem.groupType).ToArray();
                if (groupTypes.Contains("Unified") )
                {
                    groupId = groupItem.groupId;

                    unified.Add(groupId);
                } 

            }

            // get the drive for each group

            foreach(var group in unified)
            {
                var groupDrives = new List<Task<dynamic>>
                {
                    GetDriveDataAsync(group, log)
                };

                await Task.WhenAll(groupDrives);

                driveQuotaData = new List<DriveQuota>();


                foreach (var groupDrive in groupDrives)
                {
                    dynamic drive = await groupDrive;

                    log.LogInformation($"DriveID{drive}");

                    driveId = drive.id;
                    displayName= drive.owner.group.displayName;
                    quotaRemaining = drive.quota.remaining;
                    quotaUsed = drive.quota.used; 
                    quotaTotal = drive.quota.total;
                  

                    driveQuotaData.Add(new DriveQuota(quotaUsed,quotaRemaining, quotaTotal));

                    //drivesList.Add(new Drives(driveId));

                    //var driveIds = new List<Task<dynamic>>
                    //{
                    //    GetDriveItemListsAsync(group.groupId, driveId, log)
                    //};

                    //await Task.WhenAll(driveIds);

                }
            grouplist.Add(new Group(groupId, displayName, driveQuotaData));

            }
            

            




            string jsonFile = JsonConvert.SerializeObject( grouplist, Formatting.Indented);
            log.LogInformation($"JSON: {jsonFile}");

        }

        private static async Task<dynamic> GetDriveDataAsync(string groupId, ILogger log)
        {
            var requestUri = $"https://graph.microsoft.com/v1.0/groups/{groupId}/Drive/?$select=id,owner,quota";
            // log.LogInformation($"DriveURL2:{requestUri}");

            return await SendGraphRequestAsync(requestUri, "1", log);
        }

        private static async Task<dynamic> GetDriveItemListsAsync(string groupId, string driveId, ILogger log)
        {
            var requestUri = $"https://graph.microsoft.com/v1.0/groups/{groupId}/Drives/{driveId}/list?select=id,name,displayName,createdDateTime,createdBy,lastModifiedDateTime,lastModifiedBy,parentReference";
             log.LogInformation($"Folder List 3:{requestUri}");
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
                log.LogInformation($"ERROR:{ex}");
            }
            return null;
        }


        public class Group
        {
            public string groupId;
            public string displayName;
            public List<DriveQuota> driveQuotaData;
          


            public Group(string groupId, string displayName, List<DriveQuota> driveQuotaData)
            {
                this.groupId = groupId;
                this.displayName = displayName;
                this.driveQuotaData = driveQuotaData;
            }
        }
        public class DriveQuota
        {
            public string quotaUsed { get; set; }
            public string quotaRemaining { get; set; }
            public string quotaTotal { get; set; }

            public DriveQuota(string quotaUsed, string quotaRemaining, string quotaTotal)
            {
                this.quotaUsed = quotaUsed;
                this.quotaRemaining = quotaRemaining;
                this.quotaTotal = quotaTotal;
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

        public class Drives
        {
            public string driveId;
            public string driveName;
            public string driveType;
            public List<Folders> folderListItems;


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



