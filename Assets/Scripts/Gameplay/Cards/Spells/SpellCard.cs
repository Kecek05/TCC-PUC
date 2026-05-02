using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SpellCard : AbstractCard
{
    [Header("Spell Settings")]
    [SerializeField] private bool canUseInEnemyMap = false;
    private GhostSpellCard _ghostSpellCard;
    [Space(5f)]
    
    [Header("GFXs")] 
    [SerializeField] private MMF_Player fadeOutFeedback;
    [SerializeField] private MMF_Player fadeInFeedback;

    private bool _enabledTowerGFX = false;

    private BaseCardSpellDeployer _cardSpellDeployer;

    protected override void Start()
    {
        base.Start();
        _cardSpellDeployer = ServiceLocator.Get<BaseCardSpellDeployer>();
    }
    
    public void Initialize(CardUIFactoryData factoryData, BaseCardContainer cardContainer, GhostSpellCard ghostSpellCard)
    {
        base.Initialize(factoryData, cardContainer);
        _ghostSpellCard = ghostSpellCard;
    }

    public override void OnBeginDrag(PointerEventData eventData)
    {
        base.OnBeginDrag(eventData);
        DisableGhostSpellGFX();

        SpellCardDataSO spellCardData = GetSpellCardDataSO();
        _ghostSpellCard.SetSprite(spellCardData.SpellGhostSprite);
        
        _ghostSpellCard.SetScale(spellCardData.SpellData.Range * 2f);
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
        _cardSpellDeployer.OnSpellResult += HandleSpellResult;
        _clientManaManager.PredictSpend(cardDataSo.Cost);

        Vector2 serverPosition = ServiceLocator.Get<BaseMapTranslator>().LocalToServer(worldPosition);
        _cardSpellDeployer.RequestSpellCardServer(cardDataSo.CardType, serverPosition);
    }
    
    private void EnableGhostSpellGFX(Vector2 worldPosition)
    {
        _enabledTowerGFX = true;
        
        _ghostSpellCard.SetPosition(worldPosition);
        _ghostSpellCard.SetVisible(true);
    }

    private void DisableGhostSpellGFX()
    {
        if (!_enabledTowerGFX) return;
        AnimateFadeIn();
        _enabledTowerGFX = false;
        _ghostSpellCard.SetVisible(false);
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
            GameLog.Error($"CardDataSO: {cardDataSo.CardType} is not SpellCardDataSO");
            return null;
        }

        return spellCardData;
    }

    private void HandleSpellResult(SpellSpawnResult result)
    {
        if (!_waitingResult || result.CardType != cardDataSo.CardType) return;

        _waitingResult = false;
        _cardSpellDeployer.OnSpellResult -= HandleSpellResult;

        if (result.Validation.IsValid)
        {
            GameLog.Info("Spell result successful!");
            _clientManaManager.ConfirmSpend(cardDataSo.Cost);
            DiscardSelfCard();
            return;
        }

        switch (result.Validation.Reason)
        {
            case SpellInvalidReason.None:
                GameLog.Error("Spell failed for unknown reason.");
                _clientManaManager.RevertSpend(cardDataSo.Cost);
                break;
            case SpellInvalidReason.NotEnoughMana:
                GameLog.Info("Not enough mana to use a spell.");
                _clientManaManager.RevertSpend(cardDataSo.Cost);
                break;
            case SpellInvalidReason.NoTeam:
                GameLog.Error("Spell failed because client has no team.");
                _clientManaManager.RevertSpend(cardDataSo.Cost);
                break;
            default:
                GameLog.Error("Unhandled spell invalid reason: " + result.Validation.Reason);
                _clientManaManager.RevertSpend(cardDataSo.Cost);
                break;
        }
    }
}