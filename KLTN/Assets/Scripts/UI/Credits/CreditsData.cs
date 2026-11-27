using System;
using UnityEngine;

namespace EchoProtocol.UI.Credits
{
    [CreateAssetMenu(menuName = "ECHO Protocol/Credits Data")]
    public sealed class CreditsData : ScriptableObject
    {
        public enum Layout { People, Opening, Platforms, Attributions, Legal, Closing }

        [Serializable]
        public sealed class Entry
        {
            public string name;
            [TextArea] public string role;
            [TextArea] public string contribution;
        }

        [Serializable]
        public sealed class Block
        {
            public string heading;
            public Layout layout;
            [Range(1, 2)] public int columns = 1;
            [Min(0)] public float travelSeconds;
            [Min(0)] public float holdSeconds;
            public Entry[] entries = Array.Empty<Entry>();
            [TextArea] public string note;
        }

        [Serializable]
        public sealed class Section
        {
            public string id;
            public string title;
            public Block[] blocks = Array.Empty<Block>();
        }

        public string projectTitle = "ECHO PROTOCOL";
        public Sprite logo;
        public Section[] sections = Array.Empty<Section>();
    }
}
