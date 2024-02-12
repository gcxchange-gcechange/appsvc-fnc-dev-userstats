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

            var unified = new List<dynamic>();


            foreach (var groupItem in obj)
            {

                var groupTypes = ((JArray)groupItem.groupType).ToArray();
                if (groupTypes.Contains("Unified") )
                {
                    groupId = groupItem.groupId;
                    displayName = groupItem.displayName;
                    grouplist.Add(new Group(groupId, displayName));
                    //unified.Add(groupItem);
                } 

            }


            foreach(var group in grouplist)
            {
                var groupDrives = new List<Task<dynamic>>
                {
                    GetDriveDataAsync(group.groupId, log)
                };

                await Task.WhenAll(groupDrives);


                foreach (var groupDrive in groupDrives)
                {
                    dynamic drive = await groupDrive;
                    driveId = drive.id;
                    quotaRemaining = drive.quota.remaining;
                    quotaUsed = drive.quota.used; 
                    quotaTotal = drive.quota.total;

                    log.LogInformation($"DriveID{driveId}");
                }
            }

            //foreach (var group in unified)
            //{
            //    log.LogInformation($"GROUP:{group}");
            //    groupId= group.groupId;
            //    displayName = group.displayName;
            //}
            //    grouplist.Add(new Group(groupId, displayName));
           

        }

        private static async Task<dynamic> GetDriveDataAsync(string groupId, ILogger log)
        {
            var requestUri = $"https://graph.microsoft.com/v1.0/groups/{groupId}/Drive/?$select=id,quota";
            // log.LogInformation($"DriveURL2:{requestUri}");

            return await SendGraphRequestAsync(requestUri, "1", log);
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

            public Group (string groupId, string displayName)
            {
                this.groupId = groupId;
                this.displayName = displayName;
            }   
        }
        public class Folders
        {
            public string fileId;
            public string fileName;
            public string createdDate { get; set; }
            public string lasModifiedDate { get; set; }


            public Folders(string fileId, string fileName, string createdDate, string lastModifiedDate)
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
