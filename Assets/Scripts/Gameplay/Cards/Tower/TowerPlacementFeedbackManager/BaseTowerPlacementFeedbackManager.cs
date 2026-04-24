using UnityEngine;

public abstract class BaseTowerPlacementFeedbackManager : MonoBehaviour
{
    public abstract void PredictSpawn(Sprite spawnSprite, Vector2 spawnPosition, int cardUniqueId);

    public abstract void StopPredictSpawn(int cardUniqueId);
}
