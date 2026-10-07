using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The BaseDamage recipe's screen half: a red vignette that flashes in from the edges when the player's own base is
/// hit (with the base-hit thud, a Heavy buzz and a long medium shake); when the camera is on the other field, an
/// edge alert pointing home ("▼ Base hit -8") so a hit they cannot see still reaches them; and under a quarter of
/// the health, a heartbeat: every 0.9s the heart double-beats, the edges pulse red, and it is heard.
/// </summary>
/// <remarks>
/// Built in code on its own Screen Space - Overlay canvas, below the end screen and the wave banner (sort 2) and
/// with no raycaster, so it can never take a touch. Created by <c>ClientPlayersDataCanvas</c>, which already knows
/// when a base is hit, which side the camera is on, and where the heart is. The heartbeat stops with the match.
/// </remarks>
public class BaseDamageOverlay : MonoBehaviour
{
    private const int SortingOrder = 2;
    private const float LowHealthShare = 0.25f;
    private const float BeatSeconds = 0.9f;
    private const float RestingRed = 0.12f;

    private Image _vignette;
    private RectTransform _alert;
    private CanvasGroup _alertGroup;
    private TextMeshProUGUI _alertText;
    private Transform _heart;
    private Tween _flash;
    private Sequence _heartbeat;
    private Sequence _alertShow;
    private bool _low;
    private bool _ended;
    private BaseServerEndGameManager _endGame;

    public static BaseDamageOverlay Create(Transform parent)
    {
        GameObject go = new GameObject("BaseDamageOverlay", typeof(RectTransform));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);

