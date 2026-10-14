using System.Collections;
using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UI.Game.Shapes.ImmediateComponents;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class ManaCanvas : MonoBehaviour
{
    [Title("References")]
    [SerializeField] private RectangleImmediateUI manaFill;
    [SerializeField] private RectangleImmediateUI manaBackgroundFill;
    [SerializeField] private TextMeshProUGUI manaText;

    [Title("ManaBar: full")]
    [InfoBox("A full bar is wasting regen, so it says so: a MAX chip pops beside the counter and stays until the " +
             "player spends. Built in code, so the scenes need no new objects. The frame itself does not animate: " +
             "a breathing glow on it was tried and removed at the user's request.")]
    [Tooltip("Where the MAX chip sits from the counter's right edge, in canvas units: beside it and raised, " +
             "clear of the first card's face.")]
    [SerializeField] private Vector2 maxChipOffset = new(8f, 40f);

    [Title("ManaBar: spend")]
    [Tooltip("Where the \"-N\" starts from the counter's centre, in canvas units.")]
    [SerializeField] private Vector2 spendFloatOffset = new(104f, 24f);
    [Tooltip("How far the \"-N\" rises before it is gone, in canvas units.")]
    [SerializeField] private float spendFloatRise = 48f;

    private BaseTeamManager _teamManager;
    private BaseServerManaManager _serverManaManager;
    private NetworkVariable<float> _maxMana;
    private BaseClientManaManager _clientManaManager;

    //Bar
    private readonly float _barGap = 0.15f;
    private readonly float _iconWidth = 1f;

    private float _filled;
    private float _fraction;
    private float _fillPos;
    private float _fillTotal;
    private float _barFill;

    private bool _initialized = false;

    // ManaBar beats: a whole point gained, the bar filling up, the ceiling rising.
    private int _shownWhole = -1;
    private bool _wasFull;
    private float _maxAtStart = -1f;

    // ManaBar spend: the fill is drawn from a displayed value that eases down to the predicted one, so a spend
    // reads as a drain instead of a jump. Regen is followed exactly; only a drop is eased.
    private const float SpendThreshold = 0.5f;
    private float _shownMana = -1f;
    private float _lastPredicted = -1f;
    private bool _draining;
    private float _drainFrom;
    private float _drainStart;

    // ManaBar full: built on first use.
    private RectTransform _maxChip;
    private CanvasGroup _maxChipGroup;
    private Tween _maxChipTween;

    private void Start()
    {
        _teamManager = ServiceLocator.Get<BaseTeamManager>();
        _serverManaManager = ServiceLocator.Get<BaseServerManaManager>();
        _clientManaManager =  ServiceLocator.Get<BaseClientManaManager>();

        StartCoroutine(WaitForInitialization());
    }

    private void OnDestroy()
    {
        if (_maxMana != null)
            _maxMana.OnValueChanged -= OnServerMaxManaChanged;

        _maxChipTween?.Kill();
    }

    private IEnumerator WaitForInitialization()
    {
        _teamManager = ServiceLocator.Get<BaseTeamManager>();
        _serverManaManager = ServiceLocator.Get<BaseServerManaManager>();

        yield return new WaitUntil(() =>
            _teamManager != null &&
            _teamManager.HasLocalTeamBeenAssigned() &&
            _serverManaManager != null);

        _maxMana = _serverManaManager.GetMaxManaNetworkVariable(_teamManager.GetLocalTeam());

        _maxMana.OnValueChanged += OnServerMaxManaChanged;
        _maxAtStart = _maxMana.Value;

        _initialized = true;
    }

    private void Update()
    {
        if (!_initialized) return;
        if (_clientManaManager == null) return;

        float predicted = _clientManaManager.PredictedMana;
        float shown = ResolveShownMana(predicted);

        _filled = Mathf.Floor(shown);
        _fraction = shown - _filled;

        _fillPos = _filled * (_iconWidth + _barGap) + _fraction * _iconWidth;
        _fillTotal = _maxMana.Value * (_iconWidth + _barGap);
        _barFill = 1f - (_fillPos / _fillTotal);

        manaFill.SetDashFill(_barFill);

        // The counter states the truth at once; only the fill takes dur-select to catch up with a spend.
        int whole = Mathf.FloorToInt(predicted);
        manaText.text = $"{whole}";

        PlayManaBeats(whole);
    }

    /// <summary>
    /// The mana the fill is drawn at. It follows the predicted value exactly while mana rises, and eases down to
    /// it over dur-select on ease-out when a card is paid for, which is also the moment the spend's own beat
    /// plays: "-N" off the counter and a soft punch.
    /// </summary>
    private float ResolveShownMana(float predicted)
    {
        if (_shownMana < 0f)
        {
            _shownMana = _lastPredicted = predicted;
            return predicted;
        }

        float dropped = _lastPredicted - predicted;
        _lastPredicted = predicted;

        if (dropped >= SpendThreshold)
        {
            _draining = true;
            _drainFrom = _shownMana;
            _drainStart = Time.unscaledTime;
            PlaySpend(Mathf.RoundToInt(dropped));
        }

        if (!_draining)
        {
            _shownMana = predicted;
            return predicted;
        }

        float duration = Mathf.Max(0.01f, Juice.Tokens.Durations.Select);
        float t = (Time.unscaledTime - _drainStart) / duration;
        if (t >= 1f)
        {
            _draining = false;
            _shownMana = predicted;
            return predicted;
        }

        _shownMana = Mathf.Lerp(_drainFrom, predicted, DOVirtual.EasedValue(0f, 1f, t, Juice.Tokens.Eases.Out));
        return _shownMana;
    }

    /// <summary>
    /// A point that regenerates punches the counter with a mana tick a step higher per point; reaching the
    /// ceiling chimes. Only single-point gains count: a refund or the tutorial's top-up jumps several at once
    /// and stays quiet, so the bar never chatters. Being full is a state of its own, however it was reached.
    /// </summary>
    private void PlayManaBeats(int whole)
    {
        bool full = _maxMana != null && _maxMana.Value > 0f && whole >= Mathf.FloorToInt(_maxMana.Value);

        if (_shownWhole >= 0 && whole == _shownWhole + 1)
        {
            JuiceFx.Punch(manaText.transform, Juice.Tokens.Scales.PunchSoft, unscaled: false);
            if (full && !_wasFull) Juice.Sfx(SfxId.Full);
            else Juice.Sfx(SfxId.Mana, Mathf.Min(whole, 10), 0.7f);
        }

        // Not on the first read: a bar that starts full has nothing to announce.
        if (_shownWhole >= 0 && full != _wasFull) ShowFull(full);

        _shownWhole = whole;
        _wasFull = full;
    }

    /// <summary>The spend's beat: "-N" rises off the counter in the mana-gain colour, and the counter gives a soft punch.</summary>
    private void PlaySpend(int spent)
    {
        if (spent <= 0) return;

        JuiceTokensSO t = Juice.Tokens;
        JuiceFx.Punch(manaText.transform, t.Scales.PunchSoft, unscaled: false);

        RectTransform counter = manaText.rectTransform.parent as RectTransform;
        if (counter == null) return;

        GameObject go = new GameObject("Spend", typeof(RectTransform));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(counter, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(200f, 80f);
        rect.anchoredPosition = spendFloatOffset;

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.font = manaText.font;
        text.fontSharedMaterial = manaText.fontSharedMaterial;
        text.fontSize = manaText.fontSize * 0.8f;
        text.enableAutoSizing = false;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.text = $"-{spent}";
        text.color = t.Colors.ManaGain;

        float life = t.Durations.Banner * 0.4f;
        float rise = Juice.ReduceMotion ? spendFloatRise * 0.5f : spendFloatRise;
        DOTween.Sequence().SetUpdate(true).SetLink(go)
            .Append(rect.DOAnchorPosY(spendFloatOffset.y + rise, life).SetEase(t.Eases.Out))
            .Insert(life * 0.55f, text.DOFade(0f, life * 0.45f).SetEase(t.Eases.In))
            .OnComplete(() => Destroy(go));
    }

    /// <summary>
    /// Full: the MAX chip pops in beside the counter. Not full any more: it fades over dur-select.
    /// </summary>
    private void ShowFull(bool full)
    {
        JuiceTokensSO t = Juice.Tokens;
        BuildMaxChip();

        _maxChipTween?.Kill();
        if (_maxChip == null) return;

        if (!full)
        {
            _maxChipTween = _maxChipGroup.DOFade(0f, t.Durations.Select).SetEase(t.Eases.In).SetUpdate(true)
                .SetLink(_maxChip.gameObject);
            return;
        }

        _maxChipGroup.alpha = 1f;
        _maxChipTween = JuiceFx.PopIn(_maxChip, 0.6f);
    }

    /// <summary>The chip is a small attention pill reading MAX, parked beside the counter.</summary>
    private void BuildMaxChip()
    {
        if (_maxChip != null || manaText == null) return;

        RectTransform counter = manaText.rectTransform.parent as RectTransform;
        if (counter == null) return;

        JuiceTokensSO t = Juice.Tokens;
        GameObject chipGo = new GameObject("MaxChip", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        _maxChip = (RectTransform)chipGo.transform;
        _maxChip.SetParent(counter, false);
        _maxChip.anchorMin = _maxChip.anchorMax = new Vector2(1f, 0.5f);
        _maxChip.pivot = new Vector2(0f, 0.5f);
        _maxChip.sizeDelta = new Vector2(80f, 36f);
        _maxChip.anchoredPosition = maxChipOffset;

        Image pill = chipGo.GetComponent<Image>();
        pill.sprite = JuiceSprites.Pill;
        pill.type = Image.Type.Sliced;
        pill.pixelsPerUnitMultiplier = 32f / 18f;
        pill.color = t.Colors.Attention;
        pill.raycastTarget = false;

        _maxChipGroup = chipGo.GetComponent<CanvasGroup>();
        _maxChipGroup.alpha = 0f;
        _maxChipGroup.blocksRaycasts = false;
        _maxChipGroup.interactable = false;

        GameObject labelGo = new GameObject("Label", typeof(RectTransform));
        RectTransform labelRect = (RectTransform)labelGo.transform;
        labelRect.SetParent(_maxChip, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelGo.AddComponent<TextMeshProUGUI>();
        RefusalCopySO copy = Resources.Load<RefusalCopySO>(RefusalCopySO.ResourcePath);
        label.font = copy != null && copy.Font != null ? copy.Font : manaText.font;
        label.fontSize = 20f;
        label.fontStyle = FontStyles.Bold;
        label.enableAutoSizing = false;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        label.text = "MAX";
        label.color = t.Colors.Nav;
    }

    private void OnServerMaxManaChanged(float previousValue, float newValue)
    {
        manaBackgroundFill.SetDashCount(newValue);

        // The ceiling rising is its own beat: "+1 MAX" off the counter, a hard punch, a rising chime.
        if (_maxAtStart < 0f || newValue <= previousValue || previousValue <= 0f) return;

        int gained = Mathf.RoundToInt(newValue - previousValue);
        if (gained <= 0) return;

        CounterAnimator.On(manaText).Float(gained, Juice.Tokens.Colors.ManaGain, " MAX");
        JuiceFx.Punch(manaText.transform, Juice.Tokens.Scales.PunchHard, unscaled: false);
        Juice.Sfx(SfxId.Rise);
    }
}
