using Google.Cloud.ResourceManager.V3;
using Google.Protobuf.WellKnownTypes;
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
        internal Duration TTLDuration { get; set; }
        internal Duration VersionDestroyTTLDuration { get; set; }
        internal string ReplicationRegions { get; set; }
    }
}
