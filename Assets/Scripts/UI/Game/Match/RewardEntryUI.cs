using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One tile in the end-of-match rewards row: an icon plus a quantity badge. Instantiated once per line of a
/// <see cref="Reward"/> (gold is one tile, the card another) into the <c>RewardsArea</c> grid.
/// </summary>
/// <remarks>
/// Deliberately dumb — it takes a sprite and a number and knows nothing about where they came from. That is
/// what lets the same prefab serve a match payout, and later a daily claim or a shop purchase, without
/// branching on <see cref="RewardSource"/>.
/// </remarks>
public class RewardEntryUI : MonoBehaviour
{
    [Title("References")]
    [SerializeField] private Image rewardImage;
    [SerializeField] private TextMeshProUGUI rewardQtdLabel;

    public void SetGold(int amount, Sprite coinSprite) => Apply(coinSprite, Color.white, amount);

    /// <summary>Shows the card's own icon and tint, so a reward tile reads like the card it unlocks.</summary>
    public void SetCard(CardDataSO cardData, int copies) =>
        Apply(cardData != null ? cardData.CardImage : null,
              cardData != null ? cardData.CardColor : Color.white,
              copies);

    private void Apply(Sprite sprite, Color tint, int amount)
    {
        if (rewardImage != null)
        {
            rewardImage.sprite = sprite;
            rewardImage.color = tint;

            // A null sprite on an enabled Image draws as a solid white square, which reads as a broken tile
            // rather than as missing art.
            rewardImage.enabled = sprite != null;
        }

        if (rewardQtdLabel != null) rewardQtdLabel.text = $"+{amount}";
    }
}
