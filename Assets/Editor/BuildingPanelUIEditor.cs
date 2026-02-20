using UnityEditor;
using UnityEngine;

namespace CityBuilderVR.Editor
{
    [CustomEditor(typeof(BuildingPanelUI))]
    [CanEditMultipleObjects]
    public class BuildingPanelUIEditor : UnityEditor.Editor
    {
        SerializedProperty m_UseQuickPrefabList;
        SerializedProperty m_AutoSyncSlotsFromQuickList;
        SerializedProperty m_OverwriteSlotNamesFromQuickList;
        SerializedProperty m_QuickPrefabList;
        SerializedProperty m_BuildingSlots;

        static readonly string[] s_ExcludedProperties =
        {
            "m_Script",
            "m_UseQuickPrefabList",
            "m_AutoSyncSlotsFromQuickList",
            "m_OverwriteSlotNamesFromQuickList",
            "m_QuickPrefabList",
            "m_BuildingSlots",
        };

        void OnEnable()
        {
            m_UseQuickPrefabList = serializedObject.FindProperty("m_UseQuickPrefabList");
            m_AutoSyncSlotsFromQuickList = serializedObject.FindProperty("m_AutoSyncSlotsFromQuickList");
            m_OverwriteSlotNamesFromQuickList = serializedObject.FindProperty("m_OverwriteSlotNamesFromQuickList");
            m_QuickPrefabList = serializedObject.FindProperty("m_QuickPrefabList");
            m_BuildingSlots = serializedObject.FindProperty("m_BuildingSlots");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawScriptReference();
            EditorGUILayout.Space(4f);
            DrawBuildingsSection();

            EditorGUILayout.Space(8f);
            DrawPropertiesExcluding(serializedObject, s_ExcludedProperties);

            serializedObject.ApplyModifiedProperties();
        }

        void DrawScriptReference()
        {
            using (new EditorGUI.DisabledScope(true))
            {
                SerializedProperty script = serializedObject.FindProperty("m_Script");
                if (script != null)
                {
                    EditorGUILayout.PropertyField(script);
                }
            }
        }

        void DrawBuildingsSection()
        {
            EditorGUILayout.LabelField("Buildings", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(m_UseQuickPrefabList, new GUIContent("Use Quick Prefab List"));
            if (m_UseQuickPrefabList.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_AutoSyncSlotsFromQuickList, new GUIContent("Auto Sync Slots"));
                EditorGUILayout.PropertyField(m_OverwriteSlotNamesFromQuickList, new GUIContent("Overwrite Slot Names"));
                EditorGUILayout.PropertyField(m_QuickPrefabList, new GUIContent("Quick Prefab List"), true);
                EditorGUI.indentLevel--;

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Sync Quick List -> Slots"))
                {
                    serializedObject.ApplyModifiedProperties();
                    RunOnTargets(panel =>
                    {
                        panel.SyncSlotsFromQuickPrefabList();
                        EditorUtility.SetDirty(panel);
                    });
                }

                if (GUILayout.Button("Build/Rebuild Panel"))
                {
                    serializedObject.ApplyModifiedProperties();
                    RunOnTargets(panel =>
                    {
                        panel.BuildPanel();
                        EditorUtility.SetDirty(panel);
                    });
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.PropertyField(m_BuildingSlots, new GUIContent("Building Slots (Advanced)"), true);
            EditorGUILayout.HelpBox(
                "Quick Prefab List te permite asignar prefabs rapido. Slots es la version avanzada (nombre + icono + prefab).",
                MessageType.Info);
        }

        void RunOnTargets(System.Action<BuildingPanelUI> action)
        {
            for (int i = 0; i < targets.Length; i++)
            {
                BuildingPanelUI panel = targets[i] as BuildingPanelUI;
                if (panel == null)
                {
                    continue;
                }

                Undo.RecordObject(panel, "Building Panel UI Change");
                action(panel);
            }
        }
    }
}
