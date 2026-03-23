using Google.Cloud.ResourceManager.V3;
using System.Collections.Generic;

namespace Keyfactor.Extensions.Orchestrator.GCPSecretManager
{
    internal class TagKeyValue
    {
        internal TagKey TagKey { get; set; }
        internal List<TagValue> TagValues { get; set; }
    }
}
