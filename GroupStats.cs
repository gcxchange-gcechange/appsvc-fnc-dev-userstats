using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using System.Collections.Generic;
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions;
using System.Net.Http;

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

            await ProcessThisCollectionOfGroups(groups, log);

            while (groups.OdataNextLink != null)
            {
                var nextPageRequestInformation = new RequestInformation
                {
                    HttpMethod = Method.GET,
                    UrlTemplate = groups.OdataNextLink
                };

                groups = await graphAPIAuth.RequestAdapter.SendAsync(nextPageRequestInformation, (parseNode) => new GroupCollectionResponse());

                await ProcessThisCollectionOfGroups(groups, log);
            }

            log.LogInformation($"GroupList: {GroupList.Count}");
            log.LogInformation("GroupStatsDataAsync processed a request.");

            return GroupList;
        }

        private async Task<bool> ProcessThisCollectionOfGroups(GroupCollectionResponse groups, ILogger log)
        {
            BatchRequestContentCollection requests = new BatchRequestContentCollection(graphAPIAuth);

            foreach (var group in groups.Value)
            {
                if (exceptionGroupsArray.Contains(group.Id) == false)
                {
                    requests.AddBatchRequestStep(new BatchRequestStep(group.Id, new HttpRequestMessage(HttpMethod.Get, $"https://graph.microsoft.com/v1.0/groups/{group.Id}/members?$select=id")));
                    GroupList.Add(new SingleGroup(group.DisplayName, group.Id, Convert.ToString(group.CreatedDateTime), group.Description, group.GroupTypes, null));
                }
            }

            await GetGroupMembers(requests, log);

            return true;
        }

        private async Task<bool> GetGroupMembers(BatchRequestContentCollection requests, ILogger log)
        {
            string requestId;
            List<string> userIds;

            var responses = await graphAPIAuth.Batch.PostAsync(requests);

            foreach (var request in requests.BatchRequestSteps)
            {
                requestId = request.Value.RequestId;
                userIds = new List<string>();

                var users = await responses.GetResponseByIdAsync<UserCollectionResponse>(requestId);

                foreach (var user in users.Value)
                {
                    userIds.Add(user.Id);
                }

                while (users.OdataNextLink != null)
                {
                    var nextPageRequestInformation = new RequestInformation
                    {
                        HttpMethod = Method.GET,
                        UrlTemplate = users.OdataNextLink
                    };

                    users = await graphAPIAuth.RequestAdapter.SendAsync(nextPageRequestInformation, (parseNode) => new UserCollectionResponse());

                    foreach (var user in users.Value)
                    {
                        userIds.Add(user.Id);
                    }
                }

                GroupList.Find(y => y.groupId == requestId).userlist = userIds;
            }

            return true;
        }
    }
}