using UnityEngine;

namespace Hai.ConstraintTools.Runtime
{
    [AddComponentMenu("Haï/Skinned Mesh Constraint Builder")]
    [HelpURL("https://docs.hai-vr.dev/redirect/components/SkinnedMeshConstraintBuilder")]
    public class SkinnedMeshConstraintBuilder : MonoBehaviour
    #if CONSTRAINTTOOLS_VRCHAT_IS_INSTALLED
        , VRC.SDKBase.IEditorOnly
    #endif
    {
        public SkinnedMeshRenderer sourceMesh;
        public SkinnedMeshConstraintBindMethod bindMethod;
        public SkinnedMeshConstraintVendor vendor;
        public Vector3 samplerOffset = Vector3.zero;

        public enum SkinnedMeshConstraintBindMethod
        {
            ClosestFace, ClosestVertex
        }

        public enum SkinnedMeshConstraintVendor
        {
            Default, Unity, Specific
        }
    }
}
