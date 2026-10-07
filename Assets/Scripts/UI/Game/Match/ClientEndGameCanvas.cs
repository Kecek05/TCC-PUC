using System;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class PlayerEndGameCanvasData
{
    [SerializeField] private TextMeshProUGUI playerUsername;
    [SerializeField] private TextMeshProUGUI playerHealth;
    [SerializeField] private TextMeshProUGUI playerWave;
    [SerializeField] private Image waveProgress;

    public void ChangePlayerHealthText(float newHealth)
    {
        playerHealth.text = $"{newHealth}";
    }

    public void ChangePlayerWaveText(int newWave)
    {
        playerWave.text = $"{newWave}";
    }

    public void ChangeWaveProgress(float newProgress)
    {
        waveProgress.fillAmount = newProgress;
    }
}

public class ClientEndGameCanvas : MonoBehaviour
{
    [Title("References")]
    [SerializeField] private GameObject rootCanvas;
    [SerializeField] private PlayerEndGameCanvasData localPlayerData;
    [SerializeField] private PlayerEndGameCanvasData enemyPlayerData;
    [SerializeField] private GameObject victoryLabel;
    [SerializeField] private GameObject defeatLabel;
    [SerializeField] private Button okButton;
    [SerializeField] private Button playAgainButton;

    [Title("Reward")]
    [InfoBox("One RewardEntryUI is instantiated into the area per line of the payout — gold is one tile, the " +
             "card another. Optional: with no area or prefab assigned the end screen simply shows no tiles.")]
    [SerializeField] private Transform rewardsArea;
    [SerializeField] private RewardEntryUI rewardEntryPrefab;
    [SerializeField] private Sprite coinSprite;
    [SerializeField] private CardDataListSO cardDataListSO;

    [Tooltip("Optional. Colours a won card's burst by its rarity. Empty: the attention colour.")]
    [SerializeField] private CardsRarityDataSO cardsRarityData;

    [Tooltip("Optional. The deck page's card widget: a won card turns over into it, drawn exactly as it will " +
             "look in the collection. Empty: the card's art.")]
    [SerializeField] private SingleCardInDeck cardWidgetPrefab;

    [Title("Reveal Labels")]
    [SerializeField] private string rewardsTitle = "Rewards";
    [SerializeField] private string skipHint = "Tap anywhere to skip to the end";

    /// <summary>Cards owned when the match began: a reward card missing from here is the player's first copy.</summary>
    private readonly HashSet<CardType> _ownedAtStart = new();

    private readonly List<RewardEntryUI> _tiles = new();
    private Sequence _intro;
    private Sequence _reveal;
    private bool _introDone;
    private bool _rewardPending;
    private Reward _reward;
    private Button _skip;
    private TextMeshProUGUI _rewardsLabel;
    private TextMeshProUGUI _skipLabel;
    private bool _destroyed;

    private BaseServerEndGameManager _endGameManager;
    private BaseRewardService _rewardService;
    private BaseTeamManager _teamManager;
    private BaseClientManager _clientManager;

    private void Awake()
    {
        rootCanvas.SetActive(false);

        okButton.onClick.AddListener(OnOkButtonClicked);
        playAgainButton.onClick.AddListener(OnPlayAgainButtonClicked);
    }

    private void Start()
    {
        _teamManager = ServiceLocator.Get<BaseTeamManager>();
        _endGameManager = ServiceLocator.Get<BaseServerEndGameManager>();
        _clientManager = ServiceLocator.Get<BaseClientManager>();

        _endGameManager.OnGameEnded += EndGameManager_OnGameEnded;

        // Display only, and deliberately hung off the reward service rather than the end-game Rpc: the
        // service raises after the save is written, so the panel can never show a payout that failed to bank.
        // Absent in debug scenes that have no ClientManager, hence TryGet.
        if (ServiceLocator.TryGet(out _rewardService))
            _rewardService.OnRewardGranted += RewardService_OnRewardGranted;

        // The save cannot change mid-match except through this match's own reward, so ownership now is
        // ownership before the payout — which is what tells a NEW! card from one more copy.
        if (cardDataListSO != null && ServiceLocator.TryGet(out BasePlayerSaveManager save))
        {
            foreach (CardDataSO card in cardDataListSO.CardDataList)
                if (card != null && save.IsCardOwned(card.CardType)) _ownedAtStart.Add(card.CardType);
        }
    }

    private void OnDestroy()
    {
        if (_endGameManager != null) _endGameManager.OnGameEnded -= EndGameManager_OnGameEnded;
        if (_rewardService != null) _rewardService.OnRewardGranted -= RewardService_OnRewardGranted;

        okButton.onClick.RemoveListener(OnOkButtonClicked);
        playAgainButton.onClick.RemoveListener(OnPlayAgainButtonClicked);

        // Killing the intro would start the reveal from its OnKill; nothing may be built while tearing down.
        _destroyed = true;
        _intro?.Kill();
        _reveal?.Kill();
    }

