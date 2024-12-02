using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Keyfactor.Extensions.Orchestrator.GCPSecretManager

{
    interface ICertificateFormatter
    {
        bool HasPrivateKey(string entry);

        bool IsValid(string entry);

        string[] FormatCertificates(string entry);
    }
}
