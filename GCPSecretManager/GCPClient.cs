using Google.Api.Gax.ResourceNames;
using Google.Cloud.SecretManager.V1;
using Google.Cloud.ResourceManager.V3;

using Microsoft.Extensions.Logging;

using Keyfactor.Logging;

using System;
using System.Collections.Generic;
using System.Linq;
using Google.Api.Gax;

namespace Keyfactor.Extensions.Orchestrator.GCPSecretManager
{
    internal class GCPClient
    {
        ILogger _logger;
        string ProjectId { get; set; }
        SecretManagerServiceClient Client { get; set; }
        TagKeysClient TagKeysClient { get; set; }
        TagBindingsClient TagBindingsClient { get; set; }
        TagValuesClient TagValuesClient { get; set; }
        ProjectsClient ProjectsClient { get; set; }

        public GCPClient(string projectId)
        {
            _logger = LogHandler.GetClassLogger(this.GetType());
            ProjectId = projectId;
            Client = SecretManagerServiceClient.Create();
            TagKeysClient = TagKeysClient.Create();
            TagBindingsClient = TagBindingsClient.Create();
            TagValuesClient = TagValuesClient.Create();
        }

        public List<string> GetSecretNames()
        {
            _logger.MethodEntry(LogLevel.Debug);

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

        public void AddSecret(string alias, string secretContent, bool entryExists)
        {
            _logger.MethodEntry(LogLevel.Debug);

            try
            {
                SecretName secretName = new SecretName(ProjectId, alias);

                if (!entryExists)
                {
                    AccessSecretVersionRequest request = new AccessSecretVersionRequest();

                    //create secret
                    CreateSecretRequest secretRequest = new CreateSecretRequest();
                    secretRequest.ParentAsProjectName = new ProjectName(ProjectId);
                    secretRequest.SecretId = alias;
                    secretRequest.Secret = new Secret { Replication = new Replication { Automatic = new Replication.Types.Automatic() } };

                    Secret secret = Client.CreateSecret(secretRequest);
                }

                //create new version
                AddSecretVersionRequest secretVersionRequest = new AddSecretVersionRequest();
                secretVersionRequest.ParentAsSecretName = secretName;
                secretVersionRequest.Payload = new SecretPayload { Data = Google.Protobuf.ByteString.CopyFromUtf8(secretContent) };

                SecretVersion secretVersion = Client.AddSecretVersion(secretVersionRequest);
            }
            catch (Exception ex)
            {
                _logger.LogError(GCPException.FlattenExceptionMessages(ex, "Error adding/replacing certificate.  "));
                throw;
            }
            finally
            {
                _logger.MethodExit(LogLevel.Debug);
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

        public List<TagKeyValue> GetTagKeyValues(TagScope tagScope, string parentId)
        {
            _logger.MethodEntry(LogLevel.Debug);

            List<TagKeyValue> tagKeyValues = new List<TagKeyValue>();
            
            ListTagKeysRequest request = new ListTagKeysRequest()
            {
                ParentAsResourceName = tagScope == TagScope.Organization ? OrganizationName.FromOrganization(parentId) : ProjectName.FromProject(parentId),
                PageSize = 50
            };

            ListTagKeysResponse response;

            try
            {
                do
                {
                    response = TagKeysClient.ListTagKeys(request).AsRawResponses().FirstOrDefault();
                    if (response == null)
                        break;
                    else
                    {
                        foreach (var item in response.TagKeys)
                        {
                            TagKeyValue tagKeyValueItem = new TagKeyValue();
                            tagKeyValueItem.TagScope = tagScope;
                            tagKeyValueItem.TagKey = item;
                            tagKeyValueItem.TagValues = GetTagValues(item.Name);
                        }
                    }

                    request.PageToken = response.NextPageToken;
                } while (!string.IsNullOrEmpty(response.NextPageToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(GCPException.FlattenExceptionMessages(ex, "Error retrieving Tag Key/Value Pairs: "));
                throw;
            }
            finally
            {
                _logger.MethodExit(LogLevel.Debug);
            }

            return tagValuePairs;
        }

        public List<TagValue> GetTagKeyValues(string keyName)
        {
            List<TagValue> tagValues = new List<TagValue>();
            
            try
            {
                tagValues = TagValuesClient.ListTagValues(new ListTagValuesRequest()
                {
                    ParentAsResourceName = new UnparsedResourceName(keyName)
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(GCPException.FlattenExceptionMessages(ex, $"Error Tag Values for {keyName}."));
                throw;
            }
            finally
            {
                _logger.MethodExit(LogLevel.Debug);
            }

            return tagValues;
        }

        public Dictionary<string, object> GetSecretTags(string secretResource)
        {
            _logger.MethodEntry(LogLevel.Debug);
            Dictionary<string, object> rtnValue = new Dictionary<string, object>();
            List<string> tagPairs = new List<string>();

            ListTagBindingsRequest request = new ListTagBindingsRequest()
            {
                ParentAsResourceName = new UnparsedResourceName($"//secretmanager.googleapis.com/{secretResource}")
            };
            
            try
            {
                ListTagBindingsResponse response = TagBindingsClient.ListTagBindings(request).AsRawResponses().FirstOrDefault();
                foreach(TagBinding x in response.TagBindings)
                {
                    TagValue y = TagValuesClient.GetTagValue(x.TagValue);
                    tagPairs.Add(x.Name + ":" + y.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                throw;
            }
            finally
            {
                _logger.MethodExit(LogLevel.Debug);
            }

            if (tagPairs.Count > 0)
                rtnValue.Add("tags", string.Join(",", tagPairs));

            return rtnValue;
        }

        public List<TagValue> GetTagValues(string tagName)
        {
            _logger.MethodEntry(LogLevel.Debug);

            List<string> rtnValues = new List<string>();

            ListTagValuesRequest request = new ListTagValuesRequest()
            {
                ParentAsResourceName = TagKeyName.FromTagKey(tagName),
                PageSize = 20
            };

            ListTagValuesResponse response;

            try
            {
                do
                {
                    response = TagValuesClient.ListTagValues(request).AsRawResponses().FirstOrDefault();
                    if (response == null)
                        break;
                    else
                    {
                        foreach (var item in response.TagValues)
                        {
                            rtnValues.Add(item.Name);
                        }
                    }

                    request.PageToken = response.NextPageToken;
                } while (!string.IsNullOrEmpty(response.NextPageToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(GCPException.FlattenExceptionMessages(ex, "Error retrieving Tag Values: "));
                throw;
            }
            finally
            {
                _logger.MethodExit(LogLevel.Debug);
            }

            return rtnValues;
        }

        private string GetOrganizationFromProject()
        {
            _logger.MethodEntry(LogLevel.Debug);

            string organization = string.Empty;

            try
            {
                Project project = ProjectsClient.GetProject(new GetProjectRequest() { ProjectName = ProjectName.FromProject(ProjectId) });
                organization = project.Parent;
            }
            catch (Exception ex)
            {
                _logger.LogError(GCPException.FlattenExceptionMessages(ex, "Error retrieving Organization: "));
                throw;
            }
            finally
            {
                _logger.MethodExit(LogLevel.Debug);
            }

            return organization;
        }
    }
}
