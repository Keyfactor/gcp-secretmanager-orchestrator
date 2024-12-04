using Google.Api.Gax.ResourceNames;
using Google.Cloud.SecretManager.V1;

using Microsoft.Extensions.Logging;

using Keyfactor.Logging;

using System;
using System.Collections.Generic;
using System.Linq;

namespace Keyfactor.Extensions.Orchestrator.GCPSecretManager
{
    internal class GCPClient
    {
        ILogger _logger;
        string ProjectId { get; set; }
        SecretManagerServiceClient Client { get; set; }

        public GCPClient(string projectId)
        {
            _logger = LogHandler.GetClassLogger(this.GetType());
            ProjectId = projectId;
            Client = SecretManagerServiceClient.Create();
        }

        public List<string> GetSecretNames()
        {
            _logger.MethodEntry(LogLevel.Debug);
            Client
            List<string> rtnSecrets = new List<string>();

            ListSecretsRequest request = new ListSecretsRequest();
            request.PageSize = 50;
            request.ParentAsProjectName = ProjectName.FromProject(ProjectId);

            ListSecretsResponse response;

            try
            {
                do
                {
                    response = Client.ListSecrets(request).AsRawResponses().FirstOrDefault();
                    if (response == null)
                        break;
                    else
                    {
                        foreach (var item in response.Secrets)
                        {
                            rtnSecrets.Add(item.Name);
                        }
                    }

                    request.PageToken = response.NextPageToken;
                } while (!string.IsNullOrEmpty(response.NextPageToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(GCPException.FlattenExceptionMessages(ex, "Error retrieving certificates: "));
                throw;
            }
            finally
            {
                _logger.MethodExit(LogLevel.Debug);
            }

            return rtnSecrets;
        }

        public string GetCertificateEntry(string name)
        {
            _logger.MethodEntry(LogLevel.Debug);

            string rtnValue;
            
            try
            {
                AccessSecretVersionResponse version = Client.AccessSecretVersion(new AccessSecretVersionRequest() { Name = name + "/versions/latest" });
                rtnValue = version.Payload.Data.ToStringUtf8();
            }
            catch (Exception ex)
            {
                _logger.LogError(GCPException.FlattenExceptionMessages(ex, $"Error retrieving certificate {name}: "));
                throw;
            }
            finally
            {
                _logger.MethodExit(LogLevel.Debug);
            }

            return rtnValue;
        }

        public void AddSecret(string alias)
        {
            _logger.MethodEntry(LogLevel.Debug);

            try
            {
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

        public void DeleteCertificate(string name)
        {
            _logger.MethodEntry(LogLevel.Debug);

            DeleteSecretRequest request = new DeleteSecretRequest()
            {
                SecretName = new SecretName(ProjectId, name)
            };

            try
            {
                Client.DeleteSecret(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(GCPException.FlattenExceptionMessages(ex, $"Error deleting certificate {name}: "));
                throw;
            }
            finally
            {
                _logger.MethodExit(LogLevel.Debug);
            }
        }

        public bool Exists(string name)
        {
            _logger.MethodEntry(LogLevel.Debug);

            bool rtnValue = true;

            GetSecretRequest request = new GetSecretRequest()
            {
                SecretName = new SecretName(ProjectId, name)
            };

            try
            {
                Client.GetSecret(request);
            }
            catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
            {
                rtnValue = false;
            }
            finally
            {
                _logger.MethodExit(LogLevel.Debug);
            }

            return rtnValue;
        }
    }
}