    private void EndGameManager_OnGameEnded(EndGameSnapshot endgameSnapshot)
    {
        SetupEndGameUI(endgameSnapshot);

        rootCanvas.SetActive(true);
        PlayIntro(endgameSnapshot.WinnerTeam == _teamManager.GetLocalTeam());
    }

    /// <summary>
    /// MatchEnd: the screen fades up, VICTORY! stamps down in a burst of gold (fanfare, Success) or DEFEAT! falls
    /// and lands heavily (Failure); the two players' results rise in after it, then the buttons pop. The payout
    /// reveal follows. One tap anywhere finishes all of it.
    /// </summary>
    private void PlayIntro(bool localWon)
    {
        JuiceTokensSO t = Juice.Tokens;
        _intro?.Kill();
        _introDone = false;
        _intro = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        ShowSkip(true);

        Transform root = rootCanvas.transform;
        CanvasGroup background = root.Find("Background") != null ? root.Find("Background").GetComponent<CanvasGroup>() : null;
        if (background != null)
        {
            background.alpha = 0f;
            _intro.Join(background.DOFade(1f, t.Durations.Panel).SetEase(t.Eases.Out));
        }

        RectTransform label = (RectTransform)(localWon ? victoryLabel : defeatLabel).transform;
        CanvasGroup labelGroup = GroupOf(label);
        labelGroup.alpha = 0f;

        if (localWon)
        {
            label.localScale = Vector3.one * 2.2f;
            _intro.Insert(0.15f, labelGroup.DOFade(1f, t.Durations.Select));
            _intro.Insert(0.15f, label.DOScale(1f, t.Durations.Pop).SetEase(t.Eases.Pop));
            _intro.InsertCallback(0.15f + t.Durations.Pop * 0.5f, () =>
            {
                Juice.Sfx(SfxId.Fanfare);
                Juice.Haptic(HapticRole.Success);
                // The label spans the screen, so a burst sized off it is already big: no extra intensity.
                RarityBurst.Play(label, t.Colors.Gold, 0.85f, behind: true);
                JuiceCamera.Shake(t.Shake.Small, t.Durations.ShakeLong);
            });
        }
        else
        {
            Vector2 rest = label.anchoredPosition;
            label.anchoredPosition = rest + Vector2.up * 220f;
            _intro.Insert(0.15f, labelGroup.DOFade(1f, t.Durations.Select));
            _intro.Insert(0.15f, label.DOAnchorPos(rest, t.Durations.Fall * 1.6f).SetEase(t.Eases.Fall));
            _intro.InsertCallback(0.15f + t.Durations.Fall * 1.6f, () =>
            {
                Juice.Sfx(SfxId.Defeat);
                Juice.Haptic(HapticRole.Failure);
                JuiceFx.Land(label, Vector3.one, unscaled: true);
                JuiceCamera.Shake(t.Shake.Medium, t.Durations.ShakeLong);
            });
        }

        // The players' results rise in, then the buttons pop.
        float at = 0.15f + t.Durations.Pop;
        foreach (string row in new[] { "EnemyPlayer", "LocalPlayer" })
        {
            Transform target = label.parent != null && label.parent.parent != null ? label.parent.parent.Find(row) : null;
            if (target == null) continue;

            RectTransform rect = (RectTransform)target;
            CanvasGroup group = GroupOf(rect);
            Vector2 rest = rect.anchoredPosition;
            group.alpha = 0f;
            rect.anchoredPosition = rest + Vector2.down * t.Distances.RiseBanner;
            _intro.Insert(at, group.DOFade(1f, t.Durations.Pop));
            _intro.Insert(at, rect.DOAnchorPos(rest, t.Durations.Pop).SetEase(t.Eases.Out));
            at += t.Durations.StaggerReveal;
        }

        foreach (Button button in new[] { playAgainButton, okButton })
        {
            if (button == null || !button.gameObject.activeInHierarchy) continue;
            Transform b = button.transform;
            b.localScale = Vector3.zero;
            _intro.Insert(at, b.DOScale(1f, t.Durations.Pop).SetEase(t.Eases.Pop));
            at += t.Durations.Stagger * 2f;
        }

        _intro.OnKill(() =>
        {
            labelGroup.alpha = 1f;
            label.localScale = Vector3.one;
            if (background != null) background.alpha = 1f;
            foreach (Button button in new[] { playAgainButton, okButton })
                if (button != null) button.transform.localScale = Vector3.one;

            _introDone = true;
            _intro = null;
            if (_destroyed || this == null) return;
            if (_rewardPending) PlayReveal();
            else if (_reveal == null) ShowSkip(false);
        });
    }

