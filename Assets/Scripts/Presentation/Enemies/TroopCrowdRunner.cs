using UnityEngine;

/// <summary>
/// The frame hook for <see cref="TroopCrowd"/>: built on demand by the first troop of a match, one per scene.
/// </summary>
/// <remarks>
/// Execution order 100: after <see cref="ServerEnemyMovement"/> and <see cref="ClientEnemyMovement"/> (order 0) have
/// placed every root this frame, and in Update, so <see cref="EnemyFeedbackPresenter"/>'s LateUpdate pin still wins.
/// </remarks>
[DefaultExecutionOrder(100)]
public sealed class TroopCrowdRunner : MonoBehaviour
{
    private void Update() => TroopCrowd.Tick(Time.deltaTime);
}
