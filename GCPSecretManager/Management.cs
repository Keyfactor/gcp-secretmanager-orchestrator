﻿using System;
using System.Collections.Generic;

using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Keyfactor.Extensions.Orchestrator.GCPSecretManager;

using Microsoft.Extensions.Logging;

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
                return new JobResult() { Result = Keyfactor.Orchestrators.Common.Enums.OrchestratorJobStatusJobResult.Failure, JobHistoryId = config.JobHistoryId, FailureMessage = GCPException.FlattenExceptionMessages(ex, $"Site {config.CertificateStoreDetails.StorePath} on server {config.CertificateStoreDetails.ClientMachine}: Error adding certificate for {config.JobCertificate.Alias}.  ") };
            }

            return new JobResult() { Result = Keyfactor.Orchestrators.Common.Enums.OrchestratorJobStatusJobResult.Success, JobHistoryId = config.JobHistoryId };
        }

        private void PerformAdd(ManagementJobConfiguration config, GCPClient client)
        {
            Logger.MethodEntry(LogLevel.Debug);

            string alias = config.JobCertificate.Alias;
            bool entryExists = client.Exists(alias);
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
                client.AddSecret(alias, secret, entryExists);
                if (!string.IsNullOrEmpty(newPassword) && string.IsNullOrEmpty(StorePassword))
                {
                    bool passwordEntryExists = client.Exists(alias + PasswordSecretSuffix);
                    client.AddSecret(alias + PasswordSecretSuffix, newPassword, passwordEntryExists);
                }
            }
            catch { throw; }
            finally
            {
                Logger.MethodExit(LogLevel.Debug);
            }
        }
    }
}