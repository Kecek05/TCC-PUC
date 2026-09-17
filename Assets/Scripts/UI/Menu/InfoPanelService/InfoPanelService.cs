using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The card detail panel: the card itself, what the player owns of it, its stat table, its description, and
/// the button that buys its next level. Everything is resolved from the <see cref="CardDataSO"/> it is
/// handed plus <see cref="BasePlayerSaveManager"/>, and it redraws off that save's own events — so an
/// upgrade bought here updates the panel that bought it.
/// </summary>
public class InfoPanelCanvas : BaseInfoPanelService
{
    [Title("References")]
    [SerializeField] private GameObject contentObject;
    [SerializeField] private GameObject panelObject;
    [SerializeField] private CanvasGroup panelCanvasGroup;
    [SerializeField] private TextMeshProUGUI title;
    [SerializeField] private TextMeshProUGUI description;
    [SerializeField] private Button closeButton;

    [Tooltip("Optional. The dimmed area behind the panel — tapping it closes, the standard modal gesture. " +
             "Leave empty and only the close button dismisses.")]
    [SerializeField] private Button backgroundButton;

    [Title("Card")]
    [InfoBox("Every reference below is optional: an unwired one is simply not drawn, so the panel keeps " +
             "working while its layout is still being built.")]
    [Tooltip("The card portrait. The same widget the deck page grid uses, with its level pill and button " +
             "stripped out — this panel prints the level itself.")]
    [SerializeField] private SingleCardInDeck cardWidget;

    [SerializeField] private TextMeshProUGUI levelLabel;
    [SerializeField] private TextMeshProUGUI rarityLabel;
    [SerializeField] private TextMeshProUGUI typeLabel;

    [Tooltip("Paints the level and rarity labels in the rarity's own colour, the same table the card " +
             "widget's level pill reads.")]
    [SerializeField] private CardsRarityDataSO cardsRarityData;

    [Title("Upgrade")]
    [SerializeField] private Button upgradeButton;
    [SerializeField] private TextMeshProUGUI upgradeCostLabel;

    [Tooltip("Optional. The coin beside the cost — hidden when the button reads MAX or Locked and there is " +
             "no price to pay.")]
    [SerializeField] private GameObject upgradeCostIcon;

    [Title("Stats")]
    [InfoBox("One StatPrefab is instantiated into StatsParent per stat the card reports, and the grid " +
             "shrinks itself so every row fits however many a card has. Optional: with either reference " +
             "empty the panel simply shows no stat table.")]
    [SerializeField] private RectTransform statsParent;
    [SerializeField] private StatEntryUI statEntryPrefab;

    [Tooltip("Breathing room kept below the stat table when it has to shrink to fit — without it a tall " +
             "card ends flush against the frame. Comes off the height the fit may use but not off the grid's " +
             "own rect, so the gap is the same size whatever the table scales to.")]
    [SerializeField, MinValue(0f)] private float statsBottomMargin = 20f;

    [Title("Pages")]
    [Tooltip("Optional. The stats / description page strip. Put back on the first page every time the panel " +
             "opens, so a card never opens on the page the previous one was left on.")]
    [SerializeField] private ScrollRect contentPages;

    [Title("Labels")]
    [SerializeField] private string levelFormat = "Level {0}";
    [SerializeField] private string lockedLabel = "Locked";
    [SerializeField] private string maxLevelLabel = "MAX";
    [SerializeField] private string towerTypeLabel = "Tower";
    [SerializeField] private string spellTypeLabel = "Spell";
    [SerializeField] private string troopTypeLabel = "Troop";

    [Title("Settings")]
    [SerializeField] private Ease fadeInEase =  Ease.OutBack;
    [SerializeField] private float fadeInDuration = 1f;

    [SerializeField] private Ease bumpEase = Ease.OutBack;
    [SerializeField] private float bumpDuration = 0.35f;
    [SerializeField, Tooltip("Scale the panel pops from on show; it tweens up to 1, and bumpEase's overshoot is the bump.")]
    private float bumpStartScale = 0.8f;

    private Tween fadeInTween;
    private Tween bumpTween;

    /// <summary>Spawned stat rows, kept between shows rather than destroyed — see <see cref="BuildStats"/>.</summary>
    private readonly List<StatEntryUI> statEntries = new();

    /// <summary>The stat grid as it was authored, so a fit always scales from the design rather than from
    /// whatever the last card left behind.</summary>
    private GridLayoutGroup statsGrid;
    private Vector2 _statsArea;
    private Vector2 _statCellSize;
    private Vector2 _statSpacing;

    private BasePlayerSaveManager _playerSave;
    private ScreenWarning _screenWarning;

    /// <summary>The card on show. Null while the panel is hidden, which is what stops a save change that
    /// arrives from elsewhere (a match reward, a debug grant) redrawing a closed panel.</summary>
    private CardDataSO _card;

