using Google.Cloud.ResourceManager.V3;
using System.Collections.Generic;

namespace Keyfactor.Extensions.Orchestrator.GCPSecretManager
{
    internal class TagKeyValue
    {
        internal TagKey TagKey { get; set; }
        internal List<TagValue> TagValues { get; set; }
    }

    internal class ReplicationRegion
    {
        internal string Region {  get; set; }
        internal string KmsKeyPath { get; set; }
    }

    internal class SecretWithLabels
    {
        internal string Secret { get; set; }
        internal string Labels { get; set; }
    }
}
