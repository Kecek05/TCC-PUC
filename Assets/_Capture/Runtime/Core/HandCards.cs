#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace KeceK.Capture
{
    /// <summary>Reads the local hand the way the player sees it: the card widgets sitting on hand slots.</summary>
    public static class HandCards
    {
        /// <summary>A visible, open card of <paramref name="type"/> in the hand, or null.</summary>
        public static AbstractCard Find(CaptureContext ctx, CardType type)
        {
            BaseCardContainer hand = ctx.Hand;
            if (hand == null) return null;

            foreach (AbstractCard card in hand.CardsInHand)
            {
                if (card == null || card.CardData == null || card.CardData.CardType != type) continue;
                if (card.InteractionBlocked || !card.isActiveAndEnabled) continue;

                CanvasGroup group = card.GetComponent<CanvasGroup>();
                if (group != null && (group.alpha < 0.99f || !group.blocksRaycasts)) continue; // parked or mid-play

                return card;
            }

            return null;
        }

        /// <summary>Where a UI rect's centre is on screen, whatever kind of canvas it sits on.</summary>
        public static Vector2 ScreenPoint(RectTransform rect)
        {
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Camera cam = null;
            if (canvas != null)
            {
                Canvas root = canvas.rootCanvas;
                if (root.renderMode != RenderMode.ScreenSpaceOverlay) cam = root.worldCamera;
            }

            Vector3 world = rect.TransformPoint(rect.rect.center);
            return RectTransformUtility.WorldToScreenPoint(cam, world);
        }

        public static Vector2 ScreenPoint(Vector2 world)
        {
            Camera cam = Camera.main;
            return cam != null ? (Vector2)cam.WorldToScreenPoint(world) : Vector2.zero;
        }
    }
}
#endif
