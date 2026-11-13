using System.Linq;
using System.Text;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Shares talking to Vincent between players in the same room (#65 follow-up, phase 1).
/// When one player asks him (hold B), everyone else sees "Player 1 is asking Vincent…" and can't ask; when the answer
/// comes, everyone hears it from where he stands (from the same proxy) and sees the question and his words. Then the
/// next player may ask. One question at a time; if two players press B at once, the lower player number goes first
/// and the other's question is dropped.
///
/// On each player's avatar (NetworkPlayer prefabs; added by their builders). Only your own avatar sends; every
/// avatar's copy receives. Needs every player's AI Proxy Settings to point at the same proxy (docs/vangogh/README.md).
///
/// Shared in both scenes: the Gallery portrait and the Painted Bedroom's Vincent, whose mirror scene MirrorLink shares
/// (#77), so his answers there match what everyone sees.
///
/// With Convai, everyone shares one conversation per scene: after each answer the asker sends that scene's Convai
/// session ID (RPC_Conversation), and every headset uses it for its next question, so Convai has everyone's questions
/// in one history and a follow-up on someone else's question makes sense. Someone who joins gets the IDs a couple of
/// seconds after their avatar appears. Questions carry who asked ("Player 2") when more than one player is here.
/// </summary>
public class VincentLink : NetworkBehaviour
{
    // A normal Fusion RPC carries at most 512 bytes, so texts travel in pieces of this many characters.
    const int PieceChars = 100;
    const int MaxChars = 1200;            // longer texts are cut (his answers are 1–3 sentences)
    const float LockSeconds = 75f;        // a lock nobody releases (the asker crashed) ends after this

    /// <summary>Your own avatar's link, once it has spawned.</summary>
    public static VincentLink Local { get; private set; }

    // Receiving: who holds the lock, and the pieces of the message being put back together.
    static PlayerRef holder = PlayerRef.None;
    static float holderUntil;
    static int incomingId = -1;
    static string[] questionParts, replyParts;

    // Sending (only on your own avatar).
    SpeechToProxy talker;
    bool holding;      // we told the room we are asking, and haven't released it yet
    int sentId;
    PlayerRef owner;   // whose avatar this is
    float nextSearch;
    float shareAt = -1f;   // when to resend our Convai conversations, for someone who just joined

    // Fresh shared state for every run, even with domain reload turned off in Enter Play Mode Options.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Local = null; holder = PlayerRef.None; holderUntil = 0f; incomingId = -1; questionParts = replyParts = null;
    }

    public override void Spawned()
    {
        owner = Object.StateAuthority;
        if (HasStateAuthority) Local = this;
        else if (Local != null) Local.shareAt = Time.time + 2f;   // someone joined: once they're set up, tell them
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Local == this) Local = null;
        if (owner == holder) Release();   // a player who leaves mid-question releases the lock for everyone
    }

    void OnDisable()
    {
        if (talker != null) { talker.Answered -= OnAnswered; talker.ThinkingStarted -= OnThinking; Unname(talker); }
    }

    void Update()
    {
        if (holder != PlayerRef.None && Time.time > holderUntil) Release();   // safety net
        if (Object == null || !Object.IsValid || !HasStateAuthority) return;

        if (shareAt >= 0f && Time.time >= shareAt)
        {
            shareAt = -1f;
            foreach (var c in SpeechToProxy.Conversations) RPC_Conversation(c.scene, c.id, c.roomKnown, true);
        }

        var current = talker;
        if (talker == null || talker.gameObject.scene != SceneManager.GetActiveScene())
        {
            if (Time.unscaledTime < nextSearch) return;   // no Vincent here: look again in a second, not every frame
            nextSearch = Time.unscaledTime + 1f;
            current = FindTalker();
        }
        if (current != talker)
        {
            if (talker != null) { talker.Answered -= OnAnswered; talker.ThinkingStarted -= OnThinking; Unname(talker); }
            talker = current;
            if (talker != null) { talker.Answered += OnAnswered; talker.ThinkingStarted += OnThinking; talker.AskerName = AskerName; }
            holding = false;
        }
        if (talker == null || !Shared(talker)) return;

        if (talker.IsAsking && !holding)
        {
            holding = true;
            RPC_Asking();
        }
        else if (holding && !talker.IsBusy)   // our question failed, was cancelled, or its answer has finished
        {
            holding = false;
            // the conversation first, so whoever asks next, once the lock is released, continues it
            if (SpeechToProxy.TryGetConversation(talker.gameObject.scene.name, out var id, out bool roomKnown))
                RPC_Conversation(talker.gameObject.scene.name, id, roomKnown, false);
            RPC_Done();
        }
    }

    // One part of his answer: the whole answer from the proxy, or a sentence or two from Convai's stream (#157), which
    // every headset speaks as it arrives. `last`: no more parts (its text may be empty).
    void OnAnswered(string transcript, string reply, string audioUrl, int part, bool last)
    {
        if (!holding || !Shared(talker)) return;
        if (!string.IsNullOrEmpty(reply))
        {
            int id = ++sentId;
            var q = Pieces(part == 0 ? transcript : "");   // the question travels once, with the first part
            var r = Pieces(reply);
            RPC_Begin(id, (byte)q.Length, (byte)r.Length);
            for (int i = 0; i < q.Length; i++) RPC_Piece(id, 0, (byte)i, q[i]);
            for (int i = 0; i < r.Length; i++) RPC_Piece(id, 1, (byte)i, r[i]);
            RPC_Answer(id, audioUrl ?? "", (byte)Mathf.Min(part, 255));
        }
        if (last) RPC_End();
    }

    // The asker sent the question: everyone hears the same "Hmm, let me think." (VincentVoice.ThinkingLines).
    void OnThinking(int line)
    {
        if (holding && Shared(talker)) RPC_Thinking((byte)line);
    }

    // ---- messages (sent by your own avatar, run on everyone else's copy of it) ----

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, InvokeLocal = false)]
    void RPC_Asking()
    {
        var asker = owner;
        // Two at once: the lower player number keeps the lock, and the other one stops asking.
        if (holder != PlayerRef.None && holder != asker && holder.PlayerId < asker.PlayerId) return;
        var mine = Local;
        // We are asking too, and we go first: ignore theirs (they stop when ours reaches them).
        if (mine != null && mine.holding && asker.PlayerId > mine.owner.PlayerId) return;
        if (mine != null && mine.holding && mine.talker != null && mine.talker.IsAsking
            && asker.PlayerId < mine.owner.PlayerId)
        {
            mine.talker.Cancel();
            mine.holding = false;   // the other player's lock already covers us; no "done" needed
            Debug.Log($"[VincentLink] {Name(asker)} asked first; dropped our question");
        }
        Hold(asker);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, InvokeLocal = false)]
    void RPC_Done()
    {
        if (holder == owner) Release();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, InvokeLocal = false)]
    void RPC_Begin(int id, byte questionCount, byte replyCount)
    {
        incomingId = id;
        questionParts = new string[questionCount];
        replyParts = new string[replyCount];
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, InvokeLocal = false)]
    void RPC_Piece(int id, byte which, byte index, string text)
    {
        if (id != incomingId) return;
        var parts = which == 0 ? questionParts : replyParts;
        if (parts != null && index < parts.Length) parts[index] = text;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, InvokeLocal = false)]
    void RPC_Answer(int id, string audioUrl, byte part)
    {
        if (id != incomingId) return;
        if (holder != PlayerRef.None && holder != owner) return;   // someone else holds him (e.g. a late joiner asked)
        var t = FindTalker();
        if (t == null || !Shared(t)) return;
        string transcript = Join(questionParts), reply = Join(replyParts);
        if (string.IsNullOrEmpty(reply)) return;
        holderUntil = Time.time + LockSeconds;   // he speaks now; the asker releases when he stops
        t.SayRemote(Name(owner), transcript, reply, audioUrl, part);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, InvokeLocal = false)]
    void RPC_End()
    {
        if (holder != PlayerRef.None && holder != owner) return;
        var t = FindTalker();
        if (t != null && Shared(t)) t.EndRemote();
    }

    // A scene's Convai conversation, from the player who last talked to him there (or a resend for a newcomer, who
    // only takes it if they have none). Every headset keeps it for that scene, whichever scene it is in now.
    [Rpc(RpcSources.StateAuthority, RpcTargets.All, InvokeLocal = false)]
    void RPC_Conversation(string scene, string id, NetworkBool roomKnown, NetworkBool onlyIfNone)
    {
        SpeechToProxy.Adopt(scene, id, roomKnown, onlyIfNone);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, InvokeLocal = false)]
    void RPC_Thinking(byte line)
    {
        if (holder != PlayerRef.None && holder != owner) return;
        var t = FindTalker();
        if (t != null && Shared(t)) t.ThinkRemote(line);
    }

    // ---- helpers ----

    static void Hold(PlayerRef asker)
    {
        holder = asker;
        holderUntil = Time.time + LockSeconds;
        var t = FindTalker();
        if (t != null && Shared(t)) t.BlockedBy = Name(asker);
    }

    static void Release()
    {
        holder = PlayerRef.None;
        var t = FindTalker();
        if (t != null) { t.BlockedBy = null; t.EndRemote(); }   // whatever came of the answer, no more is coming
    }

    /// <summary>Every Vincent is shared (the Painted Bedroom's since MirrorLink shares its mirror scene).</summary>
    static bool Shared(SpeechToProxy t) => t != null;

    static SpeechToProxy FindTalker()
    {
        var active = SceneManager.GetActiveScene();
        foreach (var t in FindObjectsByType<SpeechToProxy>(FindObjectsSortMode.None))
            if (t.gameObject.scene == active) return t;
        return null;
    }

    // Who asks, for Convai: only when someone else is here to confuse him with.
    string AskerName() => Runner != null && Runner.ActivePlayers.Count() > 1 ? Name(owner) : null;

    void Unname(SpeechToProxy t)
    {
        if (t.AskerName == (System.Func<string>)AskerName) t.AskerName = null;
    }

    // Same name as the avatar ("Player 2"), so people can tell who is asking.
    static string Name(PlayerRef p) => $"Player {p.PlayerId}";

    static string[] Pieces(string text)
    {
        text ??= "";
        if (text.Length > MaxChars) text = text.Substring(0, MaxChars - 1) + "…";
        int count = Mathf.Max(1, (text.Length + PieceChars - 1) / PieceChars);
        var pieces = new string[count];
        for (int i = 0; i < count; i++)
            pieces[i] = text.Substring(i * PieceChars, Mathf.Min(PieceChars, text.Length - i * PieceChars));
        return pieces;
    }

    static string Join(string[] parts)
    {
        if (parts == null) return "";
        var sb = new StringBuilder();
        foreach (var p in parts) sb.Append(p);
        return sb.ToString();
    }
}
