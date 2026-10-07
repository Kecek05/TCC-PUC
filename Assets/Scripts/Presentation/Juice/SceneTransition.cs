using System;
using System.Collections.Generic;
using DG.Tweening;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The circle wipe between scenes (MatchSearch's wipe, used for every scene switch): before a load, a disc in the
/// nav colour grows from the centre until it covers the screen; once the next scene is in, a hole opens from the
/// centre to reveal it. <see cref="Loader"/> asks for the cover and only loads once it is complete; the reveal
/// follows any scene that finishes loading while covered.
/// </summary>
/// <remarks>
/// One persistent instance (DontDestroyOnLoad) built in code, on a Screen Space - Overlay canvas above everything
/// else, with a raycaster so nothing can be pressed while the screen is covered or wiping. Unscaled time: a scene
/// can be left from a frozen tutorial step. A client following the host's scene sync never calls the Loader, so a
/// Netcode load start covers the screen at once instead, and the reveal plays as usual. A cover that sees no scene
/// load within <see cref="SafetySeconds"/> reveals anyway: a covered screen must never become a dead end.
/// </remarks>
public class SceneTransition : MonoBehaviour
{
    private const int SortingOrder = 95;
    private const float SafetySeconds = 12f;
    private const float DotsAfterSeconds = 0.35f;

    private enum State { Idle, Covering, Covered, Revealing }

    private static SceneTransition _instance;
    private static float _leadIn;

    /// <summary>
    /// Raised when a cover is asked for, before it starts. A listener that has a beat to finish first (a "match
    /// found" punch) calls <see cref="HoldCover"/> from its handler to delay the wipe by that long.
    /// </summary>
    public static event Action CoverRequested;

    private readonly List<Action> _onCovered = new();
    private IrisGraphic _iris;
    private CanvasGroup _dots;
    private readonly RectTransform[] _dotRects = new RectTransform[3];
    private State _state = State.Idle;
    private Sequence _tween;
    private float _coveredAt;
    private NetworkSceneManager _hookedSceneManager;
    private int _revealFrames = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
        _leadIn = 0f;
        CoverRequested = null;
    }

    /// <summary>Whether the screen is (or is becoming) covered.</summary>
    public static bool IsCovering => _instance != null && (_instance._state == State.Covering || _instance._state == State.Covered);

    /// <summary>From a <see cref="CoverRequested"/> handler: hold the wipe back this long for a beat to be seen.</summary>
    public static void HoldCover(float seconds) => _leadIn = Mathf.Max(_leadIn, seconds);

    /// <summary>
    /// Covers the screen, then runs <paramref name="onCovered"/> (the scene load). Already covered: runs it at once.
    /// Outside play mode, runs it at once.
    /// </summary>
    public static void Cover(Action onCovered)
    {
        if (!Application.isPlaying)
        {
            onCovered?.Invoke();
            return;
        }

        Instance.BeginCover(onCovered);
    }

    private static SceneTransition Instance
    {
        get
        {
            if (_instance != null) return _instance;

            GameObject go = new GameObject("[Juice] SceneTransition");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SceneTransition>();
            _instance.Build();
            return _instance;
        }
    }

    private void OnEnable() => SceneManager.sceneLoaded += HandleSceneLoaded;

    private void OnDisable() => SceneManager.sceneLoaded -= HandleSceneLoaded;

    private void OnDestroy()
    {
        _tween?.Kill();
        Unhook();
        if (_instance == this) _instance = null;
    }

    private void BeginCover(Action onCovered)
    {
        if (onCovered != null) _onCovered.Add(onCovered);

        if (_state == State.Covered)
        {
            FlushCovered();
            return;
        }

        if (_state == State.Covering) return;

        _leadIn = 0f;
        CoverRequested?.Invoke();
        float lead = _leadIn;
        _leadIn = 0f;

        JuiceTokensSO t = Juice.Tokens;
        bool fromReveal = _state == State.Revealing;
        _state = State.Covering;
        _tween?.Kill();
        Show(true);

        float cover = _iris.CoverRadius;
        _tween = DOTween.Sequence().SetUpdate(true).SetLink(gameObject).SetDelay(lead);
        _tween.AppendCallback(() => Juice.Sfx(SfxId.Whoosh));

        if (fromReveal)
        {
            // A switch asked for while the last one was still opening: close the hole again from where it is.
            _iris.OuterRadius = cover;
            _tween.Append(DOVirtual.Float(_iris.InnerRadius, 0f, t.Durations.Travel * 0.6f, v => _iris.InnerRadius = v).SetEase(t.Eases.In));
        }
        else
        {
            _iris.InnerRadius = 0f;
            _iris.OuterRadius = 0f;
            _tween.Append(DOVirtual.Float(0f, cover, t.Durations.Travel, v => _iris.OuterRadius = v).SetEase(t.Eases.In));
        }

        _tween.OnComplete(MarkCovered);
    }

    /// <summary>The screen goes dark at once: a load that started without asking (a client following the host).</summary>
    private void CoverInstant()
    {
        if (_state == State.Covered) return;
        _tween?.Kill();
        Show(true);
        _iris.InnerRadius = 0f;
        _iris.OuterRadius = _iris.CoverRadius;
        MarkCovered();
    }

    private void MarkCovered()
    {
        _state = State.Covered;
        _coveredAt = Time.unscaledTime;
        FlushCovered();
    }

    private void FlushCovered()
    {
        if (_onCovered.Count == 0) return;

        // Copied first: a callback that loads a scene can raise sceneLoaded (and another Cover) synchronously.
        Action[] pending = _onCovered.ToArray();
        _onCovered.Clear();
        foreach (Action action in pending)
        {
            try { action(); }
            catch (Exception e) { GameLog.Exception(e); }
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;

        // Something loaded while the disc was still growing (a load we did not start): finish covering first.
        if (_state == State.Covering && _onCovered.Count == 0) CoverInstant();
        if (_state != State.Covered) return;

        // Let the new scene run its first frames (layouts, Starts) before it is shown.
        _revealFrames = 2;
    }

    private void Update()
    {
        HookNetworkScenes();

        if (_state == State.Covered)
        {
            if (_dots != null && _dots.alpha < 1f && Time.unscaledTime - _coveredAt > DotsAfterSeconds)
                _dots.alpha = Mathf.MoveTowards(_dots.alpha, 1f, Time.unscaledDeltaTime * 4f);

            if (_revealFrames > 0 && --_revealFrames == 0) Reveal();
            else if (_revealFrames < 0 && Time.unscaledTime - _coveredAt > SafetySeconds) Reveal();
        }
    }

    private void Reveal()
    {
        _revealFrames = -1;
        if (_state != State.Covered) return;

        JuiceTokensSO t = Juice.Tokens;
        _state = State.Revealing;
        _tween?.Kill();
        if (_dots != null) _dots.alpha = 0f;

        float cover = _iris.CoverRadius;
        _iris.OuterRadius = cover;
        _iris.InnerRadius = 0f;
        _tween = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
            .AppendInterval(0.05f)
            .Append(DOVirtual.Float(0f, cover, t.Durations.Travel * 1.15f, v => _iris.InnerRadius = v).SetEase(t.Eases.Out))
            .OnComplete(() =>
            {
                _state = State.Idle;
                Show(false);
            });
    }

    private void Show(bool show)
    {
        if (_iris != null) _iris.gameObject.SetActive(show);
        if (_dots != null) _dots.gameObject.SetActive(show);
        if (!show && _dots != null) _dots.alpha = 0f;
    }

    /// <summary>A client's scenes are loaded by the host's scene sync, never by its own Loader call.</summary>
    private void HookNetworkScenes()
    {
        NetworkManager network = NetworkManager.Singleton;
        NetworkSceneManager scenes = network != null ? network.SceneManager : null;
        if (scenes == _hookedSceneManager) return;

        Unhook();
        _hookedSceneManager = scenes;
        if (scenes != null) scenes.OnSceneEvent += HandleNetworkSceneEvent;
    }

    private void Unhook()
    {
        if (_hookedSceneManager != null) _hookedSceneManager.OnSceneEvent -= HandleNetworkSceneEvent;
        _hookedSceneManager = null;
    }

    private void HandleNetworkSceneEvent(SceneEvent sceneEvent)
    {
        NetworkManager network = NetworkManager.Singleton;
        if (network == null || sceneEvent.ClientId != network.LocalClientId) return;
        if (sceneEvent.SceneEventType != SceneEventType.Load || sceneEvent.LoadSceneMode != LoadSceneMode.Single) return;
        if (_state == State.Idle || _state == State.Revealing) CoverInstant();
    }

    private void Build()
    {
        JuiceTokensSO t = Juice.Tokens;

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        // The CanvasRenderer is listed explicitly: AddComponent of a Graphic subclass did not pull it in here, and
        // without it the iris neither draws nor survives a raycast.
        GameObject iris = new GameObject("Iris", typeof(RectTransform), typeof(CanvasRenderer));
        RectTransform irisRect = (RectTransform)iris.transform;
        irisRect.SetParent(transform, false);
        irisRect.anchorMin = Vector2.zero;
        irisRect.anchorMax = Vector2.one;
        irisRect.offsetMin = irisRect.offsetMax = Vector2.zero;
        _iris = iris.AddComponent<IrisGraphic>();
        _iris.color = t.Colors.Nav;
        _iris.raycastTarget = true; // the whole screen is held while a switch runs

        // Three dots that bob while a load takes its time, the same beat as the searching pill.
        GameObject dots = new GameObject("LoadingDots", typeof(RectTransform), typeof(CanvasGroup));
        RectTransform dotsRect = (RectTransform)dots.transform;
        dotsRect.SetParent(transform, false);
        dotsRect.anchorMin = dotsRect.anchorMax = new Vector2(0.5f, 0.5f);
        dotsRect.sizeDelta = new Vector2(120f, 40f);
        _dots = dots.GetComponent<CanvasGroup>();
        _dots.alpha = 0f;
        _dots.blocksRaycasts = false;

        for (int i = 0; i < _dotRects.Length; i++)
        {
            GameObject dot = new GameObject("Dot", typeof(RectTransform), typeof(Image));
            RectTransform dotRect = (RectTransform)dot.transform;
            dotRect.SetParent(dotsRect, false);
            dotRect.anchorMin = dotRect.anchorMax = new Vector2(0.5f, 0.5f);
            dotRect.sizeDelta = Vector2.one * 18f;
            dotRect.anchoredPosition = new Vector2((i - 1) * 34f, 0f);
            Image image = dot.GetComponent<Image>();
            image.sprite = JuiceSprites.Disc;
            image.color = t.Colors.Cta;
            image.raycastTarget = false;
            _dotRects[i] = dotRect;

            dotRect.DOAnchorPosY(12f, 0.32f).SetEase(t.Eases.Breathe).SetLoops(-1, LoopType.Yoyo)
                .SetDelay(i * 0.12f).SetUpdate(true).SetLink(dot);
        }

        Show(false);
    }
}
