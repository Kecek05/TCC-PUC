using DG.Tweening;
using DG.Tweening.Core;
using MoreMountains.Feedbacks;
using Sirenix.OdinInspector;
using UI.Game.Shapes.ImmediateComponents;
using UnityEngine;

/// <summary>
/// Points the player at the mana bar whenever a play is refused for lack of mana — the bar shakes, flashes and
/// its counter punches — so "you can't afford that" is answered where the answer is, not only on the card.
/// </summary>
/// <remarks>
/// Listens to <see cref="AbstractCard.AnyPlayRefused"/>, which covers both the card's own drop check and a
/// server refusal, so a prediction race lands on the same feedback. The bar is a Shapes immediate-mode
/// rectangle, which Feel cannot tint, so its flash is a DOTween on its colour; the shake and the counter punch
/// are an ordinary MMF_Player. Unscaled throughout, like the hand it answers.
/// </remarks>
public class ManaFeedbackPresenter : MonoBehaviour
{
    [Title("References")]
    [Tooltip("Shakes the bar and punches the counter.")]
    [SerializeField, Required] private MMF_Player deniedFeedback;
    [Tooltip("The bar whose colour flashes.")]
    [SerializeField, Required] private RectangleImmediateUI bar;

    [Title("Flash")]
    [SerializeField] private Color deniedColor = new Color(1f, 0.28f, 0.28f, 1f);
    [SerializeField, Min(0f)] private float flashDuration = 0.35f;

    private Color _restColor;
    private Tween _flash;

    // Cached once so a refusal allocates no delegates.
    private DOGetter<Color> _getColor;
    private DOSetter<Color> _setColor;

    private void Awake()
    {
        _restColor = bar.Color;
        _getColor = () => bar.Color;
        _setColor = value => bar.Color = value;

        AbstractCard.AnyPlayRefused += HandlePlayRefused;
    }

    private void OnDestroy()
    {
        // Static event: an un-removed subscription would outlive this scene and fire into a dead canvas.
        AbstractCard.AnyPlayRefused -= HandlePlayRefused;
        _flash?.Kill();
    }

    private void HandlePlayRefused(CardInvalidReason reason)
    {
        if (reason != CardInvalidReason.NotEnoughMana) return;

        deniedFeedback.PlayFeedbacks();

        _flash?.Kill();
        bar.Color = deniedColor;
        _flash = DOTween.To(_getColor, _setColor, _restColor, flashDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);
    }
}
