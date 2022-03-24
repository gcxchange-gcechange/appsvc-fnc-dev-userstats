using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Azure.Identity;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using Azure;
using System;
using System.Linq;

namespace appsvc_fnc_dev_userstats
{
    class ActiveUsers
    {
        public async Task<List<countactiveuserData>> ActiveUsersDataAsync (ILogger log)
        {
            IConfiguration config = new ConfigurationBuilder()

           .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
           .AddEnvironmentVariables()
           .Build();
            var exceptionUsersArray = config["exceptionActiveUsersArray"];
            string clientSecret = config["clientSecret"];
            string clientId = config["clientId"];
            string tenantid = config["tenantid"];
            string workspaceId = config["workspaceId"];
           

            var getActiveUserCount = await Activeusercount(tenantid, clientId, clientSecret, workspaceId, log);

            return getActiveUserCount;
        }

        public static async Task<List<countactiveuserData>> Activeusercount(string tenantid, string clientId, string clientSecret, string workspaceId, ILogger log)
        {
            ClientSecretCredential cred = new ClientSecretCredential(tenantid, clientId, clientSecret);
            var client = new LogsQueryClient(cred);
            try
            {
                Response<LogsQueryResult> response = await client.QueryWorkspaceAsync(
                    workspaceId,
                                "SigninLogs | where TimeGenerated > ago(30d) | where UserPrincipalName != UserId | summarize LastCall = max(TimeGenerated) by UserDisplayName, UserId, UserType | distinct UserId, UserDisplayName, LastCall | where LastCall < ago(1d) | order by LastCall asc",
                    new QueryTimeRange(TimeSpan.FromDays(30)));
                List<activeuserData> ActiveuserList = new List<activeuserData>();
                LogsTable table = response.Value.Table;

                foreach (var row in table.Rows)
                {
                    log.LogInformation(row["UserDisplayName"] + " " + row["UserId"]);

                    ActiveuserList.Add(new activeuserData()
                        {
                            UserDisplayName = row["UserDisplayName"].ToString(),
                            userid = row["UserId"].ToString()
                    });
                    
                }

                var uniqueItems = ActiveuserList.GroupBy(i => i.userid).Select(g => g.FirstOrDefault());

                List<countactiveuserData> getcountactiveuserData = new List<countactiveuserData>();

                getcountactiveuserData.Add(new countactiveuserData()
                {
                    name = "TotalActiveUser",
                    countActiveusers = uniqueItems.Count()
                });

                log.LogInformation($"{uniqueItems.Count()}");
                return getcountactiveuserData;
            }
            catch (Exception ex)
            {
                log.LogInformation(ex.Message);
                return new List<countactiveuserData>();

            }
        }
    }
}
