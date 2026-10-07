using UnityEngine;

public class SpawnEnemyCard : AbstractCard
{
    private BaseCardSpawnEnemyDeployer _cardSpawnEnemyDeployer;
    private Vector2 _thrownAt;

    protected override void Start()
    {
        base.Start();
        _cardSpawnEnemyDeployer = ServiceLocator.Get<BaseCardSpawnEnemyDeployer>();
    }

    public override void ActivateCard(Vector2 worldPosition)
    {
        _cardSpawnEnemyDeployer.OnSpawnResult += HandleSpawnResult;
        _clientManaManager.PredictSpend(cardDataSo.Cost);
        _thrownAt = worldPosition;

        _cardSpawnEnemyDeployer.RequestSpawnEnemyCardServer(cardDataSo.CardType);
    }   
    
    private void HandleSpawnResult(SpawnEnemyResult result)
    {
        if (!_waitingResult || result.CardType != cardDataSo.CardType) return;
        
        _waitingResult = false;
        _cardSpawnEnemyDeployer.OnSpawnResult -= HandleSpawnResult;

        if (result.Validation.IsValid)
        {
            GameLog.Info("Spawn result successful!");
            _clientManaManager.ConfirmSpend(cardDataSo.Cost);

            // TroopSend: the throw is answered where it landed, with how many went.
            int count = cardDataSo is SpawnEnemyCardDataSO troops ? troops.SpawnCount + troops.DecoyCount : 1;
            FloatingNumbers.Say(_thrownAt, count > 1 ? $"SENT x{count}" : "SENT!", 1.5f, Juice.Tokens.Colors.Cta);
            Juice.Sfx(SfxId.Whoosh);
            RaisePlayResolved(true);
            DiscardSelfCard();
            return;
        }

        switch (result.Validation.Reason)
        {
            case CardInvalidReason.None:
                GameLog.Error("Spawn failed for unknown reason.");
                _clientManaManager.RevertSpend(cardDataSo.Cost);
                break;
            case CardInvalidReason.NotEnoughMana:
                GameLog.Info("Not enough mana to spawn enemy.");
                _clientManaManager.RevertSpend(cardDataSo.Cost);
                break;
            case CardInvalidReason.NoTeam:
                GameLog.Error("Spawn failed because client has no team.");
                _clientManaManager.RevertSpend(cardDataSo.Cost);
                break;
            default:
                GameLog.Error("Unhandled spawn invalid reason: " + result.Validation.Reason);
                _clientManaManager.RevertSpend(cardDataSo.Cost);
                break;
        }

        RaisePlayResolved(false, result.Validation.Reason);
    }
}
