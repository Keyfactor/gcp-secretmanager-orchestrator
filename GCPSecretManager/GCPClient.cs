using Google.Api.Gax.ResourceNames;
using Google.Cloud.SecretManager.V1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Keyfactor.Extensions.Orchestrator.GCPSecretManager
{
    internal class GCPClient
    {
        public void AddSecret(string alias)
        {
            try
            {
                SecretManagerServiceClient client = SecretManagerServiceClient.Create();
                AccessSecretVersionRequest request = new AccessSecretVersionRequest();
                SecretName secretName = new SecretName(ProjectId, alias);

                //create secret
                CreateSecretRequest secretRequest = new CreateSecretRequest();
                secretRequest.ParentAsProjectName = new ProjectName(ProjectId);
                secretRequest.SecretId = alias;
                secretRequest.Secret = new Secret { Replication = new Replication { Automatic = new Replication.Types.Automatic() } };

                Secret secret = client.CreateSecret(secretRequest);

                //create new version
                AddSecretVersionRequest secretVersionRequest = new AddSecretVersionRequest();
                secretVersionRequest.ParentAsSecretName = secretName;
                secretVersionRequest.Payload = new SecretPayload { Data = Google.Protobuf.ByteString.CopyFromUtf8(GetPEMCert()) };

                SecretVersion secretVersion = client.AddSecretVersion(secretVersionRequest);
            }
            catch (Exception ex)
            {
                string i = ex.Message;
            }
        }

        public void GetSecret(string alias)
        {
            SecretManagerServiceClient client = SecretManagerServiceClient.Create();

            ListSecretVersionsRequest request = new ListSecretVersionsRequest();
            SecretName secretName = new SecretName(ProjectId, alias);
            request.ParentAsSecretName = secretName;

            var response = client.ListSecretVersions(request);
            foreach (var item in response)
            {

            }
            AccessSecretVersionRequest request2 = new AccessSecretVersionRequest();
            SecretVersionName secretVersionName = new SecretVersionName(ProjectId, alias, "latest");
            request2.SecretVersionName = secretVersionName;
            AccessSecretVersionResponse version = client.AccessSecretVersion(request2);

        }

        public void GetSecrets()
        {
            SecretManagerServiceClient client = SecretManagerServiceClient.Create();

            ListSecretsRequest request = new ListSecretsRequest();
            request.PageSize = 1;
            request.ParentAsProjectName = ProjectName.FromProject(ProjectId);

            ListSecretsResponse? response;

            do
            {
                response = client.ListSecrets(request).AsRawResponses().FirstOrDefault();
                if (response == null)
                    break;
                else
                {
                    foreach (var item in response.Secrets)
                    {
                        string x = item.Name;
                    }
                }

                request.PageToken = response.NextPageToken;
            } while (!string.IsNullOrEmpty(response.NextPageToken));

        }
    }
}
