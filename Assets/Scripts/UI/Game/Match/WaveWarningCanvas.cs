using System.Collections;
using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The wave beats on the local player's lane (WaveBanner): a wave slams in over a dark strip and rises away; the
/// final wave turns foe-red with a drum and a pulsing screen edge; a cleared wave turns green with a sparkle.
/// </summary>
/// <remarks>
/// Driven by one DOTween sequence per banner, unscaled, so a banner caught by the tutorial's freeze still finishes.
/// The subtitle and the edge pulse are built in code from the authored title, so the scene keeps its one label. A
/// "cleared" never steps on the next wave: the wave that follows waits for it to finish.
/// </remarks>
public class WaveWarningCanvas : MonoBehaviour
{
    private const string WAVE_TOKEN = "{WAVE}";

    [Title("References")]
    [SerializeField] private GameObject waveWarningRoot;
    [SerializeField] private TextMeshProUGUI  waveWarningText;

    [Title("Copy")]
    [SerializeField] private string waveTitle = "WAVE {WAVE}";
    [SerializeField] private string waveSubtitle = "INCOMING";
    [SerializeField] private string finalTitle = "FINAL WAVE";
    [SerializeField] private string finalSubtitle = "HOLD THE LINE";
    [SerializeField] private string clearedTitle = "WAVE CLEARED";
    [SerializeField] private string clearedSubtitle = "WAVE {WAVE} DOWN";

    [Title("Timing")]
    [Tooltip("How long a banner holds before it rises away.")]
    [SerializeField, Min(0f)] private float holdSeconds = 1.3f;

    private enum Banner { Wave, Final, Cleared }

    private BaseServerWaveManager _serverWaveManager;
    private TeamType _localTeamConnection;

    private bool _built;
    private RectTransform _band;
    private Vector2 _bandRest;
    private CanvasGroup _group;
    private RectTransform _strip;
    private TextMeshProUGUI _subtitle;
    private Image _edge;
    private Color _titleColor;

    private Sequence _banner;
    private Banner _playing;
    private bool _hasPending;
    private Banner _pendingKind;
    private int _pendingWave;

    private void Awake()
    {
        waveWarningRoot.SetActive(false);
    }

    private void Start()
    {
        _serverWaveManager = ServiceLocator.Get<BaseServerWaveManager>();

        StartCoroutine(SubscribeToCorrectEvent());
    }

    private IEnumerator SubscribeToCorrectEvent()
    {
        yield return new WaitUntil(() => ServiceLocator.Get<BaseTeamManager>() != null);
        BaseTeamManager teamManager = ServiceLocator.Get<BaseTeamManager>();

        yield return new WaitUntil(() => teamManager.HasLocalTeamBeenAssigned());

        _localTeamConnection = teamManager.GetLocalTeam();

        switch (_localTeamConnection)
        {
            case TeamType.Red:
                _serverWaveManager.RedCurrentWave.OnValueChanged += OnWaveChanged;
                _serverWaveManager.RedCurrentWaveProgressNormalized.OnValueChanged += OnWaveProgressChanged;
                break;
            case TeamType.Blue:
                _serverWaveManager.BlueCurrentWave.OnValueChanged += OnWaveChanged;
                _serverWaveManager.BlueCurrentWaveProgressNormalized.OnValueChanged += OnWaveProgressChanged;
                break;
        }
    }

    private void OnDestroy()
    {
        _banner?.Kill();
        if (_serverWaveManager == null) return;

        switch (_localTeamConnection)
        {
            case TeamType.Red:
                _serverWaveManager.RedCurrentWave.OnValueChanged -= OnWaveChanged;
                _serverWaveManager.RedCurrentWaveProgressNormalized.OnValueChanged -= OnWaveProgressChanged;
                break;
            case TeamType.Blue:
                _serverWaveManager.BlueCurrentWave.OnValueChanged -= OnWaveChanged;
                _serverWaveManager.BlueCurrentWaveProgressNormalized.OnValueChanged -= OnWaveProgressChanged;
                break;
        }
    }