    private bool _subscribed;

    public override bool IsVisible => contentObject != null && contentObject.activeInHierarchy;

    public override RectTransform UpgradeButtonRect =>
        upgradeButton != null ? (RectTransform)upgradeButton.transform : null;

    /// <summary>The colours the labels were authored with, restored when a rarity has none of its own.</summary>
    private Color _levelLabelColor = Color.white;
    private Color _rarityLabelColor = Color.white;

    private void Awake()
    {
        ServiceLocator.Register<BaseInfoPanelService>(this);

        if (levelLabel != null) _levelLabelColor = levelLabel.color;
        if (rarityLabel != null) _rarityLabelColor = rarityLabel.color;

        CaptureStatGrid();

        InitializeButtons();
        contentObject.SetActive(false);
    }

    private void OnDestroy()
    {
        UnsubscribeFromSave();
        fadeInTween?.Kill();
        bumpTween?.Kill();
        ServiceLocator.Unregister<BaseInfoPanelService>();
    }

    private void InitializeButtons()
    {
        closeButton.onClick.AddListener(() =>
        {
            HideInfoPanel();
        });

        // Tapping the dimmed backdrop dismisses too. A tap on the card itself can never reach this: the
        // Panel's own Background is a raycast target, so it absorbs the click before it falls through.
        if (backgroundButton != null) backgroundButton.onClick.AddListener(HideInfoPanel);

        if (upgradeButton != null) upgradeButton.onClick.AddListener(TryUpgradeShownCard);
    }

    [Button]
    public override void ShowInfoPanel(InfoPanelData infoPanelData)
    {
        SetupPanel(infoPanelData);

        // Only while open: a reward banked from anywhere else must not redraw a panel nobody is looking at.
        SubscribeToSave();

        // Restart cleanly if a previous fade is still running, then fade alpha 0 -> 1.
        fadeInTween?.Kill();
        panelCanvasGroup.alpha = 0f;
        contentObject.SetActive(true);
        ResetContentPages();
        fadeInTween = panelCanvasGroup.DOFade(1f, fadeInDuration)
            .SetEase(fadeInEase)
            .SetUpdate(true) // unscaled time: still fades if the game is paused (timeScale 0)
            .OnKill(() => fadeInTween = null); // fires on manual kill AND auto-kill, so the ref never dangles into a recycled tween

        // Bump only the panel (not the dimmed background): pop its scale from bumpStartScale up to 1.
        bumpTween?.Kill();
        panelObject.transform.localScale = Vector3.one * bumpStartScale;
        bumpTween = panelObject.transform.DOScale(1f, bumpDuration)
            .SetEase(bumpEase)
            .SetUpdate(true)
            .OnKill(() => bumpTween = null);

        TriggerOnInfoPanelShow();
    }

    [Button]
    public override void HideInfoPanel()
    {
        // No tween on hide — and if the show animations are mid-flight, stop them so they can't keep running.
        fadeInTween?.Kill();
        bumpTween?.Kill();
        contentObject.SetActive(false);

        UnsubscribeFromSave();
        _card = null;

        TriggerOnInfoPanelHide();
    }

    /// <summary>
    /// Everything that depends only on the card, not on what the player owns of it. The rest is left to
    /// <see cref="RefreshProgression"/>, which runs again on every save change while the panel is open.
    /// </summary>
    private void SetupPanel(InfoPanelData infoPanelData)
    {
        _card = infoPanelData.Card;

        if (_card == null)
        {
            GameLog.Warn($"[{nameof(InfoPanelCanvas)}] Asked to show an info panel with no card.", this);
            return;
        }

        title.text = _card.CardName;
        description.text = _card.Description;

        cardWidget?.Initialize(_card);

        if (rarityLabel != null)
        {
            rarityLabel.text = _card.Rarity.ToString();
            rarityLabel.color = TryGetRarityColor(out Color rarityColor) ? rarityColor : _rarityLabelColor;
        }

        if (typeLabel != null) typeLabel.text = GetTypeLabel(_card.ExistingType);

        RefreshProgression();
    }

    /// <summary>
    /// Everything the player's save decides: the level, the copies collected toward the next one, what that
    /// next one costs, and the stat table resolved to the level actually owned.
    /// </summary>
    private void RefreshProgression()
    {
        if (_card == null) return;

        CardType cardType = _card.CardType;
        BasePlayerSaveManager save = PlayerSave;

        bool owned = save != null && save.IsCardOwned(cardType);
        int level = save != null ? save.GetCardLevel(cardType) : 0;

        if (levelLabel != null)
        {
            levelLabel.text = owned ? string.Format(levelFormat, level) : lockedLabel;
            levelLabel.color = TryGetRarityColor(out Color rarityColor) ? rarityColor : _levelLabelColor;
        }

        cardWidget?.SetProgression(
            level,
            save?.GetCardProgress(cardType)?.Copies ?? 0,
            save != null ? save.GetCopiesRequired(cardType) : 0,
            owned);

        RefreshUpgradeButton();
        BuildStats(save?.GetCardStatProgress(cardType));
    }

