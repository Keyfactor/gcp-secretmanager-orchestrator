using System;

using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Common.Enums;

using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using Grpc.Net.Client.Balancer;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Keyfactor.Extensions.Orchestrator.GCPSecretManager;

namespace Keyfactor.Extensions.Orchestrator.SampleOrchestratorExtension
{
    public class Management : JobBase, IManagementJobExtension
    {
        public string ExtensionName => "Keyfactor.Extensions.Orchestrator.GCPSecretManager.Management";

        IPAMSecretResolver _resolver;

        public Management(IPAMSecretResolver resolver)
        {
            _resolver = resolver;
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
                        PerformAdd(config, client);
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
                return new JobResult() { Result = Keyfactor.Orchestrators.Common.Enums.OrchestratorJobStatusJobResult.Failure, JobHistoryId = config.JobHistoryId, FailureMessage = "Custom message you want to show to show up as the error message in Job History in KF Command" };
            }

            return new JobResult() { Result = Keyfactor.Orchestrators.Common.Enums.OrchestratorJobStatusJobResult.Success, JobHistoryId = config.JobHistoryId };
        }

        private void PerformAdd(ManagementJobConfiguration config, GCPClient client)
        {
            string alias = config.JobCertificate.Alias;
            bool entryExists = client.Exists(alias);
            string newPassword = string.Empty;

            if (!config.Overwrite && entryExists)
                throw new GCPException($"Secret {alias} already exists but Overwrite set to False.  Set Overwrite to True to replace the certificate.");

            if (string.IsNullOrEmpty(StorePassword))
            {
                if (!string.IsNullOrEmpty(PasswordSecretSuffix))
                    newPassword = config.JobCertificate.PrivateKeyPassword;
            }
            else
                newPassword = StorePassword;

            string secret = CertificateFormatter.ConvertCertificateEntryToSecret(config.JobCertificate.Contents, config.JobCertificate.PrivateKeyPassword, IncludeChain, newPassword);
            client.AddSecret(alias, secret, entryExists);
            if (!string.IsNullOrEmpty(newPassword) && string.IsNullOrEmpty(StorePassword))
                client.AddSecret(alias + PasswordSecretSuffix, newPassword, entryExists);
        }
    }
}