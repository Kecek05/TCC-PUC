using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.EventSystems;

public class SpellCard : AbstractCard
{
    [Header("Spell Settings")]
    [SerializeField] private GhostSpellCard ghostSpellCard;
    [SerializeField] private bool canUseInEnemyMap = false;
    [Space(5f)]
    
    [Header("GFXs")] 
    [SerializeField] private MMF_Player fadeOutFeedback;
    [SerializeField] private MMF_Player fadeInFeedback;

    private bool _enabledTowerGFX = false;
    
    public override void OnBeginDrag(PointerEventData eventData)
    {
        base.OnBeginDrag(eventData);
        DisableGhostSpellGFX();

        SpellCardDataSO spellCardData = GetSpellCardDataSO();
        ghostSpellCard.SetSprite(spellCardData.SpellGhostSprite);
        
        ghostSpellCard.SetScale(spellCardData.SpellData.Range * 2f);
    }

    public override void OnDrag(PointerEventData eventData)
    {
        base.OnDrag(eventData);

        Vector2 worldPosition = GetWorldPosition(eventData);
        if ((IsEnemyMap(worldPosition) && !canUseInEnemyMap) || !CanPlayCardAtCanvas(eventData.position))
        {
            DisableGhostSpellGFX();
            return;
        }
        
        AnimateFadeOut();

        EnableGhostSpellGFX(worldPosition);
    }

    public override void OnEndDrag(PointerEventData eventData)
    {
        base.OnEndDrag(eventData);
        DisableGhostSpellGFX();
    }
    
    public override CardValidation CanPlayCardAt(Vector2 worldPosition)
    {
        var baseCheck = base.CanPlayCardAt(worldPosition);
        if (!baseCheck) return baseCheck;

        if (IsEnemyMap(worldPosition) && !canUseInEnemyMap)
            return CardValidation.Invalid(CardInvalidReason.EnemyMap);
        
        return CardValidation.Valid;
    }

    public override void ActivateCard(Vector2 worldPosition)
    {
        CardSpellDeployer.Instance.OnSpellResult += HandleSpellResult;
        ClientManaManager.Instance.PredictSpend(cardDataSo.Cost);

        Vector2 serverPosition = ServiceLocator.Get<BaseMapTranslator>().LocalToServer(worldPosition);
        CardSpellDeployer.Instance.RequestSpellCardServerRpc(cardDataSo.CardType, serverPosition);
    }
    
    private void EnableGhostSpellGFX(Vector2 worldPosition)
    {
        _enabledTowerGFX = true;
        
        ghostSpellCard.SetPosition(worldPosition);
        ghostSpellCard.SetVisible(true);
    }

    private void DisableGhostSpellGFX()
    {
        if (!_enabledTowerGFX) return;
        AnimateFadeIn();
        _enabledTowerGFX = false;
        ghostSpellCard.SetVisible(false);
    }
    
    private void AnimateFadeOut()
    {
        if (_enabledTowerGFX) return;
        fadeInFeedback?.StopFeedbacks();
        fadeOutFeedback?.PlayFeedbacks();
    }

    private void AnimateFadeIn()
    {
        if (!_enabledTowerGFX) return;
        fadeOutFeedback?.StopFeedbacks();
        fadeInFeedback?.PlayFeedbacks();
    }
    
    private SpellCardDataSO GetSpellCardDataSO()
    {
        if (cardDataSo is not SpellCardDataSO spellCardData)
        {
            Debug.LogError($"CardDataSO: {cardDataSo.CardType} is not SpellCardDataSO");
            return null;
        }

        return spellCardData;
    }

    private void HandleSpellResult(SpellSpawnResult result)
    {
        if (!_waitingResult || result.CardType != cardDataSo.CardType) return;

        _waitingResult = false;
        CardSpellDeployer.Instance.OnSpellResult -= HandleSpellResult;

        if (result.Validation.IsValid)
        {
            Debug.Log("Spell result successful!");
            ClientManaManager.Instance.ConfirmSpend(cardDataSo.Cost);
            // Destroy(gameObject);
            return;
        }

        switch (result.Validation.Reason)
        {
            case SpellInvalidReason.None:
                Debug.LogError("Spell failed for unknown reason.");
                ClientManaManager.Instance.RevertSpend(cardDataSo.Cost);
                break;
            case SpellInvalidReason.NotEnoughMana:
                Debug.Log("Not enough mana to use a spell.");
                ClientManaManager.Instance.RevertSpend(cardDataSo.Cost);
                break;
            case SpellInvalidReason.NoTeam:
                Debug.LogError("Spell failed because client has no team.");
                ClientManaManager.Instance.RevertSpend(cardDataSo.Cost);
                break;
            default:
                Debug.LogError("Unhandled spell invalid reason: " + result.Validation.Reason);
                ClientManaManager.Instance.RevertSpend(cardDataSo.Cost);
                break;
        }
    }
}