    /// <summary>Shows what the next level costs, or why there is nothing to buy.</summary>
    private void RefreshUpgradeButton()
    {
        if (upgradeButton == null) return;

        CardUpgradeValidation upgrade = GetUpgradeState();

        // Missing copies or gold stays clickable on purpose: tapping is how the player finds out which.
        bool hasPrice = upgrade.Reason != CardUpgradeInvalidReason.MaxLevel &&
                        upgrade.Reason != CardUpgradeInvalidReason.NotOwned;

        upgradeButton.interactable = hasPrice;

        if (upgradeCostIcon != null) upgradeCostIcon.SetActive(hasPrice);

        if (upgradeCostLabel == null) return;

        upgradeCostLabel.text = upgrade.Reason switch
        {
            CardUpgradeInvalidReason.MaxLevel => maxLevelLabel,
            CardUpgradeInvalidReason.NotOwned => lockedLabel,
            _ => upgrade.GoldCost.ToString()
        };
    }

    /// <summary>
    /// Buys the next level of the card on show. The rules live in the save manager, so this only turns a
    /// refusal into player-facing feedback — a successful upgrade comes back through
    /// <see cref="BasePlayerSaveManager.OnCardProgressChanged"/> and redraws the panel.
    /// </summary>
    private void TryUpgradeShownCard()
    {
        if (_card == null || PlayerSave == null) return;

        CardUpgradeValidation upgrade = PlayerSave.CanUpgradeCard(_card.CardType);

        if (!upgrade)
        {
            ShowWarning(upgrade.WarningMessage);
            return;
        }

        PlayerSave.TryUpgradeCard(_card.CardType);
    }

    private CardUpgradeValidation GetUpgradeState()
    {
        if (_card == null || PlayerSave == null)
            return CardUpgradeValidation.Invalid(CardUpgradeInvalidReason.NotOwned);

        return PlayerSave.CanUpgradeCard(_card.CardType);
    }

    /// <summary>
    /// Remembers the stat grid as authored: its cell, its spacing, and the area on screen it is meant to
    /// fill. <see cref="FitStatGrid"/> scales from these, never from the current values, so shrinking for a
    /// ten-row card and then growing back for a two-row one both land exactly on the design.
    /// </summary>
    private void CaptureStatGrid()
    {
        if (statsParent == null) return;

        statsGrid = statsParent.GetComponent<GridLayoutGroup>();
        if (statsGrid == null) return;

        _statCellSize = statsGrid.cellSize;
        _statSpacing = statsGrid.spacing;

        // The rendered size, not the local one: should a fit ever be saved into the scene (running the
        // ShowInfoPanel button in the editor would do it), rect * scale still recovers the area the grid is
        // meant to fill rather than compounding the shrink on every load.
        _statsArea = Vector2.Scale(statsParent.rect.size, statsParent.localScale);
    }

