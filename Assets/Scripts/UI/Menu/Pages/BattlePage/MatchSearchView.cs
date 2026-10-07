using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// What Battle turns into while a match is being found (MatchSearch): a dark pill with three bobbing dots,
/// SEARCHING, a running clock and a cancel button. It only reflects the search; <see cref="ConnectionManagerUI"/>
/// owns the search itself and tells this view what happened.
/// </summary>
/// <remarks>
/// Built in code from the Battle button it replaces (its sprite, its label's font), so the scene needs no new
/// objects. Battle is faded rather than switched off, so its idle breath keeps running underneath and resumes
/// the moment it comes back. "Found" has to be seen before the scene wipes away, but the load that ends the
/// search is started inside the matchmaker: so the view listens for the wipe being asked for
/// (<see cref="SceneTransition.CoverRequested"/>), plays its found beat there, and holds the wipe back for it.
/// </remarks>
public class MatchSearchView : MonoBehaviour
{
    private const string SearchingText = "SEARCHING";
    private const string FoundText = "MATCH FOUND!";
    private const float FoundHoldSeconds = 0.6f;

    private Button _battle;
    private CanvasGroup _battleGroup;
    private RectTransform _pill;
    private CanvasGroup _pillGroup;
    private Image _pillImage;
    private Color _pillColor;
    private TextMeshProUGUI _label;
    private TextMeshProUGUI _clock;
    private Button _cancel;
    private readonly RectTransform[] _dots = new RectTransform[3];
    private readonly Tween[] _bobs = new Tween[3];

    private float _startedAt;
    private bool _searching;
    private bool _found;
    private bool _hosting;
    private Sequence _transition;

    /// <summary>The player tapped the cross.</summary>
    public event Action OnCancelRequested;

    /// <summary>
    /// When the last search began (unscaled time), or below zero for none: a host that ends up waiting in the match
    /// scene keeps counting from the press of Battle rather than from zero.
    /// </summary>
    public static float SearchStartedAt { get; private set; } = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => SearchStartedAt = -1f;

    /// <summary>Builds the view beside <paramref name="battle"/>, hidden.</summary>
    public static MatchSearchView Create(Button battle)
    {
        MatchSearchView view = battle.transform.parent.gameObject.AddComponent<MatchSearchView>();
        view.Build(battle);
        return view;
    }

    /// <summary>
    /// Builds the pill alone, centred in <paramref name="parent"/>, hidden: the same search, shown where there is
    /// no Battle button (the host waiting for an opponent in the match scene).
    /// </summary>
    /// <param name="style">A label to borrow the game's font from.</param>
    public static MatchSearchView CreateIn(RectTransform parent, TMP_Text style, Vector2 size)
    {
        MatchSearchView view = parent.gameObject.AddComponent<MatchSearchView>();
        // The pill sprite's corners are 32 px: scaled so they meet in the middle, whatever the height.
        view.BuildPill(parent, null, size, JuiceSprites.Pill, Image.Type.Sliced, 32f / Mathf.Max(1f, size.y * 0.5f), style);
        return view;
    }

    private void OnEnable() => SceneTransition.CoverRequested += HandleCoverRequested;

    private void OnDisable() => SceneTransition.CoverRequested -= HandleCoverRequested;

    /// <summary>Battle shrinks away and the searching pill pops in; the clock starts.</summary>
    /// <param name="startedAt">Continue a clock already running since then (unscaled time). Null: from now.</param>
    public void ShowSearching(float? startedAt = null)
    {
        JuiceTokensSO t = Juice.Tokens;
        _searching = true;
        _found = false;
        _hosting = false;
        _startedAt = startedAt ?? Time.unscaledTime;
        SearchStartedAt = _startedAt;
        _label.text = SearchingText;
        _clock.text = "0:00";
        _clock.gameObject.SetActive(true);
        _cancel.gameObject.SetActive(true);
        _cancel.interactable = true;
        _pillImage.color = _pillColor;
        SetDots(true);

        _transition?.Kill();
        _pill.gameObject.SetActive(true);
        _pill.localScale = new Vector3(0.6f, 0.9f, 1f);
        _pillGroup.alpha = 0f;
        if (_battleGroup != null) _battleGroup.blocksRaycasts = false;

        _transition = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
            .Join(_pillGroup.DOFade(1f, t.Durations.Select))
            .Join(_pill.DOScale(1f, t.Durations.Pop).SetEase(t.Eases.Pop));
        if (_battleGroup != null) _transition.Join(_battleGroup.DOFade(0f, t.Durations.Select).SetEase(t.Eases.In));
    }

    /// <summary>The pill goes, quickly: the found beat has been seen and what comes next takes the screen.</summary>
    public Tween Dismiss()
    {
        JuiceTokensSO t = Juice.Tokens;
        _searching = false;
        SetDots(false);
        _transition?.Kill();
        _transition = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
            .Join(_pill.DOScale(0.92f, t.Durations.Select).SetEase(t.Eases.In))
            .Join(_pillGroup.DOFade(0f, t.Durations.Select).SetEase(t.Eases.In))
            .OnComplete(() => _pill.gameObject.SetActive(false));
        return _transition;
    }

