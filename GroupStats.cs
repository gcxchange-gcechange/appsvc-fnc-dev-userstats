using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using System.Linq;
using System.Collections.Generic;
using System;
using Microsoft.Extensions.Configuration;

namespace appsvc_fnc_dev_userstats
{
    class GroupStats
    {
        [FunctionName("GroupStats")]
        public async Task<List<SingleGroup>> GroupStatsDataAsync(ILogger log)
        {
            log.LogInformation("GroupStatsDataAsync received a request.");

            IConfiguration config = new ConfigurationBuilder()

          .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
          .AddEnvironmentVariables()
          .Build();

            var exceptionGroupsArray = config["exceptionGroupsArray"];

            Auth auth = new Auth();
            var graphAPIAuth = auth.graphAuth(log);

            var groups = await graphAPIAuth.Groups
                .Request()
                .Header("ConsistencyLevel", "eventual")
                .GetAsync();

            List<SingleGroup> GroupList = new List<SingleGroup>();

            do
            {
                foreach (var group in groups)
                {
                    if(exceptionGroupsArray.Contains(group.Id) == false)
                    {
                        var users = await graphAPIAuth.Groups[group.Id].Members.Request().GetAsync();
                        var total = 0;
                        List<string> userListid = new List<string>();

                        foreach (var user in users)
                        {
                            userListid.Add(user.Id);
                        }

                        do
                        {
                            total += users.Count();
                           
                        }
                        while (users.NextPageRequest != null && (users = await users.NextPageRequest.GetAsync()).Count > 0);
                        GroupList.Add(new SingleGroup(group.DisplayName, total, group.Id, Convert.ToString(group.CreatedDateTime), group.Description, group.GroupTypes, userListid));
                    }
                }
            }
            while (groups.NextPageRequest != null && (groups = await groups.NextPageRequest.GetAsync()).Count > 0);

            log.LogInformation("GroupStatsDataAsync processed a request.");

            return GroupList;
        }
    }
}