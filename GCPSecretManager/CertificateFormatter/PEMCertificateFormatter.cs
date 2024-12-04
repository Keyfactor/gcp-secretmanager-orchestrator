using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Keyfactor.PKI.PEM;

using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;

namespace Keyfactor.Extensions.Orchestrator.GCPSecretManager

{
    class PEMCertificateFormatter : BaseCertificateFormatter
    {
        private string BEGIN_DELIMITER = "-----BEGIN CERTIFICATE-----";
        private string END_DELIMITER = "-----END CERTIFICATE-----";
        private string[] PRIVATE_KEY_DELIMITERS = new string[] { "-----BEGIN PRIVATE KEY-----", "-----BEGIN ENCRYPTED PRIVATE KEY-----", "-----BEGIN RSA PRIVATE KEY-----" };

        public override string[] FormatCertificates(string entry)
        {
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

            return rtnCertificates.ToArray();
        }

        public override bool HasPrivateKey(string entry)
        {
            bool rtnValue = false;

            foreach (string privateKeyDelimiter in PRIVATE_KEY_DELIMITERS)
            {
                if (entry.Contains(privateKeyDelimiter, StringComparison.OrdinalIgnoreCase))
                {
                    rtnValue = true;
                    break;
                }
            }

            return rtnValue;
        }

        public override bool IsValid(string entry)
        {
            return entry.Contains(BEGIN_DELIMITER, StringComparison.OrdinalIgnoreCase) && entry.Contains(END_DELIMITER, StringComparison.OrdinalIgnoreCase);
        }

        public override string FormatCertificateEntry(string certificateContents, string privateKeyPassword, bool includeChain, string newPassword)
        {
            string rtnValue = string.Empty;

            if (string.IsNullOrEmpty(privateKeyPassword))
                return PemUtilities.DERToPEM(Convert.FromBase64String(certificateContents), PemUtilities.PemObjectType.Certificate);

            Pkcs12StoreBuilder builder = new Pkcs12StoreBuilder();
            Pkcs12Store pkcs12Store = builder.Build();
            using (MemoryStream ms = new MemoryStream(Convert.FromBase64String(certificateContents)))
            {
                pkcs12Store.Load(ms, privateKeyPassword.ToCharArray());
            }




            X509CertificateEntry[] chainEntries = certificateStore.GetCertificateChain(alias);
            CertificateConverter certConverter = CertificateConverterFactory.FromBouncyCastleCertificate(chainEntries[0].Certificate);

            AsymmetricKeyParameter privateKey = certificateStore.GetKey(alias).Key;
            X509CertificateEntry[] certEntries = certificateStore.GetCertificateChain(alias);
            AsymmetricKeyParameter publicKey = certEntries[0].Certificate.GetPublicKey();

            if (isRSAPrivateKey)
            {
                TextWriter textWriter = new StringWriter();
                PemWriter pemWriter = new PemWriter(textWriter);
                pemWriter.WriteObject(privateKey);
                pemWriter.Writer.Flush();

                keyString = textWriter.ToString();
            }
            else
            {
                PrivateKeyConverter keyConverter = PrivateKeyConverterFactory.FromBCKeyPair(privateKey, publicKey, false);

                byte[] privateKeyBytes = string.IsNullOrEmpty(storePassword) ? keyConverter.ToPkcs8BlobUnencrypted() : keyConverter.ToPkcs8Blob(storePassword);
                keyString = PemUtilities.DERToPEM(privateKeyBytes, string.IsNullOrEmpty(storePassword) ? PemUtilities.PemObjectType.PrivateKey : PemUtilities.PemObjectType.EncryptedPrivateKey);
            }

            pemString = certConverter.ToPEM(true);
            if (string.IsNullOrEmpty(SeparatePrivateKeyFilePath))
                pemString += keyString;

            if (IncludesChain)
            {
                for (int i = 1; i < chainEntries.Length; i++)
                {
                    CertificateConverter chainConverter = CertificateConverterFactory.FromBouncyCastleCertificate(chainEntries[i].Certificate);
                    pemString += chainConverter.ToPEM(true);
                }
            }




            string pkcs12Alias = pkcs12Store.Aliases.First();
            X509CertificateEntry[] certChain = pkcs12Store.GetCertificateChain(pkcs12Alias);

            AsymmetricKeyEntry privateKeyEntry = pkcs12Store.GetKey(pkcs12Alias);
            if (!string.IsNullOrEmpty(newPassword))
            {
                Pkcs8Generator pkcs8Generator = new Pkcs8Generator(privateKeyEntry.Key, Pkcs8Generator.PbeSha1_3DES)
            }


            using (StringWriter certWriter = new StringWriter())
            {
                PemWriter pemWriter = new PemWriter(certWriter);
                pemWriter.WriteObject(certChain[0].Certificate);
                pemWriter.WriteObject(privateKey);

                if (includeChain)
                {
                    for (int i = 1; i < certChain.Length; i++)
                    {
                        pemWriter.WriteObject(certChain[i].Certificate);
                    }
                }

                pemWriter.Writer.Flush();
                rtnValue = certWriter.ToString();
                certWriter.Close();
            }

            return rtnValue;
        }
    }
}