    /// <summary>The cross was honoured as far as the view goes: it greys out until the search answers.</summary>
    public void ShowCancelling()
    {
        _cancel.interactable = false;
        _label.text = "CANCELLING";
        SetDots(false);
    }

    /// <summary>
    /// Nobody was waiting, so this player hosts and waits in the match scene instead: the pill keeps searching
    /// into the wipe (no "found": there is no opponent yet), and the cross goes, since nothing can be undone now.
    /// </summary>
    public void ShowHosting()
    {
        if (!_searching) return;
        _hosting = true;
        _cancel.gameObject.SetActive(false);
    }

    /// <summary>
    /// Found: the pill punches, the screen flashes in the call-to-action colour, a found chime and a Success buzz.
    /// Plays once per search, whichever comes first: the wipe being asked for, or the matchmaker's answer.
    /// </summary>
    public void ShowFound()
    {
        if (_found) return;
        _found = true;

        JuiceTokensSO t = Juice.Tokens;
        _searching = false;
        _label.text = FoundText;
        _cancel.gameObject.SetActive(false);
        _clock.gameObject.SetActive(false);
        SetDots(false);

        JuiceFx.Punch(_pill, t.Scales.PunchHard);
        _pillImage.DOKill();
        _pillImage.color = t.Colors.Cta;
        _pillImage.DOColor(_pillColor, t.Durations.Settle).SetEase(t.Eases.Out).SetUpdate(true).SetLink(gameObject);
        ScreenFlash.Flash(t.Colors.Cta, 0.3f);

        Juice.Sfx(SfxId.Found);
        Juice.Haptic(HapticRole.Success);
    }

    /// <summary>Back to Battle: the pill shrinks away and Battle pops back in, ready to press.</summary>
    public void ShowIdle(bool shake = false)
    {
        JuiceTokensSO t = Juice.Tokens;
        _searching = false;
        _found = false;
        _hosting = false;
        SetDots(false);

        _transition?.Kill();
        _transition = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

        // A failed search shakes before it goes, so the toast has a thing to belong to.
        if (shake) _transition.Append(JuiceFx.Deny(_pill, t.Distances.ShakeBar));

        _transition.Append(_pill.DOScale(0.9f, t.Durations.Select).SetEase(t.Eases.In))
            .Join(_pillGroup.DOFade(0f, t.Durations.Select).SetEase(t.Eases.In))
            .OnComplete(() =>
            {
                _pill.gameObject.SetActive(false);
                _cancel.gameObject.SetActive(true);
            })
            .OnKill(() =>
            {
                if (_battleGroup != null) _battleGroup.blocksRaycasts = true;
            });
        if (_battleGroup != null) _transition.Join(_battleGroup.DOFade(1f, t.Durations.Select));
    }

    /// <summary>The scene is about to wipe away: if that is the search succeeding, it says so first.</summary>
    private void HandleCoverRequested()
    {
        if (_hosting || (!_searching && !_found)) return;
        ShowFound();
        SceneTransition.HoldCover(FoundHoldSeconds);
    }

    private void Update()
    {
        if (!_searching) return;

        int seconds = Mathf.FloorToInt(Time.unscaledTime - _startedAt);
        _clock.text = $"{seconds / 60}:{seconds % 60:00}";
    }

    private void OnDestroy()
    {
        _transition?.Kill();
        SetDots(false);
    }

    private void SetDots(bool bobbing)
    {
        for (int i = 0; i < _dots.Length; i++)
        {
            _bobs[i]?.Kill();
            _bobs[i] = null;
            if (_dots[i] == null) continue;

            _dots[i].anchoredPosition = new Vector2(_dots[i].anchoredPosition.x, 0f);
            if (!bobbing || Juice.ReduceMotion) continue;

            _bobs[i] = _dots[i].DOAnchorPosY(10f, 0.3f).SetEase(Juice.Tokens.Eases.Breathe)
                .SetLoops(-1, LoopType.Yoyo).SetDelay(i * 0.12f).SetUpdate(true).SetLink(_dots[i].gameObject);
        }
    }

    private void Build(Button battle)
    {
        _battle = battle;
        _battleGroup = battle.GetComponent<CanvasGroup>();
        if (_battleGroup == null) _battleGroup = battle.gameObject.AddComponent<CanvasGroup>();

        RectTransform battleRect = (RectTransform)battle.transform;
        Image battleImage = battle.GetComponent<Image>();

        // The pill: Battle's own shape, wider, where Battle is.
        BuildPill((RectTransform)battleRect.parent, battleRect,
            new Vector2(battleRect.rect.width * 1.6f, battleRect.rect.height),
            battleImage != null ? battleImage.sprite : JuiceSprites.Pill,
            battleImage != null ? battleImage.type : Image.Type.Sliced,
            battleImage != null ? battleImage.pixelsPerUnitMultiplier : 1f,
            battle.GetComponentInChildren<TMP_Text>(true));
    }

