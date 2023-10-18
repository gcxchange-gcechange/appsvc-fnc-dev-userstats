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
using Microsoft.IdentityModel.Tokens;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.System, "get", "post", Route = null)] HttpRequest req, ILogger log, ExecutionContext context)
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
                            //log.LogInformation($"1{item}");
                            dynamic ids = await item;

                            foreach( var id in ids.value)
                            {
                                itemId = id.id;
                                //log.LogInformation($"ID:---{id.id}");

                                var itemIds = new List<Task<dynamic>>
                                {

                                GetFileDetailsAsync(siteId, listId, itemId, log)

                                };

                                await Task.WhenAll(itemIds);

                                foreach (var listItem in itemIds)
                                {

                                    dynamic fileDetails = await listItem;
                                    log.LogInformation($"NAME:{fileDetails.name}");
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

            //string FileTitle = DateTime.Now.ToString("dd-MM-yyyy") + "-" + "siteStorage" + ".json";
            // log.LogInformation($"File {FileTitle}");

            string jsonFile = JsonConvert.SerializeObject(GroupList, Formatting.Indented);

            log.LogInformation($"JSON: {jsonFile}");

            //CloudStorageAccount storageAccount = GetCloudStorageAccount(context);
            // CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            // CloudBlobContainer container = blobClient.GetContainerReference("groupSiteStorage");

            // CloudBlockBlob blob = container.GetBlockBlobReference(FileTitle);

            return new OkResult();

        }


        private static async Task<dynamic> GetGroupsAsync(ILogger log)
        {
            var unified = "groupTypes/any(c:c eq 'Unified')";
            var requestUri = $"https://graph.microsoft.com/v1.0/groups?$filter={unified}&$select=id,displayName&$expand";

            try
            {
                return await SendGraphRequestAsync(requestUri, "1", log);
            }
            catch (ServiceException error)
            {
                if (error.Error.InnerError.Code == "429")
                {
                    if (error.ResponseHeaders.Contains("Retry-After"))
                    {

                    }
                }
                else
                {
                    log.LogInformation($"Error: {error.Message}");
                }
            }
           

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

            var batchResponse = await graphAPIAuth.Batch.Request().PostAsync(batch);
            var responses = await batchResponse.GetResponsesAsync();

            var responseBody = await new StreamReader(responses[batchId].Content.ReadAsStreamAsync().Result).ReadToEndAsync();
           // log.LogInformation($"RB: {responseBody}");
            return JsonConvert.DeserializeObject(responseBody);
        }


        //private static CloudStorageAccount GetCloudStorageAccount(ExecutionContext executionContext)
        //{
        //    var config = new ConfigurationBuilder()
        //                    .SetBasePath(executionContext.FunctionAppDirectory)
        //                    .AddJsonFile("local.settings.json", true, true)
        //                    .AddEnvironmentVariables().Build();
        //    CloudStorageAccount storageAccount = CloudStorageAccount.Parse(config["AzureWebJobsStorage"]);
        //    return storageAccount;

        //}

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
