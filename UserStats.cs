using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace appsvc_fnc_dev_userstats
{
    class UserStats
    {
        public async Task<List<usersData>> UserStatsDataAsync (ILogger log)
        {
            IConfiguration config = new ConfigurationBuilder()

           .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
           .AddEnvironmentVariables()
           .Build();
            var exceptionUsersArray = config["exceptionUsersArray"];

            Auth auth = new Auth();
            var graphAPIAuth = auth.graphAuth(log);
            List<usersData> userList = new List<usersData>();

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
                    userList.Add(new usersData()
                    {
                        Id = user.Id,
                        creationDate = user.CreatedDateTime
                    });
                }
            }
            return userList;
        }
    }
}
