using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Hai.ConstraintTools.Editor
{
    [InitializeOnLoad]
    public class ConstraintToolsInit
    {
        static ConstraintToolsInit()
        {
            EditorApplication.delayCall += Next;
        }

        private static void Next()
        {
            // GizmoUtility.SetIconEnabled does not appear to exist in Unity 2021
#if UNITY_2022_1_OR_NEWER
            var allConstraintToolsTypes = FindAllConstraintToolsMonoTypes();
            foreach (var prefabulousType in allConstraintToolsTypes)
            {
                GizmoUtility.SetIconEnabled(prefabulousType, false);
            }
#endif
        }

        private static Type[] FindAllConstraintToolsMonoTypes()
        {
            var allConstraintToolsTypes = AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => typeof(MonoBehaviour).IsAssignableFrom(type))
                .Where(type => type.FullName.StartsWith("Hai.ConstraintTools.Runtime."))
                .ToArray();
            return allConstraintToolsTypes;
        }
    }
}