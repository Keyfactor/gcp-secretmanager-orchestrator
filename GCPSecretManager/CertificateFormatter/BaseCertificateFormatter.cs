using Keyfactor.Logging;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Extensions.Orchestrator.GCPSecretManager

{
    abstract class BaseCertificateFormatter : ICertificateFormatter
    {
        internal ILogger Logger { get; set; }

        internal BaseCertificateFormatter()
        {
            Logger = LogHandler.GetClassLogger(this.GetType());
        }

        public abstract bool HasPrivateKey(string entry);

        public abstract bool IsValid(string entry);

        public abstract string[] ConvertSecretToCertificateChain(string entry);

        public abstract string ConvertCertificateEntryToSecret(string certificateContents, string privateKeyPassword, bool includeChain, string newPassword);
    }
}
