using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using System;

namespace appsvc_fnc_dev_userstats
{
    class Auth
    {
        public GraphServiceClient graphAuth(ILogger log)
        {
            IConfiguration config = new ConfigurationBuilder()
           .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
           .AddEnvironmentVariables()
           .Build();

            var scopes = new string[] { "https://graph.microsoft.com/.default" };
            var keyVaultUrl = config["keyVaultUrl"];
            var keyname = config["keyname"];
            var tenantId = config["tenantid"];
            var clientId = config["clientid"];

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
            var client = new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential(), options);

            KeyVaultSecret secret = client.GetSecret(keyname);

            // using Azure.Identity;
            var optionsToken = new TokenCredentialOptions
            {
                AuthorityHost = AzureAuthorityHosts.AzurePublicCloud
            };

            // https://docs.microsoft.com/dotnet/api/azure.identity.clientsecretcredential
            var clientSecretCredential = new ClientSecretCredential(
                tenantId, clientId, secret.Value, optionsToken);

            var graphClient = new GraphServiceClient(clientSecretCredential, scopes);
            return graphClient;
        }

    }
}
