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
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.System, "get", "post", Route = null)] HttpRequest req,
            ILogger log, ExecutionContext context)
        {
            IConfiguration config = new ConfigurationBuilder()


            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();


            List<Group> GroupList = new List<Group>();
            List<Folders> folderListItems = new List<Folders>();
            List<GroupItem> groupItems = new List<GroupItem>();


            string groupId;
            string groupDisplayName;
            string driveId = "";
            string quotaRemaining = "";
            string quotaTotal = "";
            string quotaUsed = "";
            string siteId;
            string listId;
            string itemId;
            string fileId = "";
            string fileName = "";
            string fileSize = "";
            string createdDate = "";
            string createdBy = "";
            string lastModifiedDate = "";
            string lastModifiedBy = "";



            var groupData = await GetGroupsAsync(log);

            foreach (var group in groupData.value)
            {
                groupId = group.id;
                groupDisplayName = group.displayName;
                log.LogInformation($"GROUP 1:{group}");

                var groups = new List<Task<dynamic>> 
                {
                    GetDriveDataAsync(groupId, log)
                };

                await Task.WhenAll(groups);


                foreach (var driveData in groups)

                {
                    dynamic drive = await driveData;

                    log.LogInformation($"DRIVE2:{drive}");
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
                        log.LogInformation($"LIST 3:{list}");
                        listId = list.id;
                        siteId = list.parentReference.siteId;

                        var listItems= new List<Task<dynamic>>
                        {
                            GetAllFolderListItemsAsync(groupId, driveId, log)
                        };

                        //var folderList = await GetAllFolderListItemsAsync(groupId, driveId, log);
                        await Task.WhenAll(listItems);
                        
                        foreach (var item in listItems)
                        {
                            log.LogInformation($"1{item}");
                            dynamic ids = await item;

                            foreach( var id in ids.value)
                            {
                                itemId = id.id;
                                log.LogInformation($"ID:---{id.id}");


                                var itemIds = new List<Task<dynamic>>
                                {
                                GetFileDetailsAsync(siteId, listId, itemId, log)
                                };

                                await Task.WhenAll(itemIds);


                                folderListItems = new List<Folders>();


                                foreach(var listItem in itemIds)
                                {
                                    dynamic fileDetails = await listItem;
                                      fileId = fileDetails.id;
                                      fileName = fileDetails.name;
                                      fileSize = fileDetails.size;
                                      createdDate = fileDetails.createdDateTime;
                                      createdBy = fileDetails.createdBy.user.email;
                                      lastModifiedDate = fileDetails.lastModifiedDateTime;
                                      lastModifiedBy = fileDetails.lastModifiedBy.user.email;

                                    //folderListItems.Add(new Folders(fileId, fileName, fileSize, createdDate, createdBy, lastModifiedDate, lastModifiedBy));


                                    GroupItem groupItem = new GroupItem
                                    {
                                        fileId = fileId,
                                        fileName = fileName,
                                        fileSize = fileSize,
                                        createdDate = createdDate,
                                        createdBy = createdBy,
                                        lastModifiedDate = lastModifiedDate,
                                        lastModifiedBy = lastModifiedBy,
                                    };

                                   groupItems.Add(groupItem);

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

            log.LogInformation($"G:{groupItems}");

            string jsonFile = JsonConvert.SerializeObject(GroupList, Formatting.Indented);

            log.LogInformation($"JSON: {jsonFile}");

           

            //Auth auth = new Auth();
            //var graphAPIAuth = auth.graphAuth(log);

            //var unified = "groupTypes/any(c:c eq 'Unified')";

            //List<Group> GroupList = new List<Group>();
            //List<Folders> folderListItems = new List<Folders>();

            //var groups = await graphAPIAuth.Groups
            //    .Request()
            //    .Filter($"{unified}")
            //    .Top(5)
            //    .GetAsync();




            //string groupId;
            //string groupDisplayName;
            //string driveId;
            //string quotaRemaining = "";
            //string quotaTotal = "";
            //string quotaUsed = "";




            //List<Group> GroupList = new List<Group>();
            //List<Folders> folderListItems = new List<Folders>();


            //foreach (var group in groups)
            //{
            //    //log.LogInformation($"G:{groups.NextPageRequest}");
            //        groupId = group.Id;

            //        var drives = await graphAPIAuth.Groups[groupId].Drives.Request().GetAsync();
            //        quotaRemaining = "";
            //        quotaTotal = "";
            //        quotaUsed = "";


            //        foreach (var site in drives)

            //        {
            //            driveId = site.Id;
            //            quotaRemaining = site.Quota.Remaining.ToString();
            //            quotaTotal = site.Quota.Total.ToString();
            //            quotaUsed = site.Quota.Used.ToString();



            //            var drive = await graphAPIAuth.Groups[groupId].Drives[driveId].Root.Children.Request().GetAsync();

            //            folderListItems = new List<Folders>();

            //            foreach (var item in drive)

            //            {

            //                folderListItems.Add(new Folders(item.Id, item.Name, item.Size.ToString(), item.CreatedDateTime.ToString(), item.LastModifiedDateTime.ToString()));

            //            }
            //        }

            //        GroupList.Add(new Group(
            //            groupId,
            //            group.DisplayName,
            //            quotaRemaining,
            //            quotaTotal,
            //            quotaUsed,
            //            folderListItems
            //       ));
            //}


            //string FileTitle = DateTime.Now.ToString("dd-MM-yyyy") + "-" + "siteStorage" + ".json";
            // log.LogInformation($"File {FileTitle}");

            //string jsonFile = JsonConvert.SerializeObject(GroupList, Formatting.Indented);

            // log.LogInformation(jsonFile);


            //CloudStorageAccount storageAccount = GetCloudStorageAccount(context);
            // CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            // CloudBlobContainer container = blobClient.GetContainerReference("groupSiteStorage");

            // CloudBlockBlob blob = container.GetBlockBlobReference(FileTitle);



            return new OkResult();


        }


        private static async Task<dynamic> GetGroupsAsync(ILogger log)
        {
            var unified = "groupTypes/any(c:c eq 'Unified')";
            var requestUri = $"https://graph.microsoft.com/v1.0/groups?$filter={unified}&$select=id,displayName&$top=2";

            return await SendGraphRequestAsync(requestUri, "1", log);
        }

        private static async Task<dynamic> GetDriveDataAsync(string groupId, ILogger log)
        {
            var requestUri = $"https://graph.microsoft.com/v1.0/groups/{groupId}/Drive/?$select=id,quota";
            log.LogInformation($"DriveURL2:{requestUri}");

            return await SendGraphRequestAsync(requestUri, "2", log);
        }

        private static async Task<dynamic> GetFolderListsAsync(string groupId, string driveId, ILogger log)
        {
            var requestUri = $"https://graph.microsoft.com/v1.0/groups/{groupId}/Drives/{driveId}/list?select=id,name,displayName,createdDateTime,createdBy,lastModifiedDateTime,lastModifiedBy,parentReference";
            log.LogInformation($"Folder List 3:{requestUri}");
            return await SendGraphRequestAsync(requestUri, "3", log);
        }

        private static async Task<dynamic> GetAllFolderListItemsAsync(string groupId, string driveId, ILogger log)
        {

            var requestUri = $"https://graph.microsoft.com/v1.0/groups/{groupId}/Drives/{driveId}/list/items?select=id";
            log.LogInformation($"LIST IDS:{requestUri}");
            return await SendGraphRequestAsync(requestUri, "4", log);
        }

        private static async Task<dynamic> GetFileDetailsAsync(string siteId, string listId, string itemId, ILogger log)
        {
            log.LogInformation($"ITEMID: {itemId}");
            var requestUri = $"https://graph.microsoft.com/v1.0/sites/{siteId}/lists/{listId}/items/{itemId}/driveItem";
            log.LogInformation($"ITEMDETAILS 5:{requestUri}");
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
            log.LogInformation($"RB: {responseBody}");
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

        public class GroupItem
        {
            public string fileId { get; internal set; }
            public string fileName { get; internal set; }
            public string fileSize { get; internal set; }
            public string createdDate { get; internal set; }
            public string createdBy { get; internal set; }
            public string lastModifiedBy { get; internal set; }
            public string lastModifiedDate { get; internal set; }
        }


    }
}
