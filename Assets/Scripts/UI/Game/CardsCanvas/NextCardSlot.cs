using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

public class NextCardSlot : MonoBehaviour
{
    [Title("References")]
    [SerializeField] private Image cardImage;

    public void SetNextCardImage(Sprite image)
    {
        cardImage.sprite = image;
    }
}
