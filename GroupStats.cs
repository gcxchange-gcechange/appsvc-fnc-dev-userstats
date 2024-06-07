using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using System.Linq;
using System.Collections.Generic;
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions;
using System.Runtime.CompilerServices;

namespace appsvc_fnc_dev_userstats
{
    class GroupStats
    {

        GraphServiceClient graphAPIAuth;
        List<SingleGroup> GroupList = new List<SingleGroup>();
        string exceptionGroupsArray;

        public async Task<List<SingleGroup>> GroupStatsDataAsync(ILogger log)
        {
            log.LogInformation("GroupStatsDataAsync received a request.");

            IConfiguration config = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: true, reloadOnChange: true).AddEnvironmentVariables().Build();

            exceptionGroupsArray = config["exceptionGroupsArray"];

            Auth auth = new Auth();
            graphAPIAuth = auth.graphAuth(log);


            var groups = await graphAPIAuth.Groups.GetAsync((requestConfiguration) =>
            {
                requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
            });

            await ProcessThisCollectionOfGroups(groups);

            while (groups.OdataNextLink != null)
            {
                var nextPageRequestInformation = new RequestInformation
                {
                    HttpMethod = Method.GET,
                    UrlTemplate = groups.OdataNextLink
                };

                groups = await graphAPIAuth.RequestAdapter.SendAsync(nextPageRequestInformation, (parseNode) => new GroupCollectionResponse());

                await ProcessThisCollectionOfGroups(groups);
            }

            log.LogInformation("GroupStatsDataAsync processed a request.");

            return GroupList;
        }

        private async Task<bool> ProcessThisCollectionOfGroups(GroupCollectionResponse groups)
        {
            foreach (var group in groups.Value)
            {
                if (exceptionGroupsArray.Contains(group.Id) == false)
                {
                    var users = await graphAPIAuth.Groups[group.Id].Members.GetAsync((requestConfiguration) =>
                    {
                        requestConfiguration.QueryParameters.Select = new string[] { "id" };
                    });

                    var total = 0;
                    List<string> userListid = new List<string>();

                    foreach (var user in users.Value)
                    {
                        userListid.Add(user.Id);
                    }
                    total += users.Value.Count();

                    while (users.OdataNextLink != null)
                    {
                        var nextPageRequestInformation = new RequestInformation
                        {
                            HttpMethod = Method.GET,
                            UrlTemplate = users.OdataNextLink
                        };

                        users = await graphAPIAuth.RequestAdapter.SendAsync(nextPageRequestInformation, (parseNode) => new DirectoryObjectCollectionResponse());

                        foreach (var user in users.Value)
                        {
                            userListid.Add(user.Id);
                        }
                        total += users.Value.Count();
                    }

                    GroupList.Add(new SingleGroup(group.DisplayName, total, group.Id, Convert.ToString(group.CreatedDateTime), group.Description, group.GroupTypes, userListid));
                }
            }

            return true;
        }
    }
}