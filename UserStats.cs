using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;

namespace appsvc_fnc_dev_userstats
{
    public static class UserStats
    {
        [FunctionName("UserStats")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            Auth auth = new Auth();
            var graphAPIAuth = auth.graphAuth(log);
            //Kvar result = await getsuserstats(graphAPIAuth, log);

            //string responseMessage = result;

           

            var users = await graphAPIAuth.Users
                .Request()
                .GetAsync();
            log.LogInformation($"{users}");

            return new OkObjectResult(users);
        }
    }

}