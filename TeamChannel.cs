
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
using System.Threading.Channels;
using static appsvc_fnc_dev_userstats.TeamChannels;
using Microsoft.Identity.Client;

namespace appsvc_fnc_dev_userstats
{
    class TeamChannels
    {
        [FunctionName("TeamChannels")]

        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", "get")] HttpRequest req, ILogger log, ExecutionContext context)

        {
            IConfiguration config = new ConfigurationBuilder()


            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();


            List<Group> GroupList = new List<Group>();
           

            string groupId;
            string groupDisplayName;
            string channelId;
            string channelCount = "";
            string messageCount = "";
            
  


            var groupData = await GetGroupsAsync(log);

            foreach (var group in groupData.value)
            {
                groupId = group.id;
                groupDisplayName = group.displayName;

                var groups = new List<Task<dynamic>>
                {
                   GetTeamChannelsDataAsync(groupId, log)
                };

                await Task.WhenAll(groups);

                log.LogInformation($"Groups{groups}");

                //List<dynamic> ChannelList = new List<dynamic>();

                messageCount = "";

                foreach ( var channels in groups)
                {

                    dynamic channel = await channels;
                    
                    try
                    {
                       
                        //ChannelList.Add(new { channel.value }); 

                        channelCount = channel["@odata.count"];

                        //log.LogInformation($"CHANN-VALUE{channel.value}");


                        if (channel.value != null)
                        {
                            foreach (var channelObj in channel.value)
                            {
                                //log.LogInformation($"ID:{channelObj.id}");

                                string channelObjId = channelObj.id;

                                var channelMessages = new List<Task<dynamic>>
                                {
                                    GetChannelMessagesAsync(groupId, channelObjId, log)
                                };

                                await Task.WhenAll(channelMessages);

                              

                                foreach (var message in channelMessages)
                                {
                                    dynamic messages = await message;
                                    messageCount = messages["@odata.count"];
                                    log.LogInformation($"MESSAGES:{messages}");
                                }

                            }

                        }


                    


                    }
                    catch (Exception e)
                    {
                        log.LogInformation($"Error{e}");
                        channelCount = null;
                    }
                 

                }

                

                //List<dynamic> ChannelList = new List<dynamic>();

                //foreach (var channelData in groups)

                //{

                //    dynamic channel = await channelData;
                //    log.LogInformation($"{channel.error}");

                //    if (channel.value != null)
                //    {
                //        foreach (var channelData2 in channel.value)
                //        {

                //            ChannelList.Add(new{ channelData2.id});
                //            channelCount = channel["@odata.count"];

                //            foreach (var list in ChannelList)
                //            {
                                
                //                log.LogInformation($"LIST:{list.id}");

                //                channelId = list.id;

                //                var channelItems = new List<Task<dynamic>> {
                //                     GetChannelItemsAsync(groupId, channelId, log)
                //                };

                //                await Task.WhenAll(channelItems);

                //                foreach (var messages in channelItems)
                //                {
                //                    dynamic message = await messages;

                //                    log.LogInformation($"MESSGAE: {message}");

                //                    if (message.value != null)
                //                    {
                //                        messageCount = message["@odata.count"];
                //                        log.LogInformation($"MESSAGE_Count:{message.value}");

                //                    }


                //                }
                //            }
                //        }
                       
                //    } 

                //}

                GroupList.Add(new Group(
                    groupId,
                    groupDisplayName,
                    channelCount,
                    messageCount

               ));
            }


            //CreateContainerIfNotExists(context, "groupsitestorage", log);

            //CloudStorageAccount storageAccount = GetCloudStorageAccount(context);
            //CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            //CloudBlobContainer container = blobClient.GetContainerReference("groupsitestorage");

            //string FileTitle = DateTime.Now.ToString("dd-MM-yyyy") + "-" + "groupsitestorage" + ".json";
            //log.LogInformation($"File {FileTitle}");

            //CloudBlockBlob blob = container.GetBlockBlobReference(FileTitle);

            string jsonFile = JsonConvert.SerializeObject(GroupList, Formatting.Indented);


            //log.LogInformation($"JSON: {jsonFile}");

            //blob.Properties.ContentType = "application/json";

            //using (MemoryStream ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(jsonFile)))
            //{
            //    await blob.UploadFromStreamAsync(ms);
            //}

            //await blob.SetPropertiesAsync();

            return new OkObjectResult(jsonFile);


        }

        private static async Task<dynamic> GetGroupsAsync(ILogger log)
        {
            var unified = "groupTypes/any(c:c eq 'Unified')";
   
            var requestUri = $"https://graph.microsoft.com/v1.0/groups?$filter={unified}&$select=id,displayName";

            return await SendGraphRequestAsync(requestUri, "1", log);
        }

        private static async Task<dynamic> GetTeamChannelsDataAsync(string groupId, ILogger log)
        {
            var requestUri = $"https://graph.microsoft.com/v1.0/teams/{groupId}/channels?$select=id";
            log.LogInformation($"DriveURL2:{requestUri}");

            return await SendGraphRequestAsync(requestUri, "2", log);
        }

        private static async Task<dynamic> GetChannelMessagesAsync(string groupId, string channelId, ILogger log)
        {
            var requestUri = $"https://graph.microsoft.com/v1.0/teams/{groupId}/channels/{channelId}/messages";
            log.LogInformation($"Folder List 3:{requestUri}");
            return await SendGraphRequestAsync(requestUri, "3", log);
        }

        //private static async Task<dynamic> GetAllFolderListItemsAsync(string groupId, string driveId, ILogger log)
        //{

        //    var requestUri = $"https://graph.microsoft.com/v1.0/groups/{groupId}/Drives/{driveId}/list/items?select=id";
        //    // log.LogInformation($"LIST IDS:{requestUri}");
        //    return await SendGraphRequestAsync(requestUri, "4", log);
        //}

        //private static async Task<dynamic> GetFileDetailsAsync(string siteId, string listId, string itemId, ILogger log)
        //{
        //    var requestUri = $"https://graph.microsoft.com/v1.0/sites/{siteId}/lists/{listId}/items/{itemId}/driveItem?select=createdDateTime,id,lastModifiedDateTime,name,webUrl,size,createdBy,lastModifiedBy";
        //    //log.LogInformation($"ITEMDETAILS 5:{requestUri}");
        //    return await SendGraphRequestAsync(requestUri, "5", log);
        //}

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

        //private static async void CreateContainerIfNotExists(ExecutionContext executionContext, string containerName, ILogger log)
        //{
        //    CloudStorageAccount storageAccount = GetCloudStorageAccount(executionContext);
        //    CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
        //    string[] containers = new string[] { containerName };
        //    log.LogInformation("Create container");
        //    foreach (var item in containers)
        //    {
        //        log.LogInformation($"ITEM:{item}");
        //        CloudBlobContainer blobContainer = blobClient.GetContainerReference(item);
        //        await blobContainer.CreateIfNotExistsAsync();
        //    }
        //}

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
            //public string channelId;
            public string channelCount;
            public string messageCount;



            public Group(string groupId, string displayName,  string channelCount, string messageCount )
            {
                this.groupId = groupId;
                this.displayName = displayName;
                //this.channelId = channelId;
                this.channelCount = channelCount;
                this.messageCount = messageCount;


            }
        }

        public class ChannelIds
        {
            public string channeId;

            public ChannelIds(string channeId)
            {
                this.channeId = channeId;
            }
        }

      


    }
}
