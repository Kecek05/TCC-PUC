using System;
using DG.Tweening;
using Shapes;
using Sirenix.OdinInspector;
using UnityEngine;

public class HandShapeAnimator : MonoBehaviour
{
    [Title("References")]
    [SerializeField] private Rectangle handShape;

    private void Start()
    {
        DOTween.To(() => handShape.DashOffset, x => handShape.DashOffset = x, 2f, 5f).SetLoops(-1).SetEase(Ease.Linear);
    }
}
