using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace appsvc_fnc_dev_userstats
{
    public static class UserStats
    {
        [FunctionName("UserStats")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            IConfiguration config = new ConfigurationBuilder()

           .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
           .AddEnvironmentVariables()
           .Build();
            var exceptionUsersArray = config["exceptionUsersArray"];

            Auth auth = new Auth();
            var graphAPIAuth = auth.graphAuth(log);
            List<SingleUser> userList = new List<SingleUser>();

            // Create a bucket to hold the users
            List<User> users = new List<User>();

            // Get the first page
            IGraphServiceUsersCollectionPage usersPage = await graphAPIAuth
                .Users
                .Request()
                .Select("id,createdDateTime")
                .GetAsync();

            // Add the first page of results to the user list
            users.AddRange(usersPage.CurrentPage);

            // Fetch each page and add those results to the list
            while (usersPage.NextPageRequest != null)
            {
                usersPage = await usersPage.NextPageRequest.GetAsync();
                users.AddRange(usersPage.CurrentPage);
            }

            foreach (var user in users)
                    {
                        if (exceptionUsersArray.Contains(user.Id) == false)
                        {
                    log.LogInformation(user.Id);
                            userList.Add(new SingleUser(user.Id, user.CreatedDateTime));
                    
                        }
                    }
                return new OkObjectResult(userList);
            }
        }
    }
    public class SingleUser
    {
        public string userId;
        public System.DateTimeOffset? createDateTime;


        public SingleUser(string userId, System.DateTimeOffset? createDateTime)
        {
            this.userId = userId;
            this.createDateTime = createDateTime;
        }
    }

