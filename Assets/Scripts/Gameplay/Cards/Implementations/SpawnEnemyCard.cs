using UnityEngine;

public class SpawnEnemyCard : AbstractCard
{
    private bool _waitingResult;
    
    public override CardValidation CanPlayCard()
    {
        if (_waitingResult) return CardValidation.Invalid(CardInvalidReason.WaitingForServer);

        return base.CanPlayCard();
    }
    
    public override void ActivateCard(Vector2 worldPosition)
    {
        _waitingResult = true;
        CardSpawnEnemyDeployer.Instance.OnSpawnResult += HandleSpawnResult;
        ClientManaManager.Instance.PredictSpend(cardDataSo.Cost);

        CardSpawnEnemyDeployer.Instance.RequestSpawnEnemyCardServerRpc(cardDataSo.CardType);
    }   
    
    private void HandleSpawnResult(SpawnEnemyResult result)
    {
        if (!_waitingResult || result.CardType != cardDataSo.CardType) return;
        
        _waitingResult = false;
        CardSpawnEnemyDeployer.Instance.OnSpawnResult -= HandleSpawnResult;

        if (result.Validation.IsValid)
        {
            Debug.Log("Spawn result successful!");
            ClientManaManager.Instance.ConfirmSpend(cardDataSo.Cost);
            Destroy(gameObject);
            return;
        }
        
        switch (result.Validation.Reason)
        {
            case CardInvalidReason.None:
                Debug.LogError("Spawn failed for unknown reason.");
                ClientManaManager.Instance.RevertSpend(cardDataSo.Cost);
                break;
            case CardInvalidReason.NotEnoughMana:
                Debug.Log("Not enough mana to spawn enemy.");
                ClientManaManager.Instance.RevertSpend(cardDataSo.Cost);
                break;
            case CardInvalidReason.NoTeam:
                Debug.LogError("Spawn failed because client has no team.");
                ClientManaManager.Instance.RevertSpend(cardDataSo.Cost);
                break;
            default:
                Debug.LogError("Unhandled spawn invalid reason: " + result.Validation.Reason);
                ClientManaManager.Instance.RevertSpend(cardDataSo.Cost);
                break;
        }
    }
}
