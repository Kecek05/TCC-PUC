using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class SpawnEnemyCard : AbstractCard
{
    private BaseCardSpawnEnemyDeployer _cardSpawnEnemyDeployer;
    private Vector2 _thrownAt;

    // TroopSend, while held: over the board the card's face steps back and its troops take over (TroopHoldVisual),
    // the way a tower card hands over to its ghost and a spell to its aim.
    private TroopHoldVisual _hold;
    private CanvasGroup _faceGroup;
    private Tween _faceFade;

    protected override void Start()
    {
        base.Start();
        _cardSpawnEnemyDeployer = ServiceLocator.Get<BaseCardSpawnEnemyDeployer>();
    }

    private void OnDestroy()
    {
        _faceFade?.Kill();
    }

    public override void OnDrag(PointerEventData eventData)
    {
        base.OnDrag(eventData);

        // Over the hand the card is still a card: letting go there is a cancel.
        SetHeldOverBoard(CanPlayCardAtCanvas(eventData.position).IsValid);
    }

    public override void OnEndDrag(PointerEventData eventData)
    {
        // Decided before the base class acts: on the host the server's answer can arrive, and retire this card,
        // inside base.OnEndDrag. The same two checks it runs, so the troops only leave when the play is sent.
        bool sent = _hold != null && _hold.IsShown
                    && CanPlayCardAtCanvas(eventData.position).IsValid
                    && CanPlayCardAt(GetWorldPosition(eventData)).IsValid;

        if (sent)
        {
            // The visual leaves the card to fly off; a refused play that brings the card back builds a new one.
            _hold.Launch();
            _hold = null;
            ShowFace(true);
        }
        else
        {
            SetHeldOverBoard(false);
        }

        base.OnEndDrag(eventData);
    }

    private void SetHeldOverBoard(bool overBoard)
    {
        if (overBoard)
        {
            if (_hold == null) _hold = BuildHoldVisual();
            if (_hold == null || _hold.IsShown) return;

            _hold.Show();
            ShowFace(false);
            Juice.Sfx(SfxId.Tick, 2, 0.6f);
            return;
        }

        if (_hold == null || !_hold.IsShown) return;
        _hold.Hide();
        ShowFace(true);
    }

    private TroopHoldVisual BuildHoldVisual()
    {
        if (gfxController == null || cardDataSo == null || cardDataSo.CardImage == null) return null;

        // Beside the card's face, under the same animated root: it leans and grows with the drag, and the face
        // can fade without taking it along.
        RectTransform parent = gfxController.transform.parent as RectTransform;
        if (parent == null) return null;

        int count = cardDataSo is SpawnEnemyCardDataSO troops ? troops.SpawnCount + troops.DecoyCount : 1;
        return TroopHoldVisual.Create(parent, cardDataSo, count, gfxController.GetComponentInChildren<TMP_Text>(true));
    }

    /// <summary>The card's own face, cost and art: out while its troops are up, back when they fold away.</summary>
    private void ShowFace(bool visible)
    {
        if (gfxController == null) return;

        if (_faceGroup == null)
        {
            _faceGroup = gfxController.GetComponent<CanvasGroup>();
            if (_faceGroup == null) _faceGroup = gfxController.gameObject.AddComponent<CanvasGroup>();
        }

        JuiceTokensSO t = Juice.Tokens;
        _faceFade?.Kill();
        _faceFade = _faceGroup.DOFade(visible ? 1f : 0f, t.Durations.Select)
            .SetEase(visible ? t.Eases.Out : t.Eases.In).SetUpdate(true).SetLink(gameObject);
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
