using UnityEngine;

public static class GameUtils
{
    public static TowerType GetTowerTypeByCardType(CardType cardType)
    {
        switch (cardType)
        {
            case CardType.TowerCircle: return TowerType.Circle;
            case CardType.TowerSquare: return TowerType.Square;
            default: return TowerType.None;
        }
    }
    
    public static CardType GetCardTypeByTowerType(TowerType towerType)
    {
        switch (towerType)
        {
            case TowerType.Circle: return CardType.TowerCircle;
            case TowerType.Square: return CardType.TowerSquare;
            default: return CardType.None;
        }
    }
}
