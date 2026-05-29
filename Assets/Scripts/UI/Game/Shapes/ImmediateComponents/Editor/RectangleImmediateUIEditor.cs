using Shapes;
using UnityEditor;
using UnityEngine;

namespace UI.Game.Shapes.ImmediateComponents.Editor
{
    [CustomEditor(typeof(RectangleImmediateUI))]
    [CanEditMultipleObjects]
    public class RectangleImmediateUIEditor : UnityEditor.Editor
    {
        DashStyleEditor _dashEditor;

        void OnEnable()
        {
            SerializedProperty propDashStyle              = serializedObject.FindProperty("dashStyle");
            SerializedProperty propMatchDashSpacingToSize = serializedObject.FindProperty("matchDashSpacingToSize");
            SerializedProperty propDashed                 = serializedObject.FindProperty("dashed");

            _dashEditor = DashStyleEditor.GetDashEditor(
                propDashStyle,
                propMatchDashSpacingToSize,
                propDashed);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty prop = serializedObject.GetIterator();
            prop.NextVisible(true); // skip m_Script

            bool dashBlockDrawn = false;
            while (prop.NextVisible(false))
            {
                if (IsDashField(prop.name))
                {
                    if (!dashBlockDrawn)
                    {
                        _dashEditor.DrawProperties();
                        dashBlockDrawn = true;
                    }
                    continue;
                }

                EditorGUILayout.PropertyField(prop, true);
            }

            serializedObject.ApplyModifiedProperties();
        }

        static bool IsDashField(string name) =>
            name == "dashed" || name == "matchDashSpacingToSize" || name == "dashStyle";
    }
}
