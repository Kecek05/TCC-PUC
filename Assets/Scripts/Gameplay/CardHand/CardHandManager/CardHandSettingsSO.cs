using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "CardHandSettings", menuName = "Scriptable Objects/Settings/CardHandSettingsSO")]
public class CardHandSettingsSO : ScriptableObject
{
   [MinValue(1), MaxValue(4)] public int HandSize = 4;
   public int DeckSize = 12;
}
