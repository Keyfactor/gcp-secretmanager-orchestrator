using Google.Cloud.ResourceManager.V3;
using System.Collections.Generic;

namespace Keyfactor.Extensions.Orchestrator.GCPSecretManager
{
    internal enum TagScope
    {
        Organization,
        Project
    }

    internal class TagKeyValue
    {
        internal TagScope TagScope { get; set; },
        internal TagKey TagKey { get; set; },
        internal List<TagValue> TagValues { get; set; }
    }
}
