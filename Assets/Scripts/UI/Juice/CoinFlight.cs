using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Coins that arc from where a payout happened to the counter that banks it, each landing with a coin sound
/// one step higher than the last and a punch on the icon. The CurrencyCounter recipe's flight half.
/// </summary>
/// <remarks>
/// Built from plain Images under the target's root canvas, so they draw over everything on that canvas and
/// need no prefab. Unscaled time. Under Reduce motion nothing flies and only the landing callback runs.
/// </remarks>
public static class CoinFlight
{
    /// <param name="look">What flies looks like this Image: usually the target icon itself (sprite and tint).</param>
    /// <param name="fromWorld">World position the coins leave from.</param>
    /// <param name="target">The icon they land on; it is punched on every landing.</param>
    /// <param name="count">How many coins fly (capped at 12).</param>
    /// <param name="onFirstLanding">When the first coin arrives, with the time the rest still need, so a
    /// counter can roll over exactly the span of the landings.</param>
    /// <param name="onAllLanded">When the last coin arrives.</param>
    public static void Fly(Image look, Vector3 fromWorld, RectTransform target, int count,
        Action<float> onFirstLanding = null, Action onAllLanded = null)
    {
        if (target == null || Juice.ReduceMotion || look == null || look.sprite == null)
        {
            onFirstLanding?.Invoke(0f);
            onAllLanded?.Invoke();
            return;
        }

        JuiceTokensSO t = Juice.Tokens;
        Canvas canvas = target.GetComponentInParent<Canvas>();
        Canvas root = canvas != null ? canvas.rootCanvas : null;
        if (root == null)
        {
            onFirstLanding?.Invoke(0f);
            onAllLanded?.Invoke();
            return;
        }

        count = Mathf.Clamp(count, 1, 12);
        float unit = root.transform.lossyScale.y;
        float travel = t.Durations.Travel * 1.4f;
        float gap = t.Durations.Stagger * 1.5f;
        Vector2 size = target.rect.size * 0.7f;
        int landed = 0;

        for (int i = 0; i < count; i++)
        {
            GameObject coin = new GameObject("Coin", typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)coin.transform;
            rect.SetParent(root.transform, false);
            rect.sizeDelta = size;
            rect.position = fromWorld + (Vector3)(UnityEngine.Random.insideUnitCircle * 60f * unit);
            rect.localScale = Vector3.zero;

            Image image = coin.GetComponent<Image>();
            image.sprite = look.sprite;
            image.color = look.color;
            image.preserveAspect = true;
            image.raycastTarget = false;

            // The icon's drop shadow, so a coin in flight reads as the same coin it lands on.
            foreach (Shadow shadow in look.GetComponents<Shadow>())
            {
                if (shadow.GetType() != typeof(Shadow)) continue; // an Outline is a Shadow too
                Shadow copy = coin.AddComponent<Shadow>();
                copy.effectColor = shadow.effectColor;
                copy.effectDistance = shadow.effectDistance;
                break;
            }

            int step = i;
            float delay = i * gap;
            Sequence s = DOTween.Sequence().SetUpdate(true).SetLink(coin).SetDelay(delay)
                .Append(rect.DOScale(1.15f, t.Durations.Select).SetEase(t.Eases.Pop))
                .Append(rect.DOJump(target.position, UnityEngine.Random.Range(80f, 160f) * unit, 1, travel).SetEase(t.Eases.In))
                .Join(rect.DOScale(0.8f, travel).SetEase(t.Eases.In));

            s.OnComplete(() =>
            {
                UnityEngine.Object.Destroy(coin);
                Juice.Sfx(SfxId.Coin, Mathf.Min(step, 8));
                if (step % 3 == 0) Juice.Haptic(HapticRole.Select);
                JuiceFx.Punch(target, t.Scales.PunchSoft, t.Durations.Pop);

                if (landed++ == 0) onFirstLanding?.Invoke((count - 1) * gap);
                if (landed == count) onAllLanded?.Invoke();
            });
        }
    }
}
