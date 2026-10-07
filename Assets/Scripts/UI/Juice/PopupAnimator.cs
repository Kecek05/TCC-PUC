using System;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Opens and closes a modal or pop-over the Juice way: the scrim fades in, the panel pops from
/// scale-panel-from on ease-pop, its children stagger in; closing is quick and quiet (to 0.92 on ease-in).
/// The Popup recipe of the Juice &amp; Feedback System.
/// </summary>
/// <remarks>Unscaled time, so it works while the tutorial freezes the world.</remarks>
public class PopupAnimator : MonoBehaviour
{
    [Title("Parts")]
    [Tooltip("What pops. Empty: this transform.")]
    [SerializeField] private RectTransform panel;

    [Tooltip("Optional dim behind the panel; faded in and out.")]
    [SerializeField] private CanvasGroup scrim;

    [Tooltip("Optional: these children stagger in after the panel pops.")]
    [SerializeField] private RectTransform staggerRoot;

    [Title("Recipe")]
    [Tooltip("Small pop-overs (the card action frame) open faster and with a pop sound; modals take dur-panel and a whoosh.")]
    [SerializeField] private bool small;

    private Sequence _sequence;

    private RectTransform Panel => panel != null ? panel : (RectTransform)transform;

    /// <summary>Wires the parts for a popup built in code.</summary>
    public void Configure(RectTransform panelRect, CanvasGroup scrimGroup, RectTransform stagger, bool isSmall)
    {
        panel = panelRect;
        scrim = scrimGroup;
        staggerRoot = stagger;
        small = isSmall;
    }

    public bool IsAnimating => _sequence != null && _sequence.IsActive() && _sequence.IsPlaying();

    public Tween Open()
    {
        JuiceTokensSO t = Juice.Tokens;
        float duration = small ? t.Durations.Select : t.Durations.Panel;

        _sequence?.Kill();
        RectTransform target = Panel;
        target.localScale = Vector3.one * (Juice.ReduceMotion ? 1f : t.Scales.PanelFrom);

        CanvasGroup panelGroup = GroupOf(target);
        panelGroup.alpha = 0f;
        panelGroup.blocksRaycasts = true;

        _sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
            .Join(panelGroup.DOFade(1f, duration * 0.6f))
            .Join(target.DOScale(1f, duration).SetEase(t.Eases.Pop));

        if (scrim != null)
        {
            scrim.alpha = 0f;
            _sequence.Join(scrim.DOFade(1f, t.Durations.Panel).SetEase(t.Eases.Out));
        }

        if (staggerRoot != null)
        {
            for (int i = 0; i < staggerRoot.childCount; i++)
            {
                if (!(staggerRoot.GetChild(i) is RectTransform child) || !child.gameObject.activeSelf) continue;
                CanvasGroup g = GroupOf(child);
                g.alpha = 0f;
                _sequence.Insert(duration * 0.35f + t.Durations.Stagger * i, g.DOFade(1f, t.Durations.Pop).SetEase(t.Eases.Out));
            }
        }

        Juice.Sfx(small ? SfxId.Pop : SfxId.Whoosh);
        return _sequence;
    }

    public Tween Close(Action onClosed = null)
    {
        JuiceTokensSO t = Juice.Tokens;
        _sequence?.Kill();

        RectTransform target = Panel;
        CanvasGroup group = GroupOf(target);

        // Leaving is final: a tap during the close must not reach a button of a popup that is going away.
        group.blocksRaycasts = false;

        _sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
            .Join(group.DOFade(0f, t.Durations.Select).SetEase(t.Eases.In))
            .Join(target.DOScale(Juice.ReduceMotion ? 1f : 0.92f, t.Durations.Select).SetEase(t.Eases.In));

        if (scrim != null) _sequence.Join(scrim.DOFade(0f, t.Durations.Select).SetEase(t.Eases.In));

        _sequence.OnComplete(() =>
        {
            target.localScale = Vector3.one;
            group.blocksRaycasts = true;
            onClosed?.Invoke();
        });
        return _sequence;
    }

    /// <summary>Finishes whatever is running at once (a tap that should not wait for the animation).</summary>
    public void Complete() => _sequence?.Complete(true);

    private static CanvasGroup GroupOf(Component c)
    {
        CanvasGroup g = c.GetComponent<CanvasGroup>();
        return g != null ? g : c.gameObject.AddComponent<CanvasGroup>();
    }
}
