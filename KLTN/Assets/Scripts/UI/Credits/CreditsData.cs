using System;
using UnityEngine;

namespace EchoProtocol.UI.Credits
{
    [CreateAssetMenu(menuName = "ECHO Protocol/Credits Data")]
    public sealed class CreditsData : ScriptableObject
    {
        [Serializable]
        public sealed class Section
        {
            public string role;
            public string[] names = Array.Empty<string>();
            [TextArea] public string contribution;
            [TextArea] public string evidence;
        }

        public string projectTitle = "ECHO PROTOCOL";
        public Sprite logo;
        public Section[] sections = Array.Empty<Section>();
        public string closingMessage = "THANK YOU FOR PLAYING";
    }
}
