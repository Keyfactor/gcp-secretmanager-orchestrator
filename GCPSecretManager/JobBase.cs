﻿using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

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

            CertificateFormatter = GetCertificateFormatter();
        }

        internal ICertificateFormatter GetCertificateFormatter()
        {
            return new PEMCertificateFormatter();
        }
    }
}
