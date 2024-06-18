using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Azure.Identity;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using Azure;
using System;
using System.Linq;
using Azure.Core;
using Azure.Security.KeyVault.Secrets;

namespace appsvc_fnc_dev_userstats
{
    class ActiveUsers
    {
        ILogger log;
        List<activeuserData> ActiveuserList;
        string clientSecret;
        string clientId;
        string tenantid;
        string workspaceId;

        List<usersData> AllUsersList;

        public ActiveUsers(List<usersData> allUsersList, ILogger logger)
        {
            // Initialize variables
            
            IConfiguration config = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: true, reloadOnChange: true).AddEnvironmentVariables().Build();

            clientId = config["clientId"];
            tenantid = config["tenantid"];
            workspaceId = config["workspaceId"];

            AllUsersList = allUsersList;
            log = logger;

            SecretClientOptions options = new SecretClientOptions()
            {
                Retry =
                {
                    Delay= TimeSpan.FromSeconds(2),
                    MaxDelay = TimeSpan.FromSeconds(16),
                    MaxRetries = 5,
                    Mode = RetryMode.Exponential
                 }
            };
            var client = new SecretClient(new Uri(config["keyVaultUrl"]), new DefaultAzureCredential(), options);
            KeyVaultSecret secret = client.GetSecret(config["keyname"]);
            clientSecret = secret.Value;
        }

        public async Task<List<countactiveuserData>> GetActiveUserCount()
        {
            log.LogInformation("GetActiveUserCount received a request.");

            try
            {
                ActiveuserList = await GetActiveUserData();
                log.LogInformation($"GetActiveUserCount - ActiveuserList.Count: {ActiveuserList.Count}");

                List<countactiveuserData> getcountactiveuserData = new List<countactiveuserData>
                {
                    new countactiveuserData()
                    {
                        name = "TotalActiveUser",
                        countActiveusers = ActiveuserList.Count(),
                        countByDomain = GetActiveUsersByDomain()
                    }
                };

                log.LogInformation("GetActiveUserCount processed a request.");

                return getcountactiveuserData;
            }
            catch (Exception ex)
            {
                log.LogError(ex.Message);
                log.LogError(ex.StackTrace);

                log.LogInformation("GetActiveUserCount processed a request.");

                return new List<countactiveuserData>();
            }
        }

        private List<ActiveUserCountByDomain> GetActiveUsersByDomain()
        {
            log.LogInformation("GetActiveUsersByDomain received a request.");

            try
            {
                List<ActiveUserCountByDomain> countByDomain = new List<ActiveUserCountByDomain>();
                string domain;
                string mail;

                Auth auth = new Auth();
                var graphAPIAuth = auth.graphAuth(log);

                log.LogInformation($"GetActiveUsersByDomain - ActiveuserList.Count: {ActiveuserList.Count}");

                foreach (activeuserData user in ActiveuserList)
                {
                    domain = "";
                    mail = "";

                    try
                    {
                        int idx = AllUsersList.FindIndex(item => item.Id == user.userid);

                        if (idx > -1 && AllUsersList[idx].mail != null)
                        {
                            mail = AllUsersList[idx].mail;
                            domain = mail.Substring(mail.IndexOf("@") + 1);
                        }
                    }
                    catch (Exception ex)
                    {
                        log.LogWarning($"Error retrieving user id: {user.userid}");
                        log.LogError($"{ex.Message}");
                    }

                    int index = countByDomain.FindIndex(item => item.domain == domain);

                    if (index > -1)
                    {
                        countByDomain[index].count += 1;
                    }
                    else
                    {
                        countByDomain.Add(new ActiveUserCountByDomain(domain, 1));
                    }
                }

                log.LogInformation("GetActiveUsersByDomain processed a request.");

                return countByDomain;
            }
            catch (Exception ex)
            {
                log.LogError(ex.Message);
                log.LogError(ex.StackTrace);

                log.LogInformation("GetActiveUsersByDomain processed a request.");

                return new List<ActiveUserCountByDomain>();
            }
        }

        private async Task<List<activeuserData>> GetActiveUserData()
        {
            log.LogInformation("GetActiveUserData received a request.");

            try
            {
                ClientSecretCredential cred = new ClientSecretCredential(tenantid, clientId, clientSecret);
                var client = new LogsQueryClient(cred);

                //Connect to LA and get distinc log of users
                Response<LogsQueryResult> response = await client.QueryWorkspaceAsync(
                    workspaceId,
                    "SigninLogs | where TimeGenerated > ago(30d) | where UserPrincipalName != UserId | summarize LastCall = max(TimeGenerated) by UserDisplayName, UserId, UserType | distinct UserId, UserDisplayName, LastCall | where LastCall < ago(1d) | order by LastCall asc",
                    new QueryTimeRange(TimeSpan.FromDays(30))
                );
                List<activeuserData> ActiveuserList = new List<activeuserData>();
                LogsTable table = response.Value.Table;

                foreach (var row in table.Rows)
                {
                    if (ActiveuserList.FindIndex(item => item.userid == row["UserId"].ToString()) == -1)
                    {
                        ActiveuserList.Add(new activeuserData()
                        {
                            UserDisplayName = row["UserDisplayName"].ToString(),
                            userid = row["UserId"].ToString()
                        });
                    }
                }

                log.LogInformation("GetActiveUserData processed a request.");

                return ActiveuserList;
            }
            catch (Exception ex)
            {
                log.LogError(ex.Message);
                log.LogError(ex.StackTrace);

                log.LogInformation("GetActiveUserData processed a request.");

                return new List<activeuserData>();
            }
        }
    }
}