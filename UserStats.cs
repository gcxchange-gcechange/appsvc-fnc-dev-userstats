using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Kiota.Abstractions;

namespace appsvc_fnc_dev_userstats
{
    class UserStats
    {
        public async Task<List<usersData>> UserStatsDataAsync(ILogger log)
        {
            log.LogInformation("UserStatsDataAsync received a request.");

            List<usersData> userList = new List<usersData>();

            try {
                IConfiguration config = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: true, reloadOnChange: true).AddEnvironmentVariables().Build();
                var exceptionUsersArray = config["exceptionUsersArray"];

                Auth auth = new Auth();
                var graphAPIAuth = auth.graphAuth(log);

                // Create a bucket to hold the users
                List<User> users = new List<User>();

                // Get the first page
                var usersPage = await graphAPIAuth.Users.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Select = new string[] { "id, createdDateTime, mail" };
                });

                // Add the first page of results to the user list
                users.AddRange(usersPage.Value);

                // Fetch each page and add those results to the list
                while (usersPage.OdataNextLink != null)
                {
                    var nextPageRequestInformation = new RequestInformation
                    {
                        HttpMethod = Method.GET,
                        UrlTemplate = usersPage.OdataNextLink
                    };

                    usersPage = await graphAPIAuth.RequestAdapter.SendAsync(nextPageRequestInformation, (parseNode) => new UserCollectionResponse());
                    users.AddRange(usersPage.Value);
                }

                foreach (var user in users)
                {
                    if (exceptionUsersArray.Contains(user.Id) == false)
                    {
                        userList.Add(new usersData()
                        {
                            Id = user.Id,
                            creationDate = user.CreatedDateTime,
                            mail = user.Mail
                        });
                    }
                }
           }
            catch (System.Exception e) {
                log.LogError("!! Exception !!");
                log.LogError(e.Message);
                log.LogError("!! StackTrace !!");
                log.LogError(e.StackTrace);
            }

            log.LogInformation("UserStatsDataAsync processed a request.");

            return userList;
        }
    }
}