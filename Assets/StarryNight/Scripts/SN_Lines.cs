using System;
using UnityEngine;

namespace InnerFrame.StarryNight
{
    /// <summary>Vincent's lines: id, text and voice clip. Built by SN_Setup from Art/Source/StarryNight/lines.txt and
    /// the clips make_lines.sh records (Assets/StarryNight/Audio/SN_&lt;id&gt;.wav).</summary>
    [CreateAssetMenu(menuName = "InnerFrame/Starry Night/Lines", fileName = "SN_Lines")]
    public class SN_Lines : ScriptableObject
    {
        [Serializable]
        public class Line
        {
            public string id;
            [TextArea] public string text;
            public AudioClip clip;
        }

        public Line[] lines = new Line[0];

        public Line Get(string id) => Array.Find(lines, l => l.id == id);
    }
}
