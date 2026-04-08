using Google.Api.Gax;
using Google.Api.Gax.ResourceNames;
using Google.Cloud.ResourceManager.V3;
using Google.Cloud.SecretManager.V1;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Keyfactor.Logging;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;

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

        private const string ResourcePrefix = "//secretmanager.googleapis.com/";

        public GCPClient(string projectId)
        {
            _logger = LogHandler.GetClassLogger(this.GetType());
            ProjectId = projectId;
            Client = SecretManagerServiceClient.Create();
            TagKeysClient = TagKeysClient.Create();
            TagBindingsClient = TagBindingsClient.Create();
            TagValuesClient = TagValuesClient.Create();
            ProjectsClient = ProjectsClient.Create();
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

        public SecretWithLabels GetCertificateEntry(string name)
        {
            _logger.MethodEntry(LogLevel.Debug);

            SecretWithLabels rtnValue = new SecretWithLabels();
            
            try
            {
                AccessSecretVersionResponse version = Client.AccessSecretVersion(new AccessSecretVersionRequest() { Name = name + "/versions/latest" });
                rtnValue.Secret = version.Payload.Data.ToStringUtf8();
                rtnValue.Labels = string.Empty;

                Secret secret = GetSecret(name);
                List<string> labelsString = new List<string>();
                foreach(var label in secret.Labels)
                {
                    labelsString.Add($"{label.Key}:{label.Value}");
                }
                if (labelsString.Count > 0)
                    rtnValue.Labels = string.Join(",", labelsString.ToArray());
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

        public Secret GetSecret(string alias)
        {
            _logger.MethodEntry(LogLevel.Debug);
            string rtnValue = string.Empty;

            try
            { 
                return Client.GetSecret(
                    new GetSecretRequest()
                    {
                        SecretName = SecretName.FromProjectSecret(ProjectId, alias)
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(GCPException.FlattenExceptionMessages(ex, $"Error retrieving secret {alias}: "));
                throw;
            }
            finally
            {
                _logger.MethodExit(LogLevel.Debug);
            }
        }

        public bool AddSecret(string alias, string secretContent, bool entryExists, string labels = null, List<ReplicationRegion> replicationRegions = null, TimeSpan? ttlDuration = null, TimeSpan? versionDestroyTtlDuration = null)
        {
            _logger.MethodEntry(LogLevel.Debug);

            bool rtnWarning = false;

            try
            {
                SecretName secretName = new SecretName(ProjectId, alias);

                if (!entryExists)
                {
                    AccessSecretVersionRequest request = new AccessSecretVersionRequest();

                    MapField<string, string> labelMap = new MapField<string, string>();
                    if (labels != null)
                    {
                        foreach (string label in labels.Split(','))
                        {
                            string[] labelParts = label.Split(':');
                            if (labelParts.Length != 2)
                            {
                                _logger.LogError($"Invalid label format - {label}.  Label ignored.");
                                rtnWarning = true;
                                continue;
                            }
                            labelMap[labelParts[0]] = labelParts[1];
                        }
                    }

                    List<(string, string)> labelsList = labels != null ? null :
                        labels.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(pair => pair.Split(':', 2))
                        .Where(parts => parts.Length == 2)
                        .Select(parts => (Key: parts[0].Trim(), Value: parts[1].Trim()))
                        .ToList();

                    //create secret
                    CreateSecretRequest secretRequest = new CreateSecretRequest();
                    secretRequest.ParentAsProjectName = new ProjectName(ProjectId);
                    secretRequest.SecretId = alias;

                    secretRequest.Secret = new Secret();
                    if (ttlDuration.HasValue) secretRequest.Secret.Ttl = Duration.FromTimeSpan(ttlDuration.Value);
                    if (versionDestroyTtlDuration.HasValue) secretRequest.Secret.VersionDestroyTtl = Duration.FromTimeSpan(versionDestroyTtlDuration.Value);
                    if (replicationRegions == null || replicationRegions.Count == 0)
                    {
                        secretRequest.Secret.Replication = new Replication { Automatic = new Replication.Types.Automatic() };
                    }
                    else
                    {
                        secretRequest.Secret.Replication = new Replication { UserManaged = new Replication.Types.UserManaged() };

                        foreach (ReplicationRegion replicationRegion in replicationRegions)
                        {
                            Replication.Types.UserManaged.Types.Replica replica = new Replication.Types.UserManaged.Types.Replica();
                            replica.Location = replicationRegion.Region;
                            if (replicationRegion.KmsKeyPath != null)
                                replica.CustomerManagedEncryption = new CustomerManagedEncryption() { KmsKeyName = replicationRegion.KmsKeyPath };

                            secretRequest.Secret.Replication.UserManaged.Replicas.Add(replica);
                        }
                    }

                    foreach (var key in labelMap.Keys)
                        secretRequest.Secret.Labels[key] = labelMap[key];

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

            return rtnWarning;
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

        public List<TagKeyValue> GetTagKeysValues()
        {
            _logger.MethodEntry(LogLevel.Debug);

            List<TagKeyValue> tagKeyValues = new List<TagKeyValue>();
            
            ListTagKeysRequest request = new ListTagKeysRequest()
            {
                ParentAsResourceName = OrganizationName.FromOrganization(GetOrganizationFromProject()),
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
                            tagKeyValueItem.TagKey = item;
                            tagKeyValueItem.TagValues = GetTagValues(item.Name.Substring(item.Name.IndexOf("/") + 1));

                            tagKeyValues.Add(tagKeyValueItem);
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

            return tagKeyValues;
        }

        public List<TagValue> GetTagValues(string tagName)
        {
            _logger.MethodEntry(LogLevel.Debug);

            List<TagValue> rtnValues = new List<TagValue>();

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
                            rtnValues.Add(item);
                        }
                    }

                    request.PageToken = response.NextPageToken;
                } while (!string.IsNullOrEmpty(response.NextPageToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(GCPException.FlattenExceptionMessages(ex, $"Error retrieving Tag Values for tag {tagName}: "));
                throw;
            }
            finally
            {
                _logger.MethodExit(LogLevel.Debug);
            }

            return rtnValues;
        }

        public string GetSecretTags(string secretResource)
        {
            _logger.MethodEntry(LogLevel.Debug);
            string rtnValue = string.Empty;
            List<string> tagPairs = new List<string>();

            ListTagBindingsRequest request = new ListTagBindingsRequest()
            {
                ParentAsResourceName = new UnparsedResourceName($"//secretmanager.googleapis.com/{secretResource}")
            };

            try
            {
                ListTagBindingsResponse response = TagBindingsClient.ListTagBindings(request).AsRawResponses().FirstOrDefault();
                foreach (TagBinding tagBinding in response.TagBindings)
                {
                    TagValue tagValue = TagValuesClient.GetTagValue(tagBinding.TagValue);
                    TagKey tagKey = TagKeysClient.GetTagKey(tagValue.Parent);
                    tagPairs.Add(tagKey.ShortName + ":" + tagValue.ShortName);
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
                rtnValue = string.Join(",", tagPairs);

            return rtnValue;
        }

        public void SetSecretTag(string alias, string tagValue)
        {
            _logger.MethodEntry(LogLevel.Debug);

            try
            {
                TagBindingsClient.CreateTagBinding(new CreateTagBindingRequest()
                {
                    TagBinding = new TagBinding()
                    {
                        Parent = $"{ResourcePrefix}{GetSecret(alias).Name}",
                        TagValue = tagValue
                    }
                });
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
        }

        private string GetOrganizationFromProject()
        {
            string organization = string.Empty;

            Project project = ProjectsClient.GetProject(new GetProjectRequest() { ProjectName = ProjectName.FromProject(ProjectId) });
            organization = project.Parent;

            return organization.Substring(organization.IndexOf("/") + 1);
        }
    }
}
