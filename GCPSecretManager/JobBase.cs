using Google.Protobuf.WellKnownTypes;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Keyfactor.Extensions.Orchestrator.GCPSecretManager
{
    public class JobBase
    {
        internal ICertificateFormatter CertificateFormatter { get; set; }
        internal ILogger Logger { get; set; }
        internal IPAMSecretResolver Resolver { get; set; }
        internal string StorePassword { get; set; }
        internal string ProjectId { get; set; }
        internal string PasswordSecretSuffix { get; set; }
        internal bool IncludeChain { get; set; }
        internal List<ReplicationRegion> ReplicationRegions { get; set; }
        internal TimeSpan TTLDuration { get; set; }
        internal TimeSpan VersionDestroyTtlDuration { get; set; }


        internal void Initialize(CertificateStore certificateStoreDetails)
        {
            Logger.MethodEntry(LogLevel.Debug);

            StorePassword = PAMUtilities.ResolvePAMField(Resolver, Logger, "Store Password", certificateStoreDetails.StorePassword);
            ProjectId = certificateStoreDetails.StorePath;

            string errMessage = string.Empty;
            dynamic properties = JsonConvert.DeserializeObject(certificateStoreDetails.Properties.ToString());

            if (properties.PasswordSecretSuffix != null)
                PasswordSecretSuffix = properties.PasswordSecretSuffix.Value;

            IncludeChain = properties.IncludeChain == null || string.IsNullOrEmpty(properties.IncludeChain.Value) ? true : bool.Parse(properties.IncludeChain.Value);

            if (properties.ReplicationRegions != null)
            {
                string property = properties.ReplicationRegions.Value;
                string[] replicationRegionStrings = property.Split(',');
                foreach(string replicationRegionString in replicationRegionStrings)
                {
                    string[] replicationRegionStringArr = replicationRegionString.Split(":");
                    ReplicationRegion replicationRegion = new ReplicationRegion();
                    replicationRegion.Region = replicationRegionStringArr[0];
                    if (replicationRegionStringArr.Length > 1)
                        replicationRegion.KmsKeyPath = replicationRegionStringArr[1];
                }
            }

            int ttlDuration = 0;
            if (properties.TtlDuration != null && int.TryParse(properties.TtlDuration.Value, out ttlDuration))
            {
                TTLDuration = TimeSpan.FromDays(ttlDuration);
            }

            int versionDestroyTtlDuration = 0;
            if (properties.VersionDestroyTtlDuration != null && int.TryParse(properties.VersionDestroyTtlDuration.Value, out versionDestroyTtlDuration))
            {
                VersionDestroyTtlDuration = TimeSpan.FromDays(versionDestroyTtlDuration);
            }

            CertificateFormatter = GetCertificateFormatter();
        }

        internal ICertificateFormatter GetCertificateFormatter()
        {
            return new PEMCertificateFormatter();
        }
    }
}
