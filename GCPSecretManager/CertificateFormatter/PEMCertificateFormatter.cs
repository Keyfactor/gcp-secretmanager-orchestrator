using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Logging;

using Keyfactor.Logging;
using Keyfactor.PKI.PEM;
using Keyfactor.PKI.PrivateKeys;
using Keyfactor.PKI.X509;

using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Pkcs;

namespace Keyfactor.Extensions.Orchestrator.GCPSecretManager

{
    class PEMCertificateFormatter : BaseCertificateFormatter
    {
        private string BEGIN_DELIMITER = "-----BEGIN CERTIFICATE-----";
        private string END_DELIMITER = "-----END CERTIFICATE-----";
        private string[] PRIVATE_KEY_DELIMITERS = new string[] { "-----BEGIN PRIVATE KEY-----", "-----BEGIN ENCRYPTED PRIVATE KEY-----", "-----BEGIN RSA PRIVATE KEY-----" };

        public override bool HasPrivateKey(string entry)
        {
            Logger.MethodEntry(LogLevel.Debug);

            bool rtnValue = false;

            foreach (string privateKeyDelimiter in PRIVATE_KEY_DELIMITERS)
            {
                if (entry.Contains(privateKeyDelimiter, StringComparison.OrdinalIgnoreCase))
                {
                    rtnValue = true;
                    break;
                }
            }

            Logger.MethodExit(LogLevel.Debug);

            return rtnValue;
        }

        public override bool IsValid(string entry)
        {
            Logger.MethodEntry(LogLevel.Debug);

            Logger.MethodExit(LogLevel.Debug);

            return entry.Contains(BEGIN_DELIMITER, StringComparison.OrdinalIgnoreCase) && entry.Contains(END_DELIMITER, StringComparison.OrdinalIgnoreCase);
        }

        public override string[] ConvertSecretToCertificateChain(string entry)
        {
            Logger.MethodEntry(LogLevel.Debug);

            List<string> rtnCertificates = new List<String>();
            int currStart = 0;
            int currEnd = 0;

            do
            {
                currStart = entry.IndexOf(BEGIN_DELIMITER, currEnd) + BEGIN_DELIMITER.Length;
                currEnd = entry.IndexOf(END_DELIMITER, currStart);
                rtnCertificates.Add(entry.Substring(currStart + BEGIN_DELIMITER.Length, currEnd - (currStart + BEGIN_DELIMITER.Length)));
                currEnd++;
            }
            while (entry.IndexOf(END_DELIMITER, currEnd) > -1);

            Logger.MethodExit(LogLevel.Debug);

            return rtnCertificates.ToArray();
        }

        public override string ConvertCertificateEntryToSecret(string certificateContents, string privateKeyPassword, bool includeChain, string newPassword)
        {
            Logger.MethodEntry(LogLevel.Debug);

            if (string.IsNullOrEmpty(privateKeyPassword))
                return PemUtilities.DERToPEM(Convert.FromBase64String(certificateContents), PemUtilities.PemObjectType.Certificate);

            Pkcs12StoreBuilder builder = new Pkcs12StoreBuilder();
            Pkcs12Store pkcs12Store = builder.Build();
            using (MemoryStream ms = new MemoryStream(Convert.FromBase64String(certificateContents)))
            {
                pkcs12Store.Load(ms, privateKeyPassword.ToCharArray());
            }

            string alias = pkcs12Store.Aliases.First();

            X509CertificateEntry[] certChainEntries = pkcs12Store.GetCertificateChain(alias);
            CertificateConverter certConverter = CertificateConverterFactory.FromBouncyCastleCertificate(certChainEntries[0].Certificate);

            AsymmetricKeyParameter privateKey = pkcs12Store.GetKey(alias).Key;
            AsymmetricKeyParameter publicKey = certChainEntries[0].Certificate.GetPublicKey();

            PrivateKeyConverter keyConverter = PrivateKeyConverterFactory.FromBCKeyPair(privateKey, publicKey, false);

            byte[] privateKeyBytes = string.IsNullOrEmpty(newPassword) ? keyConverter.ToPkcs8BlobUnencrypted() : keyConverter.ToPkcs8Blob(newPassword);
            string keyString = PemUtilities.DERToPEM(privateKeyBytes, string.IsNullOrEmpty(newPassword) ? PemUtilities.PemObjectType.PrivateKey : PemUtilities.PemObjectType.EncryptedPrivateKey);

            string pemString = certConverter.ToPEM(true);
            pemString += keyString;

            if (includeChain)
            {
                for (int i = 1; i < certChainEntries.Length; i++)
                {
                    CertificateConverter chainConverter = CertificateConverterFactory.FromBouncyCastleCertificate(certChainEntries[i].Certificate);
                    pemString += chainConverter.ToPEM(true);
                }
            }

            Logger.MethodExit(LogLevel.Debug);

            return pemString;
        }
    }
}
