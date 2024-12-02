using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
