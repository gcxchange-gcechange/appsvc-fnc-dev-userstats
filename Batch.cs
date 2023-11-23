using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Newtonsoft.Json;
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

            try
            {
                //Create a batch request

                var batch  = new BatchRequestContent();

                //Request for the batch
                var unified = "groupTypes/any(c:c eq 'Unified')";
                var unifiedGroups = graphAPIAuth.Groups.Request().Filter(unified).Select("id, DisplayName").Top(20);
                var groupDrives = graphAPIAuth.Groups[groupId].Drives.Request();

                //Add the request to the Batch
                var groupsRequestId = batch.AddBatchRequestStep(unifiedGroups);
                var drivesRequestId = batch.AddBatchRequestStep(groupDrives);
                

                //var allgroupsRequest = batch.AddBatchRequestStep(allgroups);

                //Execute the batch

                var returnedResponse = await graphAPIAuth.Batch.Request().PostAsync(batch); 

                // Process batch responses

                var response = await returnedResponse.GetResponseByIdAsync(groupsRequestId);
                var responseContent = response.Content.ReadAsStringAsync().Result;
                dynamic responseBody = JsonConvert.DeserializeObject(responseContent);
                //var responseBody = await new StreamReader(response[allgroupsID].Content.ReadAsStreamAsync().Result).ReadToEndAsync();

                // Now process the dependencies
                var nextPageLink = responseBody["@odata.nextLink"];
              
                List<string> groupIds = new List<string>(); 

                while (nextPageLink != null )
                {
                    unifiedGroups = nextPageLink;
                    var nextpage = batch.AddBatchRequestStep(unifiedGroups);
                    returnedResponse = await graphAPIAuth.Batch.Request().PostAsync(batch);

                    if(returnedResponse != null )
                    {
                        response = await returnedResponse.GetResponseByIdAsync(nextpage);
                        responseContent = response.Content.ReadAsStringAsync().Result;
                        responseBody = JsonConvert.DeserializeObject(responseContent);

                        

                    }
                }

                foreach (var ids in responseBody.value)
                {
                    groupId = ids.id;
                    log.LogInformation($"{groupId}");
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
