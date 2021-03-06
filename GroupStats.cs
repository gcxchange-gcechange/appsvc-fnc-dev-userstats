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
                        do
                        {
                            total += users.Count();
                        }
                        while (users.NextPageRequest != null && (users = await users.NextPageRequest.GetAsync()).Count > 0);
                        GroupList.Add(new SingleGroup(group.DisplayName, total, group.Id, Convert.ToString(group.CreatedDateTime), group.Description, group.GroupTypes));

                    }
                }
            }
            while (groups.NextPageRequest != null && (groups = await groups.NextPageRequest.GetAsync()).Count > 0);

            return GroupList;
        }
    }
    public class SingleGroup
    {
        public string displayName;
        public int countMember;
        public string groupId;
        public string creationDate;
        public string description;
        public IEnumerable<string> groupType;

        public SingleGroup(string displayName, int countMember, string groupId, string creationDate, string description, IEnumerable<string> groupType)
        {
            this.displayName = displayName;
            this.countMember = countMember;
            this.groupId = groupId;
            this.creationDate = creationDate;
            this.description = description;
            this.groupType = groupType;
        }
    }
}

