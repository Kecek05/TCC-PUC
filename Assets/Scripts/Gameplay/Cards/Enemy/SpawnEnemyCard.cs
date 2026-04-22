using UnityEngine;

public class SpawnEnemyCard : AbstractCard
{
    private BaseCardSpawnEnemyDeployer _cardSpawnEnemyDeployer;

    protected override void Start()
    {
        base.Start();
        _cardSpawnEnemyDeployer = ServiceLocator.Get<BaseCardSpawnEnemyDeployer>();
    }

    public override void ActivateCard(Vector2 worldPosition)
    {
        _cardSpawnEnemyDeployer.OnSpawnResult += HandleSpawnResult;
        _clientManaManager.PredictSpend(cardDataSo.Cost);

        _cardSpawnEnemyDeployer.RequestSpawnEnemyCardServer(cardDataSo.CardType);
    }   
    
    private void HandleSpawnResult(SpawnEnemyResult result)
    {
        if (!_waitingResult || result.CardType != cardDataSo.CardType) return;
        
        _waitingResult = false;
        _cardSpawnEnemyDeployer.OnSpawnResult -= HandleSpawnResult;

        if (result.Validation.IsValid)
        {
            Debug.Log("Spawn result successful!");
            _clientManaManager.ConfirmSpend(cardDataSo.Cost);
            // Destroy(gameObject);
            return;
        }
        
        switch (result.Validation.Reason)
        {
            case CardInvalidReason.None:
                Debug.LogError("Spawn failed for unknown reason.");
                _clientManaManager.RevertSpend(cardDataSo.Cost);
                break;
            case CardInvalidReason.NotEnoughMana:
                Debug.Log("Not enough mana to spawn enemy.");
                _clientManaManager.RevertSpend(cardDataSo.Cost);
                break;
            case CardInvalidReason.NoTeam:
                Debug.LogError("Spawn failed because client has no team.");
                _clientManaManager.RevertSpend(cardDataSo.Cost);
                break;
            default:
                Debug.LogError("Unhandled spawn invalid reason: " + result.Validation.Reason);
                _clientManaManager.RevertSpend(cardDataSo.Cost);
                break;
        }
    }
}
