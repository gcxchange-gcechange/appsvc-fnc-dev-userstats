using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace appsvc_fnc_dev_userstats
{
    public static class Batch 
    {
        [FunctionName("BatchSiteStorage")] 
        
            public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.System, "get", "post", Route = null)] HttpRequest req, ILogger log, ExecutionContext context)
            {

            IConfiguration config = new ConfigurationBuilder()

           .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
           .AddEnvironmentVariables()
           .Build();


            Auth auth = new Auth();
            var graphAPIAuth = auth.graphAuth(log);

            var groupId = ""; 

            var groups = new List<string>();

            try
            {
                //Create a batch request

                var batch  = new BatchRequestContent();

                //Request for the batch
                var unified = "groupTypes/any(c:c eq 'Unified')";
                var unifiedGroups = graphAPIAuth.Groups.Request().Filter(unified).Select("id, DisplayName").Top(20);
                //var groupDrives = graphAPIAuth.Groups[groupId].Drives.Request();

                //Add the request to the Batch
                var groupsRequestId = batch.AddBatchRequestStep(unifiedGroups);
           
                //var drivesRequestId = batch.AddBatchRequestStep(groupDrives);
                

                //var allgroupsRequest = batch.AddBatchRequestStep(allgroups);

                //Execute the batch

                var returnedResponse = await graphAPIAuth.Batch.Request().PostAsync(batch); 

                // Process batch responses

                var response = await returnedResponse.GetResponseByIdAsync(groupsRequestId);
                var responseContent = response.Content.ReadAsStringAsync().Result;
                dynamic responseBody = JsonConvert.DeserializeObject(responseContent);
                //var responseBody = await new StreamReader(response[allgroupsID].Content.ReadAsStreamAsync().Result).ReadToEndAsync();

                // Now process the dependencies

                log.LogInformation($"Res:{responseBody.value }");

                foreach ( var item in responseBody.value ) 
                { 
                    //convert object to string
                    string jsonGroupIds = JsonConvert.SerializeObject(item.id);
                    groups.Add(jsonGroupIds);
                   
                }

                string nextPageLink = responseBody["@odata.nextLink"];

                 foreach(var id in groups)
                    {
                        log.LogInformation($"LIST IDS:{id}");
                    }


                log.LogInformation($"NEXTPGAE:{nextPageLink}");
                log.LogInformation($"LIST:{groups.Count}");


                while (nextPageLink != null)
                {

                    var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, nextPageLink);
                    batch.AddBatchRequestStep(httpRequestMessage);
                    returnedResponse = await graphAPIAuth.Batch.Request().PostAsync(batch);
                    var res = await returnedResponse.GetResponsesAsync();
                   

                    


                    log.LogInformation($"{res} returned response");

                    //var nextLinkResponse = await graphAPIAuth.HttpProvider.GetAsync(new Uri(nextPageLink)); 

                    //dynamic nextLinkResponseBody = JsonConvert.DeserializeObject(nextLinkResponseContent);

                    // Process the next page response

                    // Check if there are more pages in the next page response
                    //nextPageLink = nextLinkResponseBody["@odata.nextLink"];

                }

            }
            catch( Exception ex)
            {
                log.LogInformation($"ERROR: {ex}"); 
                return null;
            }


            return new OkResult();
            }


    }
}