    /// <summary>The payout, tile by tile, stagger-reveal apart. Starts once the end screen has arrived.</summary>
    private void PlayReveal()
    {
        _rewardPending = false;
        _reveal?.Kill();
        if (_tiles.Count == 0)
        {
            ShowSkip(false);
            return;
        }

        JuiceTokensSO t = Juice.Tokens;
        _reveal = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        ShowSkip(true);

        float at = 0f;
        // "Rewards" rises in over the row before the first tile drops.
        TextMeshProUGUI title = RevealLabel(ref _rewardsLabel, "RewardsTitle", rewardsTitle, 40f, true);
        if (title != null)
        {
            CanvasGroup titleGroup = GroupOf(title);
            titleGroup.alpha = 0f;
            _reveal.Insert(0f, titleGroup.DOFade(1f, t.Durations.Pop));
            at += t.Durations.Select;
        }

        for (int i = 0; i < _tiles.Count; i++)
        {
            RewardEntryUI tile = _tiles[i];
            bool isCard = i == _tiles.Count - 1 && _reward.HasCard;
            float intensity = 1f;

            if (isCard)
            {
                CardDataSO card = cardDataListSO != null ? cardDataListSO.GetCardDataByType(_reward.Card) : null;
                intensity = RarityBurst.IntensityOf(card != null ? card.Rarity : CardRarityType.Common);
            }

            Sequence one = tile.Reveal(i, isCard && !_ownedAtStart.Contains(_reward.Card), intensity);
            _reveal.Insert(at, one);
            at += one.Duration() * 0.55f + t.Durations.StaggerReveal;
        }

        _reveal.OnKill(() =>
        {
            _reveal = null;
            if (_destroyed || this == null) return;
            if (_rewardsLabel != null) GroupOf(_rewardsLabel).alpha = 1f;
            ShowSkip(false);
        });
    }

    /// <summary>The colour a card's name, burst and flash take: its rarity's, or attention for a colourless one.</summary>
    private Color RarityColor(CardDataSO card)
    {
        Color color = Juice.Tokens.Colors.Attention;
        if (card == null || cardsRarityData == null) return color;

        Color rarity = cardsRarityData.Get(card.Rarity).mainColor;
        return rarity.a > 0f ? rarity : color;
    }

    /// <summary>
    /// A line of text about the reveal, built once beside the rewards row in the result labels' font: the title over
    /// the tiles, or the skip hint under them. Kept out of the row itself, which is cleared on every grant.
    /// </summary>
    private TextMeshProUGUI RevealLabel(ref TextMeshProUGUI label, string objectName, string text, float size, bool above)
    {
        if (label != null) return label;
        if (rewardsArea == null || !(rewardsArea is RectTransform area) || area.parent == null) return null;

        GameObject go = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(area.parent, false);
        rect.SetSiblingIndex(area.GetSiblingIndex() + 1);

        // Over the row: from the area's middle, half a tile up. Under it: from the area's bottom edge.
        float cell = area.TryGetComponent(out GridLayoutGroup grid) ? grid.cellSize.y : 250f;
        float y = above ? (area.anchorMin.y + area.anchorMax.y) * 0.5f : area.anchorMin.y;
        rect.anchorMin = new Vector2(0f, y);
        rect.anchorMax = new Vector2(1f, y);
        rect.pivot = new Vector2(0.5f, above ? 0f : 1f);
        rect.sizeDelta = new Vector2(0f, size * 1.4f);
        rect.anchoredPosition = new Vector2(0f, above ? cell * 0.5f + 18f : -6f);

        label = go.AddComponent<TextMeshProUGUI>();
        TMP_Text style = victoryLabel != null ? victoryLabel.GetComponent<TMP_Text>() : null;
        if (style != null)
        {
            label.font = style.font;
            label.fontSharedMaterial = style.fontSharedMaterial;
        }

        label.text = text;
        label.fontSize = size;
        label.color = Juice.Tokens.Colors.InkMuted;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }

