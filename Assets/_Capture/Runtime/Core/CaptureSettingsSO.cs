#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;

namespace KeceK.Capture
{
    /// <summary>
    /// The game data a capture reads but never writes: the card list (to name a tower by its card) and the
    /// enemy list (to stage a crowd). Referenced by every shot and match script.
    /// </summary>
    [CreateAssetMenu(fileName = "CaptureSettings", menuName = "Kecek/Capture/Settings")]
    public class CaptureSettingsSO : ScriptableObject
    {
        public CardDataListSO Cards;
        public EnemyDataListSO Enemies;

        [Tooltip("Seconds the bot waits for a second human before taking the slot, during a capture only. " +
                 "The asset's own value is put back when the capture ends.")]
        public float BotFillSeconds = 1f;

        private Dictionary<TowerDataSO, CardType> _towerCards;

        public CardDataSO Card(CardType type) => Cards != null ? Cards.GetCardDataByType(type) : null;

        public int Cost(CardType type)
        {
            CardDataSO card = Card(type);
            return card != null ? card.Cost : 0;
        }

        /// <summary>Which card built this tower: its data asset is the prefab's own.</summary>
        public CardType CardOf(TowerManager tower)
        {
            if (tower == null || tower.Data == null) return CardType.None;

            if (_towerCards == null)
            {
                _towerCards = new Dictionary<TowerDataSO, CardType>();
                if (Cards != null)
                {
                    foreach (CardDataSO card in Cards.CardDataList)
                    {
                        if (card is not TowerCardDataSO towerCard || towerCard.TowerPrefab == null) continue;
                        TowerManager prefab = towerCard.TowerPrefab.GetComponentInChildren<TowerManager>(true);
                        if (prefab != null && prefab.Data != null) _towerCards[prefab.Data] = card.CardType;
                    }
                }
            }

            return _towerCards.TryGetValue(tower.Data, out CardType type) ? type : CardType.None;
        }

        public EnemyDataSO Enemy(EnemyType type) => Enemies != null ? Enemies.GetEnemyDataByType(type) : null;
    }
}
#endif
