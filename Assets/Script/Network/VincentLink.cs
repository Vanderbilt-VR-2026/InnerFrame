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
/// Phase 1 shares only where Vincent has no MirrorScene (the Gallery). In the Painted Bedroom the mirror, his canvas
/// and his scripted lines are still per headset (#77), so sharing his answers there would contradict what each player
/// sees; it stays private until #77 shares the scene too.
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
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Local == this) Local = null;
        if (owner == holder) Release();   // a player who leaves mid-question releases the lock for everyone
    }

    void OnDisable()
    {
        if (talker != null) talker.Answered -= OnAnswered;
    }

    void Update()
    {
        if (holder != PlayerRef.None && Time.time > holderUntil) Release();   // safety net
        if (Object == null || !Object.IsValid || !HasStateAuthority) return;

        var current = talker;
        if (talker == null || talker.gameObject.scene != SceneManager.GetActiveScene())
        {
            if (Time.unscaledTime < nextSearch) return;   // no Vincent here: look again in a second, not every frame
            nextSearch = Time.unscaledTime + 1f;
            current = FindTalker();
        }
        if (current != talker)
        {
            if (talker != null) talker.Answered -= OnAnswered;
            talker = current;
            if (talker != null) talker.Answered += OnAnswered;
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
            RPC_Done();
        }
    }

    void OnAnswered(string transcript, string reply, string audioUrl)
    {
        if (!holding || !Shared(talker)) return;
        int id = ++sentId;
        var q = Pieces(transcript);
        var r = Pieces(reply);
        RPC_Begin(id, (byte)q.Length, (byte)r.Length);
        for (int i = 0; i < q.Length; i++) RPC_Piece(id, 0, (byte)i, q[i]);
        for (int i = 0; i < r.Length; i++) RPC_Piece(id, 1, (byte)i, r[i]);
        RPC_Answer(id, audioUrl ?? "");
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
    void RPC_Answer(int id, string audioUrl)
    {
        if (id != incomingId) return;
        if (holder != PlayerRef.None && holder != owner) return;   // someone else holds him (e.g. a late joiner asked)
        var t = FindTalker();
        if (t == null || !Shared(t)) return;
        string transcript = Join(questionParts), reply = Join(replyParts);
        if (string.IsNullOrEmpty(reply)) return;
        holderUntil = Time.time + LockSeconds;   // he speaks now; the asker releases when he stops
        t.SayRemote(Name(owner), transcript, reply, audioUrl);
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
        if (t != null) t.BlockedBy = null;
    }

    /// <summary>Phase 1: shared only where there is no mirror scene (see the class comment).</summary>
    static bool Shared(SpeechToProxy t) => t.GetComponent<MirrorScene>() == null;

    static SpeechToProxy FindTalker()
    {
        var active = SceneManager.GetActiveScene();
        foreach (var t in FindObjectsByType<SpeechToProxy>(FindObjectsSortMode.None))
            if (t.gameObject.scene == active) return t;
        return null;
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