    /// <summary>
    /// One tap anywhere finishes the intro and the reveal at once: the end screen never makes the player wait
    /// to leave. The catcher sits over everything while either plays, so that first tap cannot also press OK.
    /// </summary>
    private void ShowSkip(bool show)
    {
        if (_skip == null)
        {
            GameObject go = new GameObject("SkipReveal", typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(rootCanvas.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = Color.clear;

            _skip = go.GetComponent<Button>();
            _skip.transition = Selectable.Transition.None;
            _skip.onClick.AddListener(() =>
            {
                _intro?.Complete(true);
                _reveal?.Complete(true);
            });
        }

        _skip.transform.SetAsLastSibling();
        _skip.gameObject.SetActive(show);

        // Say that the wait can be skipped, under the row, for as long as it can be.
        TextMeshProUGUI hint = RevealLabel(ref _skipLabel, "SkipHint", skipHint, 28f, false);
        if (hint != null) hint.gameObject.SetActive(show);
    }

    private static CanvasGroup GroupOf(Component c)
    {
        CanvasGroup g = c.GetComponent<CanvasGroup>();
        return g != null ? g : c.gameObject.AddComponent<CanvasGroup>();
    }

    /// <summary>
    /// The reward is banked on its own targeted Rpc, which can land before or after the snapshot, so this
    /// only touches the reward widgets and never the win/lose layout.
    /// </summary>
    private void RewardService_OnRewardGranted(Reward reward)
    {
        if (rewardsArea == null || rewardEntryPrefab == null) return;

        // A grant is one whole payout, so rebuild rather than append: a second grant (the debug commands do
        // exactly this) must replace the tiles, not stack a second row on top of them.
        _reveal?.Kill();
        ClearRewardsArea();
        _tiles.Clear();
        _reward = reward;

        if (reward.Gold > 0)
        {
            RewardEntryUI gold = Instantiate(rewardEntryPrefab, rewardsArea);
            gold.SetGold(reward.Gold, coinSprite);
            _tiles.Add(gold);
        }

        if (reward.HasCard)
        {
            CardDataSO cardData = cardDataListSO != null ? cardDataListSO.GetCardDataByType(reward.Card) : null;
            RewardEntryUI card = Instantiate(rewardEntryPrefab, rewardsArea);
            card.SetCard(cardData, reward.Copies, cardWidgetPrefab, RarityColor(cardData));
            _tiles.Add(card);
        }

        // The tiles wait for their beat: after the end screen has arrived, or now if it already has.
        foreach (RewardEntryUI tile in _tiles) tile.HideForReveal();
        _rewardPending = true;
        if (rootCanvas.activeInHierarchy && _introDone) PlayReveal();
    }

    private void ClearRewardsArea()
    {
        for (int i = rewardsArea.childCount - 1; i >= 0; i--)
            Destroy(rewardsArea.GetChild(i).gameObject);
    }

    private void SetupEndGameUI(EndGameSnapshot endgameSnapshot)
    {
        SetupLabelsData(endgameSnapshot);

        bool localWon = endgameSnapshot.WinnerTeam == _teamManager.GetLocalTeam();
        victoryLabel.SetActive(localWon);
        defeatLabel.SetActive(!localWon);
    }

    private void SetupLabelsData(EndGameSnapshot endgameSnapshot)
    {
        //TODO: Setup Usernames

        TeamType localTeam = _teamManager.GetLocalTeam();

        if (localTeam == TeamType.None)
        {
            GameLog.Warn("ClientEndGameCanvas: Local team is None. Cannot setup end game UI data.");
            return;
        }

        if (localTeam == TeamType.Blue)
        {
            localPlayerData.ChangePlayerHealthText(endgameSnapshot.BluePlayer.Health);
            localPlayerData.ChangePlayerWaveText(endgameSnapshot.BluePlayer.Wave);
            localPlayerData.ChangeWaveProgress(endgameSnapshot.BluePlayer.WaveProgress);

            enemyPlayerData.ChangePlayerHealthText(endgameSnapshot.RedPlayer.Health);
            enemyPlayerData.ChangePlayerWaveText(endgameSnapshot.RedPlayer.Wave);
            enemyPlayerData.ChangeWaveProgress(endgameSnapshot.RedPlayer.WaveProgress);
        }
        else
        {
            localPlayerData.ChangePlayerHealthText(endgameSnapshot.RedPlayer.Health);
            localPlayerData.ChangePlayerWaveText(endgameSnapshot.RedPlayer.Wave);
            localPlayerData.ChangeWaveProgress(endgameSnapshot.RedPlayer.WaveProgress);

            enemyPlayerData.ChangePlayerHealthText(endgameSnapshot.BluePlayer.Health);
            enemyPlayerData.ChangePlayerWaveText(endgameSnapshot.BluePlayer.Wave);
            enemyPlayerData.ChangeWaveProgress(endgameSnapshot.BluePlayer.WaveProgress);
        }
    }

    private void OnOkButtonClicked() => LeaveMatch();

    // Play Again behaves like OK: tear down the match and return to the menu,
    // where the player re-hosts or re-joins (no matchmaking for a 1-click rematch).
    private void OnPlayAgainButtonClicked() => LeaveMatch();

    private async void LeaveMatch()
    {
        // Guard against double-press while the async teardown is running.
        okButton.interactable = false;
        playAgainButton.interactable = false;

        await _clientManager.LeaveMatchAsync();
    }
}
