using UnityEngine;

public class SpellCard : AbstractCard
{
    public override void ActivateCard(Vector2 worldPosition)
    {
        CardSpellDeployer.Instance.OnSpellResult += HandleSpellResult;
        ClientManaManager.Instance.PredictSpend(cardDataSo.Cost);

        CardSpellDeployer.Instance.RequestSpellCardServerRpc(cardDataSo.CardType);
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
