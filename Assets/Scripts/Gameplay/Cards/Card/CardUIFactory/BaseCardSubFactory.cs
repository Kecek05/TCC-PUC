using UnityEngine;
using UnityEngine.UI;

public abstract class BaseCardSubFactory : MonoBehaviour
{
    public abstract bool CanHandle(CardDataSO data);

    public abstract AbstractCard Create(
        CardDataSO data,
        Transform parent,
        Canvas canvas,
        Transform safeArea,
        GraphicRaycaster blockingRaycaster);
}
