using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class TowerPlacementFeedbackManager : MonoBehaviour
{
   [Title("References")]
   [SerializeField] private TowerPlacementFeedback placementPrefab;

   public static TowerPlacementFeedbackManager Instance { get; private set; }

   private readonly Dictionary<int, TowerPlacementFeedback> _feedbackById = new();  
   
   private void Awake()
   {
      Instance = this;
   }

   public void PredictSpawn(Sprite spawnSprite, Vector2 spawnPosition, int cardUniqueId)
   {
      if (_feedbackById.ContainsKey(cardUniqueId))
      {
         Debug.LogWarning($"{cardUniqueId} is already predicted");
         return;
      }
      
      TowerPlacementFeedback placement = Instantiate(placementPrefab);
      placement.transform.SetPositionAndRotation(spawnPosition, Quaternion.identity);
      
      placement.ShowGFX(spawnSprite);
      
      _feedbackById.Add(cardUniqueId, placement);
   }

   public void StopPredictSpawn(int cardUniqueId)
   {
      if (!_feedbackById.ContainsKey(cardUniqueId))
      {
         Debug.LogWarning($"{cardUniqueId} is not predicted");
         return;
      }
      
      _feedbackById.Remove(cardUniqueId, out TowerPlacementFeedback towerPlacementFeedback);
      
      towerPlacementFeedback.HideGFX();
   }
}