    /// <summary>
    /// The pill itself, in the dark sheet colour so the dots and the clock read on it: placed like
    /// <paramref name="placeLike"/> when given (Battle), otherwise centred in <paramref name="parent"/>.
    /// </summary>
    private void BuildPill(RectTransform parent, RectTransform placeLike, Vector2 size, Sprite sprite, Image.Type type,
        float pixelsPerUnitMultiplier, TMP_Text battleLabel)
    {
        JuiceTokensSO t = Juice.Tokens;

        GameObject pill = new GameObject("MatchSearchPill", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        _pill = (RectTransform)pill.transform;
        _pill.SetParent(parent, false);
        if (placeLike != null)
        {
            _pill.SetSiblingIndex(placeLike.GetSiblingIndex() + 1);
            _pill.anchorMin = placeLike.anchorMin;
            _pill.anchorMax = placeLike.anchorMax;
            _pill.pivot = placeLike.pivot;
            _pill.anchoredPosition = placeLike.anchoredPosition;
        }
        else
        {
            _pill.anchorMin = _pill.anchorMax = _pill.pivot = new Vector2(0.5f, 0.5f);
            _pill.anchoredPosition = Vector2.zero;
        }

        float width = size.x;
        float height = size.y;
        _pill.sizeDelta = new Vector2(width, height);
        _pillGroup = pill.GetComponent<CanvasGroup>();

        _pillImage = pill.GetComponent<Image>();
        _pillImage.sprite = sprite;
        _pillImage.type = type;
        _pillImage.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;

        _pillColor = t.Colors.Sheet;
        _pillImage.color = _pillColor;
        _pillImage.raycastTarget = false;

        // Left to right, as in the design: dots, SEARCHING, then the clock and the cancel at the far end.
        float left = -width * 0.5f + height * 0.42f;
        for (int i = 0; i < _dots.Length; i++)
        {
            RectTransform holder = NewRect("DotHolder", _pill, new Vector2(18f, 18f));
            holder.anchoredPosition = new Vector2(left + i * 28f, 0f);

            RectTransform dot = NewRect("Dot", holder, new Vector2(18f, 18f));
            Image image = dot.gameObject.AddComponent<Image>();
            image.sprite = JuiceSprites.Disc;
            image.color = t.Colors.Cta;
            image.raycastTarget = false;
            _dots[i] = dot;
        }

        _label = Text("Label", battleLabel, SearchingText, height * 0.34f, t.Colors.Ink);
        _label.alignment = TextAlignmentOptions.MidlineLeft;
        _label.rectTransform.sizeDelta = new Vector2(width * 0.5f, height);

        // "MATCH FOUND!" is longer than "SEARCHING": it shrinks to fit rather than running into the clock.
        _label.enableAutoSizing = true;
        _label.fontSizeMax = height * 0.34f;
        _label.fontSizeMin = height * 0.2f;
        _label.rectTransform.anchoredPosition = new Vector2(left + 3 * 28f + 12f + width * 0.25f, 0f);

        float cancelSize = height * 0.6f;
        float cancelX = width * 0.5f - height * 0.5f;
        _clock = Text("Clock", battleLabel, "0:00", height * 0.26f, t.Colors.InkMuted);
        _clock.alignment = TextAlignmentOptions.MidlineRight;
        _clock.rectTransform.sizeDelta = new Vector2(width * 0.25f, height);
        _clock.rectTransform.anchoredPosition = new Vector2(cancelX - cancelSize * 0.5f - 16f - width * 0.125f, 0f);

        // The cancel: a light round button with a dark cross, the one control on the pill.
        GameObject cross = new GameObject("Cancel", typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform crossRect = (RectTransform)cross.transform;
        crossRect.SetParent(_pill, false);
        crossRect.anchorMin = crossRect.anchorMax = new Vector2(0.5f, 0.5f);
        crossRect.sizeDelta = Vector2.one * cancelSize;
        crossRect.anchoredPosition = new Vector2(cancelX, 0f);

        Image crossImage = cross.GetComponent<Image>();
        crossImage.sprite = JuiceSprites.Disc;
        crossImage.color = t.Colors.ButtonFace;

        TextMeshProUGUI x = Text("X", battleLabel, "×", height * 0.44f, t.Colors.Nav);
        x.rectTransform.SetParent(crossRect, false);
        x.rectTransform.anchoredPosition = new Vector2(0f, 3f);
        x.rectTransform.sizeDelta = crossRect.sizeDelta;
        x.alignment = TextAlignmentOptions.Center;
        x.fontSharedMaterial = battleLabel != null && battleLabel.font != null ? battleLabel.font.material : x.fontSharedMaterial;

        _cancel = cross.GetComponent<Button>();
        _cancel.onClick.AddListener(() => OnCancelRequested?.Invoke());
        cross.AddComponent<JuicyButton>();

        pill.SetActive(false);
    }

    private static RectTransform NewRect(string name, Transform parent, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        return rect;
    }

    private TextMeshProUGUI Text(string name, TMP_Text style, string value, float size, Color color)
    {
        RectTransform rect = NewRect(name, _pill, Vector2.zero);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (style != null)
        {
            text.font = style.font;
            text.fontSharedMaterial = style.fontSharedMaterial;
        }

        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }
}
