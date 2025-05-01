using System;
using System.Collections.Generic;
using System.Linq;
using Hai.ConstraintTools.Runtime;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using Object = UnityEngine.Object;
#if CONSTRAINTTOOLS_VRCHAT_CONSTRAINTS_SUPPORTED
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Constraint.Components;
#endif

namespace Hai.ConstraintTools.Editor
{
    [CustomEditor(typeof(SkinnedMeshConstraintBuilder))]
    public class SkinnedMeshConstraintBuilderEditor : UnityEditor.Editor
    {
        private const string CreateParentConstraintLabel = "Create Parent Constraint";
        private const string UpdateParentConstraintLabel = "Update Parent Constraint";
        private const string MsgBuilderCanBeRemoved = "If you're finished, you can remove this Skinned Mesh Constraint Builder.";
        private const string RemoveSkinnedMeshConstraintBuilderLabel = "Remove Skinned Mesh Constraint Builder";
        private const string SamplerOffsetLabel = "Sampler Offset";
        private const string UpdateOffsetLabel = "Update Skinned Mesh Constraint offset";

        private const bool DrawMatch = true;
        private const string MsgNotEnoughBones = "This constraint only has one bone. You should parent to that bone instead, or use a bone proxy.";
        private const string ApplySkinnedMeshConstraintLabel = "Apply Skinned Mesh Constraint";

        public static GUIStyle RedText;
        public static GUIStyle BlueText;

        private static GUIStyle MakeText(Color color)
        {
            var guiStyle = new GUIStyle(EditorStyles.label);
            guiStyle.normal.textColor = color;
            return guiStyle;
        }

        public override void OnInspectorGUI()
        {
            var my = (SkinnedMeshConstraintBuilder)target;
            var smc = my;
            Component parentConstraintNullable = smc.GetComponent<ParentConstraint>();
#if CONSTRAINTTOOLS_VRCHAT_CONSTRAINTS_SUPPORTED
            // Note: Requires VRC.Dynamics.dll to resolve that VRCParentConstraint is a MonoBehaviour
            if (parentConstraintNullable == null) parentConstraintNullable = smc.GetComponent<VRCParentConstraint>();
#endif
            
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(SkinnedMeshConstraintBuilder.sourceMesh)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(SkinnedMeshConstraintBuilder.bindMethod)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(SkinnedMeshConstraintBuilder.samplerOffset)));
            
            EditorGUI.BeginDisabledGroup(my.sourceMesh == null);
            if (GUILayout.Button(parentConstraintNullable == null ? CreateParentConstraintLabel : UpdateParentConstraintLabel))
            {
                if (parentConstraintNullable == null)
                {
#if CONSTRAINTTOOLS_VRCHAT_CONSTRAINTS_SUPPORTED
                    if (my.vendor == SkinnedMeshConstraintBuilder.SkinnedMeshConstraintVendor.Unity)
                    {
                        
                        parentConstraintNullable = Undo.AddComponent<ParentConstraint>(my.gameObject);
                    }
                    else
                    {
                        parentConstraintNullable = Undo.AddComponent<VRCParentConstraint>(my.gameObject);
                    }
#else
                    parentConstraintNullable = Undo.AddComponent<ParentConstraint>(my.gameObject);
#endif
                }
                
                ResolveConstraintConfiguration(my, parentConstraintNullable);
                EditorUtility.SetDirty(parentConstraintNullable);
            }
            EditorGUI.EndDisabledGroup();
            