    /// <summary>
    /// Shrinks the whole grid — cells, spacing and text alike — until every row of <paramref name="count"/>
    /// stats fits the area the panel gives it. Anel reports 10 stats and Circle 9, which need a fifth row
    /// the authored grid has no height for.
    /// </summary>
    /// <remarks>
    /// Done by scaling <c>StatsParent</c> down and widening its rect by the same factor, rather than by
    /// shrinking <c>cellSize</c>: StatPrefab's three labels are anchored to its top-left corner at a fixed
    /// font size, so a shorter cell would clip them instead of fitting them. Scaling the parent keeps every
    /// row pixel-identical to the design, only smaller.
    /// <para>Never scales above 1 — a two-row card blown up to fill the panel would read as a different
    /// widget from the eight-row card beside it.</para>
    /// </remarks>
    private void FitStatGrid(int count)
    {
        if (statsGrid == null || _statsArea.x <= 0f || _statsArea.y <= 0f) return;

        int columns = StatColumns;
        int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)columns));

        float neededWidth = columns * _statCellSize.x + (columns - 1) * _statSpacing.x + statsGrid.padding.horizontal;
        float neededHeight = rows * _statCellSize.y + (rows - 1) * _statSpacing.y + statsGrid.padding.vertical;

        // The margin comes off the height the fit may use, but NOT off the rect below, so the table simply
        // stops short of the frame instead of ending flush against it. Kept out of the rect on purpose: a
        // margin that scaled with the grid would shrink exactly when the grid is tallest and needs it most.
        float usableHeight = Mathf.Max(1f, _statsArea.y - statsBottomMargin);

        float scale = Mathf.Min(1f, _statsArea.x / neededWidth, usableHeight / neededHeight);

        statsParent.localScale = new Vector3(scale, scale, 1f);
        statsParent.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _statsArea.x / scale);
        statsParent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _statsArea.y / scale);
    }

    /// <summary>
    /// How many columns the grid lays out. A fixed constraint is honoured; Flexible cannot be, because it
    /// would re-derive the count from the rect <see cref="FitStatGrid"/> just widened and change the row
    /// count under the maths that widened it — so it is resolved once, the way Flexible itself would have,
    /// against the authored area.
    /// </summary>
    private int StatColumns
    {
        get
        {
            if (statsGrid.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
                return Mathf.Max(1, statsGrid.constraintCount);

            float stride = _statCellSize.x + _statSpacing.x;
            if (stride <= 0f) return 1;

            float usable = _statsArea.x - statsGrid.padding.horizontal + _statSpacing.x;
            return Mathf.Max(1, Mathf.FloorToInt(usable / stride));
        }
    }

    /// <summary>
    /// Puts the page strip back on the first page. The panel is hidden rather than destroyed, so the scroll
    /// keeps whatever offset the last card was left on - and a card opened from there would show its
    /// description with its stat table already swiped off screen. Reset after the content is activated, so
    /// the ScrollRect measures against a laid-out viewport rather than a stale one.
    /// </summary>
    private void ResetContentPages()
    {
        if (contentPages == null) return;

        contentPages.StopMovement(); // drop the inertia left over from the last swipe, or it scrolls straight back off
        contentPages.horizontalNormalizedPosition = 0f;
    }

    /// <summary>
    /// Fills the stat grid, one row per stat. Rows are reused and deactivated rather than destroyed and
    /// re-instantiated: this panel opens on every card tap, cards differ by only a row or two, and a
    /// GridLayoutGroup skips inactive children, so a surplus row leaves no hole in the layout.
    /// </summary>
    private void BuildStats(IReadOnlyList<CardStatProgress> stats)
    {
        if (statsParent == null || statEntryPrefab == null) return;

        int count = stats?.Count ?? 0;

        FitStatGrid(count);

        while (statEntries.Count < count)
            statEntries.Add(Instantiate(statEntryPrefab, statsParent));

        for (int i = 0; i < statEntries.Count; i++)
        {
            bool used = i < count;

            statEntries[i].gameObject.SetActive(used);
            if (used) statEntries[i].SetStat(stats[i]);
        }
    }

    private string GetTypeLabel(ExistingTypesOfCard type) => type switch
    {
        ExistingTypesOfCard.Tower => towerTypeLabel,
        ExistingTypesOfCard.Spell => spellTypeLabel,
        ExistingTypesOfCard.Enemy => troopTypeLabel,
        _ => string.Empty
    };

    /// <summary>
    /// The rarity's own colour, or false when it has none to give. CardRarityType.None is fully transparent
    /// in CardsRarityData and is where a freshly authored card starts, so painting a label with it would
    /// make that label disappear — the authored colour is kept instead.
    /// </summary>
    private bool TryGetRarityColor(out Color color)
    {
        color = Color.white;
        if (cardsRarityData == null || _card == null) return false;

        RarityData rarity = cardsRarityData.Get(_card.Rarity);
        if (rarity.mainColor.a <= 0f) return false;

        color = rarity.mainColor;
        return true;
    }

    /// <summary>The save is registered from a persistent scene, but resolve late anyway so the panel does
    /// not care which scene built it first.</summary>
    private BasePlayerSaveManager PlayerSave
    {
        get
        {
            if (_playerSave == null) ServiceLocator.TryGet(out _playerSave);
            return _playerSave;
        }
    }

    private void ShowWarning(string message)
    {
        if (_screenWarning == null) ServiceLocator.TryGet(out _screenWarning);

        if (_screenWarning != null) _screenWarning.ShowWarning(message);
        else GameLog.Warn($"[{nameof(InfoPanelCanvas)}] {message}", this);
    }

    private void SubscribeToSave()
    {
        if (_subscribed || PlayerSave == null) return;

        _playerSave.OnCardProgressChanged += HandleCardProgressChanged;
        _subscribed = true;
    }

    private void UnsubscribeFromSave()
    {
        if (!_subscribed) return;

        // Detach from the instance that was subscribed to, never through the property: it would resolve a
        // fresh one if the old registration went away and leave the real handler attached.
        if (_playerSave != null) _playerSave.OnCardProgressChanged -= HandleCardProgressChanged;
        _subscribed = false;
    }

    private void HandleCardProgressChanged(CardType cardType)
    {
        if (_card != null && cardType == _card.CardType) RefreshProgression();
    }
}
