using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Every line the tutorial says, keyed by <see cref="TutorialStepId"/>. One table so the whole script can
/// be read and rewritten in one place without recompiling — the step classes own <i>when</i> a line shows
/// and what completes it, this owns <i>what it says</i>.
/// </summary>
[CreateAssetMenu(fileName = "TutorialCopy", menuName = "Scriptable Objects/Tutorial/TutorialCopySO")]
public class TutorialCopySO : ScriptableObject
{
    [System.Serializable]
    public struct Line
    {
        [TableColumnWidth(150, Resizable = false)]
        public TutorialStepId Step;

        [TextArea(2, 4)] public string Text;
    }

    [TableList(AlwaysExpanded = true)]
    [InfoBox("A step with no entry here shows no text box — which is the right behaviour for a step that " +
             "is pure highlight, not a wrong one.")]
    [SerializeField] private List<Line> lines = new();

    private Dictionary<TutorialStepId, string> _lookup;

    private void OnEnable() => _lookup = null;

    public string Get(TutorialStepId step)
    {
        _lookup ??= BuildLookup();
        return _lookup.TryGetValue(step, out string text) ? text : string.Empty;
    }

    private Dictionary<TutorialStepId, string> BuildLookup()
    {
        Dictionary<TutorialStepId, string> map = new(lines.Count);
        foreach (Line line in lines) map[line.Step] = line.Text;
        return map;
    }

#if UNITY_EDITOR
    [Button(ButtonSizes.Medium), PropertyOrder(-1)]
    [Tooltip("Adds an empty row for every TutorialStepId this table is still missing, in enum order.")]
    private void PopulateMissing()
    {
        foreach (TutorialStepId step in System.Enum.GetValues(typeof(TutorialStepId)))
        {
            if (step == TutorialStepId.None) continue;
            if (lines.Exists(l => l.Step == step)) continue;

            lines.Add(new Line { Step = step, Text = string.Empty });
        }

        lines.Sort((a, b) => a.Step.CompareTo(b.Step));
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
