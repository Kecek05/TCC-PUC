using System;

public interface IOnDrawACard
{
    public event Action<CardType> OnDrawACard;
}
