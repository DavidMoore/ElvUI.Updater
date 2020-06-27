using System.Runtime.Serialization;

namespace SadRobot.ElvUI.Deployment
{
    [DataContract]
    public class UpdateInfo
    {
        [DataMember(Name = "version")]
        public SemanticVersion Version { get; set; }

        [DataMember(Name = "uri")]
        public string Uri { get; set; }
        public bool IsPreRelease { get; set; }
        public string Name { get; set; }
        public SemanticVersion CurrentVersion { get; set; }

        public bool IsUpgrade()
        {
            return Version > CurrentVersion;
        }
    }
}