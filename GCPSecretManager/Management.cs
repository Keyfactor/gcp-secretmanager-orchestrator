using Google.Cloud.ResourceManager.V3;
using Keyfactor.Extensions.Orchestrator.GCPSecretManager;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace Keyfactor.Extensions.Orchestrator.GCPSecretManager
{
    public class Management : JobBase, IManagementJobExtension
    {
        public string ExtensionName => "Keyfactor.Extensions.Orchestrator.GCPSecretManager.Management";

        public Management(IPAMSecretResolver resolver)
        {
            Resolver = resolver;
        }

        public JobResult ProcessJob(ManagementJobConfiguration config)
        {
            Logger = LogHandler.GetClassLogger(this.GetType());
            Logger.LogDebug($"Begin {config.Capability} for job id {config.JobId}...");
            Logger.LogDebug($"Server: {config.CertificateStoreDetails.ClientMachine}");
            Logger.LogDebug($"Store Path: {config.CertificateStoreDetails.StorePath}");
            Logger.LogDebug($"Job Properties:");
            foreach (KeyValuePair<string, object> keyValue in config.JobProperties ?? new Dictionary<string, object>())
            {
                Logger.LogDebug($"    {keyValue.Key}: {keyValue.Value}");
            }

            try
            {
                Initialize(config.CertificateStoreDetails);

                GCPClient client = new GCPClient(ProjectId);

                switch (config.OperationType)
                {
                    case CertStoreOperationType.Add:
                        string tagsMessage = string.Empty;
                        string labelsMessage = string.Empty;
                        string message = string.Empty;

                        bool entryExists = client.Exists(config.JobCertificate.Alias);

                        string warningMessages = PerformAdd(config, client, entryExists);

                        if (!string.IsNullOrEmpty(warningMessages))
                            return new JobResult() { Result = OrchestratorJobStatusJobResult.Warning, JobHistoryId = config.JobHistoryId, FailureMessage = $"Certificate added successfully, but {message}" };
                        
                        break;
                    case CertStoreOperationType.Remove:
                        client.DeleteCertificate(config.JobCertificate.Alias);
                        break;
                    default:
                        return new JobResult() { Result = Keyfactor.Orchestrators.Common.Enums.OrchestratorJobStatusJobResult.Failure, JobHistoryId = config.JobHistoryId, FailureMessage = $"Site {config.CertificateStoreDetails.StorePath} on server {config.CertificateStoreDetails.ClientMachine}: Unsupported operation: {config.OperationType.ToString()}" };
                }
            }
            catch (Exception ex)
            {
                return new JobResult() { Result = Keyfactor.Orchestrators.Common.Enums.OrchestratorJobStatusJobResult.Failure, JobHistoryId = config.JobHistoryId, FailureMessage = GCPException.FlattenExceptionMessages(ex, $"Site {config.CertificateStoreDetails.StorePath} on server {config.CertificateStoreDetails.ClientMachine}: Error adding certificate for {config.JobCertificate.Alias}.  ") };
            }

            return new JobResult() { Result = Keyfactor.Orchestrators.Common.Enums.OrchestratorJobStatusJobResult.Success, JobHistoryId = config.JobHistoryId };
        }

        private string PerformAdd(ManagementJobConfiguration config, GCPClient client, bool entryExists)
        {
            Logger.MethodEntry(LogLevel.Debug);

            string rtnWarnings = string.Empty;

            string alias = config.JobCertificate.Alias;
            string newPassword = string.Empty;

            if (!config.Overwrite && entryExists)
            {
                string errMsg = $"Secret {alias} already exists but Overwrite set to False.  Set Overwrite to True to replace the certificate.";
                Logger.LogError(errMsg);
                Logger.MethodExit(LogLevel.Debug);
                throw new GCPException(errMsg);
            }

            if (string.IsNullOrEmpty(StorePassword))
            {
                if (!string.IsNullOrEmpty(PasswordSecretSuffix))
                    newPassword = config.JobCertificate.PrivateKeyPassword;
            }
            else
                newPassword = StorePassword;

            try
            {
                string secret = CertificateFormatter.ConvertCertificateEntryToSecret(config.JobCertificate.Contents, config.JobCertificate.PrivateKeyPassword, IncludeChain, newPassword);
                string labels = (config.JobProperties.ContainsKey("labels") && config.JobProperties["labels"] != null) ? config.JobProperties["labels"].ToString() : null;
                string tags = (config.JobProperties.ContainsKey("tags") && config.JobProperties["tags"] != null) ? config.JobProperties["tags"].ToString() : null;

                int ttlDuration = 0;
                TimeSpan? ttlDurationTS = null;
                if (config.JobProperties.ContainsKey("ttlDuration") && config.JobProperties["ttlDuration"] != null && int.TryParse(config.JobProperties["ttlDuration"].ToString(), out ttlDuration))
                {
                    ttlDurationTS = TimeSpan.FromDays(ttlDuration);
                }

                int versionDestroyTtlDuration = 0;
                TimeSpan? versionDestroyTtlDurationTS = null;
                if (config.JobProperties.ContainsKey("versionDestroyTtlDuration") && config.JobProperties["versionDestroyTtlDuration"] != null && int.TryParse(config.JobProperties["versionDestroyTtlDuration"].ToString(), out versionDestroyTtlDuration))
                {
                    versionDestroyTtlDurationTS = TimeSpan.FromDays(versionDestroyTtlDuration);
                }

                List<ReplicationRegion> ReplicationRegions = new List<ReplicationRegion>();
                if (config.JobProperties.ContainsKey("replicationRegions") && config.JobProperties["replicationRegions"] != null)
                {
                    string property = config.JobProperties["replicationRegions"].ToString();
                    string[] replicationRegionStrings = property.Split(',');
                    foreach (string replicationRegionString in replicationRegionStrings)
                    {
                        string[] replicationRegionStringArr = replicationRegionString.Split(":");
                        ReplicationRegion replicationRegion = new ReplicationRegion();
                        replicationRegion.Region = replicationRegionStringArr[0];
                        if (replicationRegionStringArr.Length > 1)
                            replicationRegion.KmsKeyPath = replicationRegionStringArr[1];

                        ReplicationRegions.Add(replicationRegion);
                    }
                }

                rtnWarnings = client.AddSecret(alias, secret, entryExists, labels, ReplicationRegions, ttlDurationTS, versionDestroyTtlDurationTS, tags);
                if (!string.IsNullOrEmpty(newPassword) && string.IsNullOrEmpty(StorePassword))
                {
                    bool passwordEntryExists = client.Exists(alias + PasswordSecretSuffix);
                    client.AddSecret(alias + PasswordSecretSuffix, newPassword, passwordEntryExists, null, ReplicationRegions, ttlDurationTS, versionDestroyTtlDurationTS);
                }
            }
            finally
            {
                Logger.MethodExit(LogLevel.Debug);
            }

            return rtnWarnings;
        }
    }
}