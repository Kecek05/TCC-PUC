using System;

public interface IMaxManaProvider
{
    float GetMaxMana(TeamType teamType);
    event Action<TeamType, float> OnMaxManaChanged;
}
