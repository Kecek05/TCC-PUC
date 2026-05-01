using UnityEngine;
using UnityEngine.UI;

public abstract class BaseCardSubFactory : MonoBehaviour
{
    public abstract bool CanHandle(CardDataSO data);

    public abstract AbstractCard Create(CardUIFactoryData factoryData, CardDataSO cardDataSO);
}
