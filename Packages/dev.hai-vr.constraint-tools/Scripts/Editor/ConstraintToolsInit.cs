using Hai.ConstraintTools.Runtime;
using UnityEditor;

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
            GizmoUtility.SetIconEnabled(typeof(SkinnedMeshConstraintBuilder), false);
#endif
        }
    }
}