        // Nested under a canvas it fills that canvas; its own sorting puts it above the HUD.
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        BaseDamageOverlay overlay = go.AddComponent<BaseDamageOverlay>();
        overlay.Build();
        return overlay;
    }

    /// <summary>The heart in the HUD, which beats with the low-health heartbeat.</summary>
    public void SetHeart(Transform heart) => _heart = heart;

    private void Start()
    {
        // A low base keeps beating only while there is a match to lose.
        if (ServiceLocator.TryGet(out _endGame)) _endGame.OnGameEnded += HandleGameEnded;
    }

    /// <summary>The player's own base took a hit.</summary>
    /// <param name="healthShare">What is left, 0..1, for the low-health heartbeat.</param>
    /// <param name="away">The camera is on the other field, so the hit happened out of sight.</param>
    /// <param name="damage">How much it took, for the alert.</param>
    public void Hit(float healthShare, bool away, int damage)
    {
        if (_ended) return;
        JuiceTokensSO t = Juice.Tokens;

        Juice.Sfx(SfxId.BaseHit);
        Juice.Haptic(HapticRole.Heavy);
        JuiceCamera.Shake(t.Shake.Medium, t.Durations.ShakeLong);

        if (JuiceCamera.TakeFlash())
        {
            _flash?.Kill();
            _vignette.color = Faded(t.Colors.Deny, 0.6f);
            _flash = _vignette.DOFade(_low ? RestingRed : 0f, t.Durations.ShakeLong * 2.5f).SetEase(t.Eases.Out)
                .SetUpdate(true).SetLink(gameObject);
        }

        if (away) ShowAlert(damage);

        bool low = healthShare > 0f && healthShare <= LowHealthShare;
        if (low != _low)
        {
            _low = low;
            if (low) StartHeartbeat();
            else StopHeartbeat();
        }
    }

    /// <summary>The camera came home: the alert has said what it had to.</summary>
    public void HideAlert()
    {
        if (_alert == null || !_alert.gameObject.activeSelf) return;
        _alertShow?.Kill();
        _alertShow = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
            .Join(_alertGroup.DOFade(0f, Juice.Tokens.Durations.Select))
            .OnComplete(() => _alert.gameObject.SetActive(false));
    }

    /// <summary>"▼ Base hit -N", rising in at the bottom edge, pointing home.</summary>
    private void ShowAlert(int damage)
    {
        JuiceTokensSO t = Juice.Tokens;
        _alertText.text = damage > 0 ? $"Base hit -{damage}" : "Base hit";
        _alertShow?.Kill();
        _alert.gameObject.SetActive(true);

        _alertShow = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        if (_alertGroup.alpha < 0.5f)
        {
            _alertGroup.alpha = 0f;
            _alert.anchoredPosition = AlertRest + Vector2.down * 24f;
            _alertShow.Join(_alertGroup.DOFade(1f, t.Durations.Select))
                .Join(_alert.DOAnchorPos(AlertRest, t.Durations.Pop).SetEase(t.Eases.Out));
        }
        else
        {
            _alert.anchoredPosition = AlertRest;
            _alertShow.Join(_alert.DOPunchScale(Vector3.one * t.Scales.PunchSoft, t.Durations.Pop, 6, 0.6f));
        }

        // Says it, then gets out of the way; another hit brings it back.
        _alertShow.AppendInterval(t.Durations.Toast)
            .Append(_alertGroup.DOFade(0f, t.Durations.Panel))
            .OnComplete(() => _alert.gameObject.SetActive(false))
            .OnKill(() => _alert.localScale = Vector3.one);
    }

    private static readonly Vector2 AlertRest = new(0f, 420f);

    /// <summary>
    /// Under a quarter: lub-dub every <see cref="BeatSeconds"/>. The heart punches twice, the edges pulse red and
    /// settle on a faint red rest, and the beat is heard. Flashes stay inside the flash budget: one per beat.
    /// </summary>
    private void StartHeartbeat()
    {
        StopHeartbeat();
        if (_ended) return;

        JuiceTokensSO t = Juice.Tokens;
        Color red = t.Colors.Deny;
        _vignette.color = Faded(red, RestingRed);
        float peak = Juice.ReduceMotion ? 0.3f : 0.48f;

        _heartbeat = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
            .AppendCallback(() =>
            {
                Juice.Sfx(SfxId.Heartbeat);
                if (_heart != null) JuiceFx.Punch(_heart, t.Scales.PunchSoft, 0.26f);
            })
            .Append(_vignette.DOFade(peak, 0.08f).SetEase(t.Eases.Out))
            .Append(_vignette.DOFade(RestingRed, 0.3f).SetEase(t.Eases.In))
            .AppendCallback(() =>
            {
                if (_heart != null) JuiceFx.Punch(_heart, t.Scales.PunchSoft, 0.26f);
            })
            .Append(_vignette.DOFade(peak * 0.6f, 0.07f).SetEase(t.Eases.Out))
            .Append(_vignette.DOFade(RestingRed, 0.25f).SetEase(t.Eases.In))
            .AppendInterval(Mathf.Max(0f, BeatSeconds - 0.7f))
            .SetLoops(-1);
    }

    private void StopHeartbeat()
    {
        _heartbeat?.Kill();
        _heartbeat = null;
        if (!_low && (_flash == null || !_flash.IsActive())) _vignette.color = Color.clear;
    }

    private void HandleGameEnded(EndGameSnapshot snapshot)
    {
        _ended = true;
        _low = false;
        StopHeartbeat();
        _flash?.Kill();
        _vignette.color = Color.clear;
        HideAlert();
    }

    private void OnDestroy()
    {
        if (_endGame != null) _endGame.OnGameEnded -= HandleGameEnded;
        _flash?.Kill();
        _heartbeat?.Kill();
        _alertShow?.Kill();
    }

    private static Color Faded(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = SortingOrder;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject vignette = new GameObject("Vignette", typeof(RectTransform), typeof(Image));
        RectTransform vRect = (RectTransform)vignette.transform;
        vRect.SetParent(transform, false);
        vRect.anchorMin = Vector2.zero;
        vRect.anchorMax = Vector2.one;
        vRect.offsetMin = vRect.offsetMax = Vector2.zero;
        _vignette = vignette.GetComponent<Image>();
        _vignette.sprite = JuiceSprites.Vignette;
        _vignette.color = Color.clear;
        _vignette.raycastTarget = false;

        // The alert that points home: low on the screen, just above the hand, on the dark sheet with the deny text.
        JuiceTokensSO t = Juice.Tokens;
        GameObject alert = new GameObject("AwayAlert", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        _alert = (RectTransform)alert.transform;
        _alert.SetParent(transform, false);
        _alert.anchorMin = _alert.anchorMax = new Vector2(0.5f, 0f);
        _alert.pivot = new Vector2(0.5f, 0.5f);
        _alert.anchoredPosition = AlertRest;
        _alert.sizeDelta = new Vector2(440f, 86f);
        _alertGroup = alert.GetComponent<CanvasGroup>();
        _alertGroup.blocksRaycasts = false;
        _alertGroup.interactable = false;
        _alertGroup.alpha = 0f;

        Image pill = alert.GetComponent<Image>();
        pill.sprite = JuiceSprites.Pill;
        pill.type = Image.Type.Sliced;
        pill.color = t.Colors.Sheet;
        pill.raycastTarget = false;

        GameObject arrow = new GameObject("Arrow", typeof(RectTransform), typeof(Image));
        RectTransform aRect = (RectTransform)arrow.transform;
        aRect.SetParent(_alert, false);
        aRect.anchorMin = aRect.anchorMax = new Vector2(0f, 0.5f);
        aRect.sizeDelta = new Vector2(40f, 40f);
        aRect.anchoredPosition = new Vector2(52f, 0f);
        Image arrowImage = arrow.GetComponent<Image>();
        arrowImage.sprite = JuiceSprites.ArrowDown;
        arrowImage.color = t.Colors.Deny;
        arrowImage.raycastTarget = false;

        GameObject label = new GameObject("Label", typeof(RectTransform));
        RectTransform lRect = (RectTransform)label.transform;
        lRect.SetParent(_alert, false);
        lRect.anchorMin = Vector2.zero;
        lRect.anchorMax = Vector2.one;
        lRect.offsetMin = new Vector2(84f, 0f);
        lRect.offsetMax = new Vector2(-28f, 0f);
        _alertText = label.AddComponent<TextMeshProUGUI>();
        RefusalCopySO copy = Resources.Load<RefusalCopySO>(RefusalCopySO.ResourcePath);
        if (copy != null && copy.Font != null) _alertText.font = copy.Font;
        _alertText.text = "Base hit";
        _alertText.fontSize = 42f;
        _alertText.alignment = TextAlignmentOptions.MidlineLeft;
        _alertText.color = t.Colors.Deny;
        _alertText.raycastTarget = false;
        _alertText.textWrappingMode = TextWrappingModes.NoWrap;

        alert.SetActive(false);
    }
}
