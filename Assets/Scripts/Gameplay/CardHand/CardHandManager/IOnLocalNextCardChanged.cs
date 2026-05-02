using System;

public interface IOnLocalNextCardChanged
{
    public event Action<CardType> OnLocalNextCardChanged;
}