            if (parentConstraintNullable == null)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(SkinnedMeshConstraintBuilder.vendor)));
            }
            else
            {
                if (!HasMultipleSources(parentConstraintNullable))
                {
                    EditorGUILayout.HelpBox(MsgNotEnoughBones, MessageType.Error);
                }
                
                EditorGUILayout.HelpBox(MsgBuilderCanBeRemoved, MessageType.Info);
                if (GUILayout.Button(RemoveSkinnedMeshConstraintBuilderLabel))
                {
                    Undo.DestroyObjectImmediate(my);
                }
            }

            // Prevents error after calling Undo.DestroyObjectImmediate
            if (serializedObject.targetObject != null)
            {
                serializedObject.ApplyModifiedProperties();
            }
        }

        private bool HasMultipleSources(Component parentConstraintNullable)
        {
            if (parentConstraintNullable is ParentConstraint unityConstraint)
            {
                return unityConstraint.sourceCount > 1;
            }
#if CONSTRAINTTOOLS_VRCHAT_CONSTRAINTS_SUPPORTED
            else if (parentConstraintNullable is VRCParentConstraint vrcConstraint)
            {
                return vrcConstraint.Sources.Count > 1;
            }
#endif

            return true;
        }

        private void OnSceneGUI()
        {
            var my = (SkinnedMeshConstraintBuilder)target;
            
            if (my.samplerOffset != Vector3.zero)
            {
                var worldOffset = my.transform.TransformPoint(my.samplerOffset);
            
                EditorGUI.BeginChangeCheck();
                var newValue = Handles.PositionHandle(worldOffset, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(my, UpdateOffsetLabel);
                    my.samplerOffset = my.transform.InverseTransformPoint(newValue);
                }
            }
        }

        [DrawGizmo(GizmoType.Active)]
        static void DrawGizmo(SkinnedMeshConstraintBuilder my, GizmoType gizmoType)
        {
            if (RedText == null)
            {
                RedText = MakeText(Color.red);
                BlueText = MakeText(Color.blue);
            }

            if (my.samplerOffset != Vector3.zero)
            {
                Gizmos.color = Color.red;
                var worldOffset = my.transform.TransformPoint(my.samplerOffset);
                Handles.Label(worldOffset, SamplerOffsetLabel, RedText);
                Gizmos.DrawWireSphere(worldOffset, 0.001f);
                
                Handles.color = Color.red;
                Handles.DrawLine(worldOffset, my.transform.position, 2f);
            }

            Component parentConstraint = my.GetComponent<ParentConstraint>();
#if CONSTRAINTTOOLS_VRCHAT_CONSTRAINTS_SUPPORTED
            if (parentConstraint == null) parentConstraint = my.GetComponent<VRCParentConstraint>();
#endif
            if (parentConstraint == null) return;

            if (parentConstraint is ParentConstraint unityConstraint)
            {
                for (var index = 0; index < unityConstraint.translationOffsets.Length; index++)
                {
                    var constraintSource = unityConstraint.GetSource(index);
                    if (constraintSource.sourceTransform != null)
                    {
                        var offset = unityConstraint.translationOffsets[index];
                        DrawFor(my.transform.position, constraintSource.sourceTransform.TransformPoint(offset), constraintSource.sourceTransform, constraintSource.weight);
                    }
                }
            }
#if CONSTRAINTTOOLS_VRCHAT_CONSTRAINTS_SUPPORTED
            else if (parentConstraint is VRCParentConstraint vrcConstraint)
            {
                var perSourcePositionOffsets = vrcConstraint.GetPerSourcePositionOffsets(); // Not the same as vrcConstraint.Sources.ParentPositionOffset
                var sources = vrcConstraint.Sources;
                for (var i = 0; i < vrcConstraint.Sources.Count; i++)
                {
                    var source = sources.ElementAt(i);
                    if (source.SourceTransform != null)
                    {
                        var offset = perSourcePositionOffsets[i];
                        DrawFor(my.transform.position, source.SourceTransform.TransformPoint(offset), source.SourceTransform, source.Weight);
                    }
                }
            }
#endif
        }

        private static void DrawFor(Vector3 ourConstraintPos, Vector3 pivotPos, Transform pivotSource, float weight)
        {
            Handles.color = Color.red;
            Handles.DrawLine(ourConstraintPos, pivotPos, 2f);
            Handles.color = Color.blue;
            Handles.DrawLine(ourConstraintPos, pivotSource.position, 2f);
            Handles.Label(pivotSource.position, $"{pivotSource.name} ({(weight * 100):0.000}%)", BlueText);
        }

        // Licensing notes:
        // Portions of the code below originally comes from portions of a proprietary software that I (Haï~) am the author of,
        // and is notably used in Starmesh/HaiMeshLib (2024).
        // The code below is released under the same terms of this HaiConstraintTools's LICENSE which is MIT,
        // including the specific portions of the code that originally came from Starmesh/HaiMeshLib.

        private void ResolveConstraintConfiguration(SkinnedMeshConstraintBuilder my, Component constraint)
        {
            var bakeMesh = new Mesh();
            var renderer = my.sourceMesh;
            
            // We're going to bake the mesh to make space conversions simpler
            // (i.e. if an A-posed avatar is being T-posed in editor then it should still work),
            // under the assumption that baking the mesh will not reorder the internal vertex data,
            // as we will need it to bind the bone weights.
            renderer.BakeMesh(bakeMesh, true);

            var pointToSampleWeightsFrom = my.transform.TransformPoint(my.samplerOffset);
            var samplePointInMeshSpace = renderer.transform.InverseTransformPoint(pointToSampleWeightsFrom);

            var boneToWeight = my.bindMethod == SkinnedMeshConstraintBuilder.SkinnedMeshConstraintBindMethod.ClosestFace
                    ? UsingBarycentricCoordinates(bakeMesh, samplePointInMeshSpace, renderer)
                    : UsingClosestVertex(bakeMesh, samplePointInMeshSpace, renderer);

            var summedWeights = boneToWeight.Values.Sum();

            var bones = renderer.bones;
            var referenceTransform = constraint.transform;
            if (constraint is ParentConstraint unityConstraint)
            {
                Undo.RecordObject(unityConstraint, ApplySkinnedMeshConstraintLabel);
                unityConstraint.constraintActive = false;
                unityConstraint.locked = false;
                while (unityConstraint.sourceCount > 0)
                    unityConstraint.RemoveSource(0);
                
                foreach (var boneIndexToWeight in boneToWeight)
                {
                    var sourceTransform = bones[boneIndexToWeight.Key];
                    if (sourceTransform != null)
                    {
                        unityConstraint.AddSource(new ConstraintSource
                        {
                            weight = boneIndexToWeight.Value / summedWeights,
                            sourceTransform = sourceTransform
                        });
                    }
                }
            
                // The Activate button does not appear to calculate the offsets in the same way as we're doing below.
                // We're trying to avoid an issue with the Activate button where calculated offsets somehow doesn't
                // match the behaviour of mesh skinning.
                // In other words, do not replace the code below with invoking the stock ActivateAndPreserveOffset function.
                var sources = new List<ConstraintSource>();
                unityConstraint.GetSources(sources);
                unityConstraint.translationOffsets = sources.Select(source => source.sourceTransform == null ? Vector3.zero : source.sourceTransform.InverseTransformPoint(referenceTransform.position)).ToArray();
                unityConstraint.rotationOffsets = sources.Select(source => source.sourceTransform == null ? Quaternion.identity.eulerAngles : (Quaternion.Inverse(source.sourceTransform.rotation) * referenceTransform.rotation).eulerAngles).ToArray();

                unityConstraint.translationAtRest = my.transform.localPosition;
                unityConstraint.rotationAtRest = my.transform.localRotation.eulerAngles;
                unityConstraint.locked = true;
                unityConstraint.constraintActive = true;
            }
#if CONSTRAINTTOOLS_VRCHAT_CONSTRAINTS_SUPPORTED
            else if (constraint is VRCParentConstraint vrcConstraint)
            {
                Undo.RecordObject(vrcConstraint, ApplySkinnedMeshConstraintLabel);
                vrcConstraint.IsActive = false;
                vrcConstraint.Locked = false;
                vrcConstraint.Sources.SetLength(boneToWeight.Count);

                var i = 0;
                foreach (var boneIndexToWeight in boneToWeight)
                {
                    var sourceTransform = bones[boneIndexToWeight.Key];
                    if (sourceTransform != null)
                    {
                        // We need 3.7.3 or higher for this to serialize properly.
                        vrcConstraint.Sources[i] = new VRCConstraintSource
                        {
                            Weight = boneIndexToWeight.Value / summedWeights,
                            SourceTransform = sourceTransform,
                            ParentPositionOffset = sourceTransform.InverseTransformPoint(referenceTransform.position),
                            ParentRotationOffset = (Quaternion.Inverse(sourceTransform.rotation) * referenceTransform.rotation).eulerAngles
                        };
                        i++;
                    }
                }
                vrcConstraint.Sources.SetLength(i); // Lazy way to handle null sourceTransform 
                
                vrcConstraint.PositionAtRest = my.transform.localPosition;
                vrcConstraint.RotationAtRest = my.transform.localRotation.eulerAngles;
                vrcConstraint.Locked = true;
                vrcConstraint.IsActive = true;
            }
#endif

            Object.DestroyImmediate(bakeMesh);
        }

        private static Dictionary<int, float> UsingBarycentricCoordinates(Mesh bakeMesh, Vector3 samplePointInMeshSpace, SkinnedMeshRenderer renderer)
        {
            var boneToWeight = new Dictionary<int, float>();
            
            // We should really be using a KD tree here, in order to pre-select the closest vertices,
            // but that would mean reimporting an entire KD tree library here, even though we're only
            // doing this operation once on demand for just once vertex, and it is not even done on
            // every avatar build. Just wing it.

            var vertices = bakeMesh.vertices;
            var triangles = bakeMesh.triangles;
            var smallestDistance = float.MaxValue;
            var smallestTriangleStartIndex = -1;
            var smallestTriangleBarycentric = Vector3.zero;
            for (var triangleStartIndex = 0; triangleStartIndex < triangles.Length; triangleStartIndex += 3)
            {
                var VecA = vertices[triangles[triangleStartIndex]];
                var VecB = vertices[triangles[triangleStartIndex + 1]];
                var VecC = vertices[triangles[triangleStartIndex + 2]];
                
                var ab = VecB - VecA;
                var ac = VecC - VecA;
                var bc = VecC - VecB;
                var triangleNormal = ForceNormalize(Vector3.Cross(ab, ac));
                var signedDistanceToPlane = Vector3.Dot(samplePointInMeshSpace - VecA, triangleNormal);
                var vertexProjectedOntoTriangle = samplePointInMeshSpace - signedDistanceToPlane * triangleNormal;
                
                float distanceToEvaluate;
                var barycentric = ToBarycentricCoordinates(VecA, VecB, VecC, vertexProjectedOntoTriangle);
                if (triangleNormal == Vector3.zero || float.IsNaN(barycentric.x) || float.IsNaN(barycentric.y) || float.IsNaN(barycentric.z))
                {
                    // If by any forsaken chance even ForceNormalize fails to return a non-zero result
                    // (probably because it might have encountered a super-degenerate triangle with colinear points),
                    // we don't want the computed distance to be zero, it would mess up everything.
                    // Reduce the chance for the caller of this function from using the triangle.
                    distanceToEvaluate = float.MaxValue;
                }
                else if (!(barycentric.x < 0) && !(barycentric.y < 0) && !(barycentric.z < 0))
                {
                    distanceToEvaluate = Mathf.Abs(signedDistanceToPlane);
                }
                else
                {
                    // Find minimum distance to any edge of the triangle
                    var distanceAgainstC = Vector3.Distance(ProjectOntoEdge(vertexProjectedOntoTriangle, VecA, ab), samplePointInMeshSpace);
                    var distanceAgainstB = Vector3.Distance(ProjectOntoEdge(vertexProjectedOntoTriangle, VecA, ac), samplePointInMeshSpace);
                    var distanceAgainstA = Vector3.Distance(ProjectOntoEdge(vertexProjectedOntoTriangle, VecB, bc), samplePointInMeshSpace);
                    distanceToEvaluate = Mathf.Min(Mathf.Min(distanceAgainstA, distanceAgainstB), distanceAgainstC);
                }

                if (distanceToEvaluate < smallestDistance)
                {
                    smallestDistance = distanceToEvaluate;
                    smallestTriangleStartIndex = triangleStartIndex;
                    smallestTriangleBarycentric = barycentric;
                }
            }

            if (smallestDistance != float.MaxValue)
            {
                var sharedMesh = renderer.sharedMesh;
                var boneCountPerVertex = sharedMesh.GetBonesPerVertex();
                var allBoneWeights = sharedMesh.GetAllBoneWeights();
                var vertexIdToStartingIndexInsideBoneWeightsArray = CalculateVertexIdToStartingIndexInsideBoneWeightsArray(boneCountPerVertex);

                if (DrawMatch)
                {
                    var vertexA = renderer.transform.TransformPoint(vertices[triangles[smallestTriangleStartIndex]]);
                    var vertexB = renderer.transform.TransformPoint(vertices[triangles[smallestTriangleStartIndex + 1]]);
                    var vertexC = renderer.transform.TransformPoint(vertices[triangles[smallestTriangleStartIndex + 2]]);
                    var samplePointInWorldSpace = renderer.transform.TransformPoint(samplePointInMeshSpace);
                    Debug.DrawLine(vertexA, vertexB, Color.magenta, 5f);
                    Debug.DrawLine(vertexC, vertexB, Color.magenta, 5f);
                    Debug.DrawLine(vertexC, vertexA, Color.magenta, 5f);
                    var sumOfPositives = Mathf.Clamp(smallestTriangleBarycentric.x, 0, float.MaxValue) + Mathf.Clamp(smallestTriangleBarycentric.y, 0, float.MaxValue) + Mathf.Clamp(smallestTriangleBarycentric.z, 0, float.MaxValue);
                    if (smallestTriangleBarycentric.x > 0f) Debug.DrawLine(samplePointInWorldSpace, vertexA, Color.Lerp(Color.black, Color.yellow, smallestTriangleBarycentric.x / sumOfPositives), 5f);
                    if (smallestTriangleBarycentric.y > 0f) Debug.DrawLine(samplePointInWorldSpace, vertexB, Color.Lerp(Color.black, Color.yellow, smallestTriangleBarycentric.y / sumOfPositives), 5f);
                    if (smallestTriangleBarycentric.z > 0f) Debug.DrawLine(samplePointInWorldSpace, vertexC, Color.Lerp(Color.black, Color.yellow, smallestTriangleBarycentric.z / sumOfPositives), 5f);
                }
                
                
                var weightsA = ReadInputBoneWeightsAsNewList(triangles[smallestTriangleStartIndex], boneCountPerVertex, vertexIdToStartingIndexInsideBoneWeightsArray, allBoneWeights);
                var weightsB = ReadInputBoneWeightsAsNewList(triangles[smallestTriangleStartIndex + 1], boneCountPerVertex, vertexIdToStartingIndexInsideBoneWeightsArray, allBoneWeights);
                var weightsC = ReadInputBoneWeightsAsNewList(triangles[smallestTriangleStartIndex + 2], boneCountPerVertex, vertexIdToStartingIndexInsideBoneWeightsArray, allBoneWeights);

                AccumulateWeights(weightsA, smallestTriangleBarycentric.x);
                AccumulateWeights(weightsB, smallestTriangleBarycentric.y);
                AccumulateWeights(weightsC, smallestTriangleBarycentric.z);

                void AccumulateWeights(List<BoneWeight1> weights, float barycentricMember)
                {
                    if (barycentricMember > 0f)
                    {
                        // barycentricMember may be greater than 1.0, but this should fine as we're dividing by the sum of the accumulated weights later.
                        foreach (var boneWeight in weights)
                        {
                            if (!boneToWeight.ContainsKey(boneWeight.boneIndex)) boneToWeight.Add(boneWeight.boneIndex, boneWeight.weight * barycentricMember);
                            else boneToWeight[boneWeight.boneIndex] += boneWeight.weight * barycentricMember;
                        }
                    }
                }
            }

            return boneToWeight;
        }

        private static int[] CalculateVertexIdToStartingIndexInsideBoneWeightsArray(NativeArray<byte> boneCountPerVertex)
        {
            var startingIndices = new List<int>();
            var anchor = 0;
            foreach (var boneCountForThatVertex in boneCountPerVertex)
            {
                startingIndices.Add(anchor);
                anchor += boneCountForThatVertex;
            }

            var idToStartingIndexInsideBoneWeightsArray = startingIndices.ToArray();
            return idToStartingIndexInsideBoneWeightsArray;
        }

        /// Calculates the barycentric coordinates.
        private static Vector3 ToBarycentricCoordinates(Vector3 VecA, Vector3 VecB, Vector3 VecC, Vector3 vertexProjectedOntoTriangle)
        {
            var v_ab = VecB - VecA;
            var v_ac = VecC - VecA;
            var v_ap = vertexProjectedOntoTriangle - VecA;
            var d_abab = Vector3.Dot(v_ab, v_ab);
            var d_abac = Vector3.Dot(v_ab, v_ac);
            var d_acac = Vector3.Dot(v_ac, v_ac);
            var d_apab = Vector3.Dot(v_ap, v_ab);
            var d_apac = Vector3.Dot(v_ap, v_ac);

            var d_n = d_abab * d_acac - d_abac * d_abac;

            var bLambda = (d_acac * d_apab - d_abac * d_apac) / d_n;
            var cLambda = (d_abab * d_apac - d_abac * d_apab) / d_n;
            var aLambda = 1 - bLambda - cLambda;

            return new Vector3(aLambda, bLambda, cLambda);
        }

        /// Normalizes a vertex, even if it is small.
        private static Vector3 ForceNormalize(Vector3 input)
        {
            var inputNormalized = input.normalized;
            
            // Vector3.normalized documentation says:
            //   "Returns a zero vector If the current vector is too small to be normalized."
            // We don't want this.
            // It makes the distance calculation between triangle and vertex return 0.0, because
            // the cross product of some triangles on a mesh are too small.
            // So, bypass this restriction by multiplying that small vector then normalizing it.
            if (inputNormalized == Vector3.zero)
            {
                return (input * 1_000_000).normalized;
            }

            return inputNormalized;
        }

        /// Project a point onto a clamped edge.
        private static Vector3 ProjectOntoEdge(Vector3 pointToProject, Vector3 leftPoint, Vector3 edgeLeftToRight)
        {
            var t01 = Mathf.Clamp01(Vector3.Dot(pointToProject - leftPoint, edgeLeftToRight) / Vector3.Dot(edgeLeftToRight, edgeLeftToRight));
            var projectedOntoEdge = leftPoint + t01 * edgeLeftToRight;
            return projectedOntoEdge;
        }

        private static Dictionary<int, float> UsingClosestVertex(Mesh bakeMesh, Vector3 samplePointInMeshSpace, SkinnedMeshRenderer renderer)
        {
            var boneToWeight = new Dictionary<int, float>();
            
            var vertices = bakeMesh.vertices;
            var smallestDistance = float.MaxValue;
            var closestVertexId = -1;
            for (var index = 0; index < vertices.Length; index++)
            {
                var pos = vertices[index];
                var distance = Vector3.Distance(pos, samplePointInMeshSpace);
                if (distance < smallestDistance)
                {
                    smallestDistance = distance;
                    closestVertexId = index;
                }
            }

            if (smallestDistance != float.MaxValue)
            {
                if (DrawMatch)
                {
                    var vertex = renderer.transform.TransformPoint(vertices[closestVertexId]);
                    var samplePointInWorldSpace = renderer.transform.TransformPoint(samplePointInMeshSpace);
                    Debug.DrawLine(samplePointInWorldSpace, vertex, Color.magenta, 5f);
                }
                
                var sharedMesh = renderer.sharedMesh;
                var boneCountPerVertex = sharedMesh.GetBonesPerVertex();
                var allBoneWeights = sharedMesh.GetAllBoneWeights();
                var vertexIdToStartingIndexInsideBoneWeightsArray = CalculateVertexIdToStartingIndexInsideBoneWeightsArray(boneCountPerVertex);

                var weightsA = ReadInputBoneWeightsAsNewList(closestVertexId, boneCountPerVertex, vertexIdToStartingIndexInsideBoneWeightsArray, allBoneWeights);

                foreach (var boneWeight in weightsA)
                {
                    if (!boneToWeight.ContainsKey(boneWeight.boneIndex)) boneToWeight.Add(boneWeight.boneIndex, boneWeight.weight);
                    else boneToWeight[boneWeight.boneIndex] += boneWeight.weight;
                }
            }

            return boneToWeight;
        }

        private static List<BoneWeight1> ReadInputBoneWeightsAsNewList(int vertexId, NativeArray<byte> boneCountPerVertex, int[] vertexIdToStartingIndexInsideBoneWeightsArray, NativeArray<BoneWeight1> allBoneWeights)
        {
            var startingIndex = vertexIdToStartingIndexInsideBoneWeightsArray[vertexId];

            var boneWeight1s = new List<BoneWeight1>();
            for (var offset = 0; offset < boneCountPerVertex[vertexId]; offset++)
            {
                var currentBoneWeight = allBoneWeights[startingIndex + offset];
                boneWeight1s.Add(currentBoneWeight);
            }

            return boneWeight1s;
        }
    }
}