    private void OnWaveChanged(int previousValue, int newValue)
    {
        int total = _serverWaveManager != null ? _serverWaveManager.GetTotalWaves() : 0;
        Show(total > 0 && newValue >= total ? Banner.Final : Banner.Wave, newValue);
    }

    /// <summary>The lane's wave is down to its last enemy: cleared. Not the last wave, which ends the match instead.</summary>
    private void OnWaveProgressChanged(float previousValue, float newValue)
    {
        if (previousValue >= 0.999f || newValue < 0.999f || _serverWaveManager == null) return;

        int wave = CurrentWave();
        int total = _serverWaveManager.GetTotalWaves();
        if (wave <= 0 || (total > 0 && wave >= total)) return;

        Show(Banner.Cleared, wave);
    }

    private int CurrentWave() => _localTeamConnection == TeamType.Blue
        ? _serverWaveManager.BlueCurrentWave.Value
        : _serverWaveManager.RedCurrentWave.Value;

    /// <summary>A cleared banner gets to finish before the next wave's; anything else replaces what is up.</summary>
    private void Show(Banner kind, int wave)
    {
        if (_banner != null && _banner.IsActive() && _playing == Banner.Cleared && kind != Banner.Cleared)
        {
            _hasPending = true;
            _pendingKind = kind;
            _pendingWave = wave;
            return;
        }

        Play(kind, wave);
    }

    private void Play(Banner kind, int wave)
    {
        Build();
        _banner?.Kill();

        JuiceTokensSO t = Juice.Tokens;
        _playing = kind;
        bool final = kind == Banner.Final;
        bool cleared = kind == Banner.Cleared;

        string title = kind switch { Banner.Final => finalTitle, Banner.Cleared => clearedTitle, _ => waveTitle };
        string subtitle = kind switch { Banner.Final => finalSubtitle, Banner.Cleared => clearedSubtitle, _ => waveSubtitle };
        waveWarningText.text = title.Replace(WAVE_TOKEN, wave.ToString());
        waveWarningText.color = final ? t.Colors.HpFoe : cleared ? t.Colors.Gain : _titleColor;
        _subtitle.text = subtitle.Replace(WAVE_TOKEN, wave.ToString());
        _subtitle.gameObject.SetActive(!string.IsNullOrEmpty(subtitle));

        // The starting pose: strip shut, title big and tilted, nothing visible yet.
        waveWarningRoot.SetActive(true);
        RectTransform titleRect = waveWarningText.rectTransform;
        _band.anchoredPosition = _bandRest;
        _group.alpha = 1f;
        _strip.localScale = new Vector3(1f, 0f, 1f);
        titleRect.localScale = Vector3.one * (Juice.ReduceMotion ? 1.15f : 1.6f);
        titleRect.localRotation = Quaternion.Euler(0f, 0f, Juice.ReduceMotion ? 0f : 4f);
        waveWarningText.alpha = 0f;
        _subtitle.alpha = 0f;

        Sequence s = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        s.Append(_strip.DOScaleY(1f, t.Durations.Select).SetEase(t.Eases.Out));
        s.Join(waveWarningText.DOFade(1f, t.Durations.Select));
        s.Join(titleRect.DOScale(1f, t.Durations.Pop).SetEase(t.Eases.Pop));
        s.Join(titleRect.DOLocalRotate(Vector3.zero, t.Durations.Pop).SetEase(t.Eases.Pop));
        s.Insert(t.Durations.Select, _subtitle.DOFade(1f, t.Durations.Pop));
        s.InsertCallback(t.Durations.Pop * 0.5f, () => Land(kind));

        // The final wave: the screen's edge breathes in the foe colour while the banner holds.
        if (final && _edge != null && !Juice.ReduceMotion)
        {
            _edge.color = Faded(t.Colors.HpFoe, 0f);
            s.Insert(0f, _edge.DOFade(0.5f, 0.45f).SetEase(t.Eases.Breathe).SetLoops(4, LoopType.Yoyo));
        }

        // Hold, then rise away.
        s.AppendInterval(holdSeconds);
        s.Append(_band.DOAnchorPos(_bandRest + Vector2.up * t.Distances.RiseBanner, t.Durations.Travel).SetEase(t.Eases.In));
        s.Join(_group.DOFade(0f, t.Durations.Travel).SetEase(t.Eases.In));

        s.OnKill(() =>
        {
            if (this == null) return;
            _band.anchoredPosition = _bandRest;
            _strip.localScale = Vector3.one;
            titleRect.localScale = Vector3.one;
            titleRect.localRotation = Quaternion.identity;
            if (_edge != null) _edge.color = Faded(_edge.color, 0f);
            waveWarningRoot.SetActive(false);
            _banner = null;

            if (!_hasPending) return;
            _hasPending = false;
            Play(_pendingKind, _pendingWave);
        });

        _banner = s;
    }

