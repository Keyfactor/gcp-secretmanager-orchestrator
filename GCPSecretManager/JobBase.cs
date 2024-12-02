using Keyfactor.Logging;
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

        internal void Initialize(CertificateStore certificateStoreDetails)
        {
            Logger.MethodEntry(LogLevel.Debug);

            StorePassword = PAMUtilities.ResolvePAMField(Resolver, Logger, "Store Password", certificateStoreDetails.StorePassword);

            dynamic properties = JsonConvert.DeserializeObject(certificateStoreDetails.Properties.ToString());
            ProjectId = properties.ProjectId?.Value;
            if (string.IsNullOrEmpty(ProjectId))
            {
                string errMessage = "ProjectId missing or empty.  Please provide a valid ProjectId in the certificate store definition.";
                Logger.LogError(errMessage);
                throw new GCPException(errMessage);
            }

            CertificateFormatter = GetCertificateFormatter();
        }

        internal ICertificateFormatter GetCertificateFormatter()
        {
            return new PEMCertificateFormatter();
        }
    }
}
