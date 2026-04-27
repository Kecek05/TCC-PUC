using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Aggregates <see cref="ICardDeployer.OnCardDeployed"/> signals from every registered
/// deployer and re-emits them through a single <see cref="OnAnyCardDeployed"/> event.
/// Listeners depend on one fan-in instead of N concrete deployer types.
/// </summary>
public class CardDeploymentBus : MonoBehaviour
{
    public event Action<CardDeployedEventArgs> OnAnyCardDeployed;

    private readonly HashSet<ICardDeployer> _registered = new();

    private void Awake()
    {
        ServiceLocator.Register<CardDeploymentBus>(this);
    }

    private void OnDestroy()
    {
        foreach (ICardDeployer deployer in _registered)
            deployer.OnCardDeployed -= TriggerAnyCardDeployed;
        _registered.Clear();
        ServiceLocator.Unregister<CardDeploymentBus>();
    }

    public void Register(ICardDeployer deployer)
    {
        if (deployer == null) return;
        if (!_registered.Add(deployer)) return;
        deployer.OnCardDeployed += TriggerAnyCardDeployed;
    }

    public void Unregister(ICardDeployer deployer)
    {
        if (deployer == null) return;
        if (!_registered.Remove(deployer)) return;
        deployer.OnCardDeployed -= TriggerAnyCardDeployed;
    }

    private void TriggerAnyCardDeployed(CardDeployedEventArgs args) => OnAnyCardDeployed?.Invoke(args);
}
