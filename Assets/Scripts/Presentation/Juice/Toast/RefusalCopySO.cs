using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

/// <summary>The words for one refusal: what happened, then what to do.</summary>
[Serializable]
public class RefusalCopyEntry
{
    public RefusalReason Reason;

    [Tooltip("What happened. Sentence case, no exclamation mark.")]
    public string Title;

    [Tooltip("What to do about it, shown muted after the title. {0} is replaced by the caller's detail.")]
    public string Hint;

    public RefusalCopyEntry() { }

    public RefusalCopyEntry(RefusalReason reason, string title, string hint)
    {
        Reason = reason;
        Title = title;
        Hint = hint;
    }
}

/// <summary>
/// The refusal toast's words and look. One table for every refusal in the game, so the deck page, the info
/// panel and the match can never explain the same no with different words.
/// </summary>
/// <remarks>Lives in Resources so the toast service can load it in any scene.</remarks>
[CreateAssetMenu(fileName = "RefusalCopy", menuName = "Scriptable Objects/Presentation/Refusal Copy")]
public class RefusalCopySO : ScriptableObject
{
    public const string ResourcePath = "Juice/RefusalCopy";

    [Title("Look")]
    [Tooltip("Fredoka Bold SDF: its material carries the game's black outline.")]
    public TMP_FontAsset Font;

    [Tooltip("A 9-sliced rounded sprite for the toast's pill.")]
    public Sprite PillSprite;

    [Tooltip("A circle sprite behind the cross.")]
    public Sprite IconSprite;

    [Tooltip("Above every menu and match canvas and the tutorial overlay (20), below the Quantum Console (30).")]
    public int SortingOrder = 26;

    [Tooltip("Distance of the toast's top edge below the safe area, in canvas units at 1080x1920.")]
    public float TopOffset = 150f;

    [Title("Copy")]
    [TableList(ShowIndexLabels = false, AlwaysExpanded = true)]
    public List<RefusalCopyEntry> Entries = DefaultEntries();

    private Dictionary<RefusalReason, RefusalCopyEntry> _byReason;

    /// <summary>The title and hint for a reason, with the caller's detail formatted into the hint.</summary>
    public void Resolve(RefusalReason reason, string detail, out string title, out string hint)
    {
        RefusalCopyEntry entry = Find(reason) ?? Find(RefusalReason.Generic);
        title = entry != null && !string.IsNullOrEmpty(entry.Title) ? entry.Title : "That didn't work";

        string template = entry?.Hint;
        if (string.IsNullOrEmpty(template)) hint = detail ?? string.Empty;
        else if (template.Contains("{0}")) hint = string.IsNullOrEmpty(detail) ? string.Empty : string.Format(template, detail);
        else hint = template;
    }

    private RefusalCopyEntry Find(RefusalReason reason)
    {
        if (_byReason == null)
        {
            _byReason = new Dictionary<RefusalReason, RefusalCopyEntry>();
            foreach (RefusalCopyEntry e in Entries)
                if (e != null && !_byReason.ContainsKey(e.Reason)) _byReason.Add(e.Reason, e);
        }

        return _byReason.TryGetValue(reason, out RefusalCopyEntry found) ? found : null;
    }

    private void OnValidate() => _byReason = null;

    /// <summary>The design system's copy: what happened, then what to do; sentence case; no exclamation marks.</summary>
    public static List<RefusalCopyEntry> DefaultEntries() => new()
    {
        new(RefusalReason.Generic, "That didn't work", "{0}"),
        new(RefusalReason.NotEnoughMana, "Not enough mana", "{0} more"),
        new(RefusalReason.InvalidTarget, "Can't play that there", "{0}"),
        new(RefusalReason.WaitingForServer, "One moment", "Still placing your last card"),
        new(RefusalReason.EnemyFieldOnly, "Works on their field", "Swipe down first"),
        new(RefusalReason.OwnFieldOnly, "Works on your field", "Swipe up first"),
        new(RefusalReason.NotInHand, "That card isn't in your hand", ""),
        new(RefusalReason.PathBlocked, "Can't build there", "Drop it on an empty spot"),
        new(RefusalReason.SlotTaken, "That spot is taken", "Pick an empty spot"),
        new(RefusalReason.DifferentTower, "Another tower is there", "Drop the same card on it to level it up"),
        new(RefusalReason.TowerMaxLevel, "Already at its top level", "{0}"),
        new(RefusalReason.DeckFull, "Deck is full", "Remove a card first"),
        new(RefusalReason.CardNotOwned, "You don't own this card yet", "Win it in a match"),
        new(RefusalReason.CardAlreadyInDeck, "Already in this deck", ""),
        new(RefusalReason.NotEnoughGold, "Not enough gold", "{0}"),
        new(RefusalReason.NotEnoughCopies, "Not enough copies", "{0}"),
        new(RefusalReason.CardMaxLevel, "Already at max level", ""),
        new(RefusalReason.DeckNotFull, "Deck isn't full", "{0}"),
        new(RefusalReason.MatchmakingFailed, "Couldn't find a match", "Check your connection"),
        new(RefusalReason.JoinFailed, "Couldn't join that room", "Check the code"),
        new(RefusalReason.Cooldown, "Not ready yet", ""),
        new(RefusalReason.NoTeam, "The match hasn't started", ""),
        new(RefusalReason.ConnectionLost, "Couldn't join the match", "Press Battle to try again"),
    };
}