    /// <summary>The title lands: a micro shake and the beat's own sound and buzz.</summary>
    private void Land(Banner kind)
    {
        JuiceTokensSO t = Juice.Tokens;
        JuiceCamera.Shake(t.Shake.Micro, t.Durations.Shake);

        switch (kind)
        {
            case Banner.Final:
                Juice.Sfx(SfxId.Drum);
                Juice.Haptic(HapticRole.Medium);
                break;
            case Banner.Cleared:
                Juice.Sfx(SfxId.Rise);
                Juice.Haptic(HapticRole.Success);
                RarityBurst.Play(waveWarningText.rectTransform, t.Colors.Sparkle, 0.45f, behind: true);
                break;
            default:
                Juice.Sfx(SfxId.Horn);
                Juice.Haptic(HapticRole.Light);
                break;
        }
    }

    private void Build()
    {
        if (_built) return;
        _built = true;

        _titleColor = waveWarningText.color;

        // The title is the banner: let it grow as big as its half of the strip allows.
        waveWarningText.enableAutoSizing = true;
        waveWarningText.fontSizeMax = Mathf.Max(waveWarningText.fontSizeMax, 120f);
        _band = (RectTransform)waveWarningText.rectTransform.parent;
        _bandRest = _band.anchoredPosition;
        _group = _band.GetComponent<CanvasGroup>();
        if (_group == null) _group = _band.gameObject.AddComponent<CanvasGroup>();

        Transform strip = _band.Find("Background");
        _strip = strip != null ? (RectTransform)strip : _band;

        // Title over the top of the strip, the subtitle under it.
        RectTransform titleRect = waveWarningText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 0.3f);
        titleRect.anchorMax = Vector2.one;
        titleRect.offsetMin = titleRect.offsetMax = Vector2.zero;

        _subtitle = Instantiate(waveWarningText, _band);
        _subtitle.name = "WaveSubtitle";
        _subtitle.enableAutoSizing = false;
        _subtitle.fontSize = Mathf.Max(24f, waveWarningText.fontSize * 0.42f);
        _subtitle.color = Juice.Tokens.Colors.InkMuted;
        RectTransform subRect = _subtitle.rectTransform;
        subRect.anchorMin = new Vector2(0f, 0.02f);
        subRect.anchorMax = new Vector2(1f, 0.34f);
        subRect.offsetMin = subRect.offsetMax = Vector2.zero;

        // The screen's edge, for the final wave: full screen, behind the strip, never in the way of a touch.
        Transform root = waveWarningRoot.transform;
        GameObject edge = new GameObject("FinalWaveEdge", typeof(RectTransform), typeof(Image));
        RectTransform edgeRect = (RectTransform)edge.transform;
        edgeRect.SetParent(root, false);
        edgeRect.SetAsFirstSibling();
        edgeRect.anchorMin = Vector2.zero;
        edgeRect.anchorMax = Vector2.one;
        edgeRect.offsetMin = edgeRect.offsetMax = Vector2.zero;
        _edge = edge.GetComponent<Image>();
        _edge.sprite = JuiceSprites.Vignette;
        _edge.raycastTarget = false;
        _edge.color = Faded(Juice.Tokens.Colors.HpFoe, 0f);
    }

    private static Color Faded(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
