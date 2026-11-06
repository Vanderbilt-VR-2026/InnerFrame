using UnityEngine;

/// <summary>
/// Vincent through Convai's web API, straight from the headset (no proxy, no Convai SDK): SpeechToProxy sends the
/// recorded question to https://api.convai.com/character/getResponse and plays the voice it returns.
///
/// One asset, Assets/VanGogh/Resources/ConvaiDirectSettings (Tools > InnerFrame > Van Gogh > Set Up Convai Direct):
/// Use Convai and Vincent's Character ID. The API key is NOT here: it goes in Assets/VanGogh/Resources/ConvaiApiKey.txt
/// (one line), which git ignores because the repo is public. Builds include it, so share APKs with care.
/// With Use Convai off, no Character ID or no key, Vincent uses the AI proxy exactly as before.
/// </summary>
[CreateAssetMenu(menuName = "InnerFrame/Convai Direct Settings", fileName = "ConvaiDirectSettings")]
public class ConvaiDirectSettings : ScriptableObject
{
    public const string KeyResource = "ConvaiApiKey";   // Resources/ConvaiApiKey.txt
    public const string KeyPath = "Assets/VanGogh/Resources/" + KeyResource + ".txt";
    public const string Endpoint = "https://api.convai.com/character/getResponse";

    [Tooltip("Talk to Vincent through Convai. Off: the AI proxy, as before.")]
    public bool useConvai = true;

    [Tooltip("Vincent's Character ID from convai.com: only the ID, like 1a2b3c4d-1a2b-1a2b-1a2b-1a2b3c4d5e6f.")]
    public string characterId = "";

    static ConvaiDirectSettings current;
    static string apiKey;
    static bool keyLoaded;

    public static ConvaiDirectSettings Current =>
        current != null ? current : current = Resources.Load<ConvaiDirectSettings>("ConvaiDirectSettings");

    /// <summary>The key from Resources/ConvaiApiKey.txt, or empty.</summary>
    public static string ApiKey
    {
        get
        {
            if (!keyLoaded)
            {
                var file = Resources.Load<TextAsset>(KeyResource);
                apiKey = file != null ? file.text.Trim() : "";
                keyLoaded = true;
            }
            return apiKey;
        }
    }

    /// <summary>Convai is on and has a character and a key: SpeechToProxy asks Convai instead of the proxy.</summary>
    public static bool Ready
    {
        get
        {
            var s = Current;
            return s != null && s.useConvai && !string.IsNullOrWhiteSpace(s.characterId) && ApiKey.Length > 0;
        }
    }

    /// <summary>Why Ready is false, for the log; null when it's true.</summary>
    public static string WhyNotReady() => WhyNotReadyInEditor(Current);

    public static string WhyNotReadyInEditor(ConvaiDirectSettings s)
    {
        if (s == null) return "no Resources/ConvaiDirectSettings (Tools > InnerFrame > Van Gogh > Set Up Convai Direct)";
        if (!s.useConvai) return "Use Convai is off";
        if (string.IsNullOrWhiteSpace(s.characterId)) return "no Character ID in ConvaiDirectSettings";
        var key = Resources.Load<TextAsset>(KeyResource);
        if (key == null || key.text.Trim().Length == 0) return $"no API key in {KeyPath}";
        return null;
    }
}
