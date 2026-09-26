//
//  Outline.cs
//  QuickOutline (Universal Render Pipeline Compatible)
//
//  Created by Chris Nolet on 3/30/18.
//  Copyright © 2018 Chris Nolet. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace QuickOutline
{
[DisallowMultipleComponent]
[ExecuteAlways]
public class Outline : MonoBehaviour {
  private static HashSet<Mesh> registeredMeshes = new HashSet<Mesh>();

  public enum Mode {
    OutlineAll,
    OutlineVisible,
    OutlineHidden,
    OutlineAndSilhouette,
    SilhouetteOnly
  }

  public Mode OutlineMode {
    get { return outlineMode; }
    set {
      outlineMode = value;
      needsUpdate = true;
    }
  }

  public Color OutlineColor {
    get { return outlineColor; }
    set {
      outlineColor = value;
      needsUpdate = true;
    }
  }

  public float OutlineWidth {
    get { return outlineWidth; }
    set {
      outlineWidth = value;
      needsUpdate = true;
    }
  }

  [Serializable]
  private class ListVector3 {
    public List<Vector3> data;
  }

  [SerializeField]
  private Mode outlineMode = Mode.OutlineAll;

  [ColorUsage(true, true)]
  [SerializeField]
  private Color outlineColor = Color.white;

  [SerializeField, Range(0f, 10f)]
  private float outlineWidth = 2f;

  [Header("Optional")]

  [SerializeField, Tooltip("Precompute enabled: Per-vertex calculations are performed in the editor and serialized with the object. "
  + "Precompute disabled: Per-vertex calculations are performed at runtime in Awake(). This may cause a pause for large meshes.")]
  private bool precomputeOutline;

  [SerializeField, HideInInspector]
  private List<Mesh> bakeKeys = new List<Mesh>();

  [SerializeField, HideInInspector]
  private List<ListVector3> bakeValues = new List<ListVector3>();

  private Renderer[] renderers;
  private Material outlineMaskMaterial;
  private Material outlineFillMaterial;

  private bool needsUpdate;

  void Awake() {
    InitMaterials();
  }

  void InitMaterials() {
    // Cache renderers, filtering out UI Canvas elements
    renderers = GetComponentsInChildren<Renderer>(true)
      .Where(r => r != null && r.GetComponentInParent<Canvas>() == null)
      .ToArray();

    // Instantiate outline materials
    if (outlineMaskMaterial == null) {
      var maskPreset = Resources.Load<Material>(@"Materials/OutlineMask");
      if (maskPreset != null) {
        outlineMaskMaterial = Instantiate(maskPreset);
        outlineMaskMaterial.name = "OutlineMask (Instance)";
      }
    }

    if (outlineFillMaterial == null) {
      var fillPreset = Resources.Load<Material>(@"Materials/OutlineFill");
      if (fillPreset != null) {
        outlineFillMaterial = Instantiate(fillPreset);
        outlineFillMaterial.name = "OutlineFill (Instance)";
      }
    }

    // Retrieve or generate smooth normals safely
    LoadSmoothNormals();

    // Apply material properties immediately
    needsUpdate = true;
  }

  void OnEnable() {
    if (outlineMaskMaterial == null || outlineFillMaterial == null) {
      InitMaterials();
    }

    if (renderers == null || renderers.Length == 0) {
      renderers = GetComponentsInChildren<Renderer>(true)
        .Where(r => r != null && r.GetComponentInParent<Canvas>() == null)
        .ToArray();
    }

    foreach (var renderer in renderers) {
      if (renderer == null) continue;

      // Append outline shaders
      var materials = renderer.sharedMaterials.ToList();

      if (outlineMaskMaterial != null && !materials.Contains(outlineMaskMaterial)) {
        materials.Add(outlineMaskMaterial);
      }
      if (outlineFillMaterial != null && !materials.Contains(outlineFillMaterial)) {
        materials.Add(outlineFillMaterial);
      }

      renderer.materials = materials.ToArray();
    }

    UpdateMaterialProperties();
  }

  void OnValidate() {
    // Update material properties
    needsUpdate = true;

    // Clear cache when baking is disabled or corrupted
    if (!precomputeOutline && bakeKeys.Count != 0 || bakeKeys.Count != bakeValues.Count) {
      bakeKeys.Clear();
      bakeValues.Clear();
    }

    // Generate smooth normals when baking is enabled
    if (precomputeOutline && bakeKeys.Count == 0) {
      Bake();
    }

    UpdateMaterialProperties();
  }

  void Update() {
    if (needsUpdate) {
      needsUpdate = false;
      UpdateMaterialProperties();
    }
  }

  void OnDisable() {
    if (renderers == null) return;

    foreach (var renderer in renderers) {
      if (renderer == null) continue;

      // Remove outline shaders
      var materials = renderer.sharedMaterials.ToList();

      materials.Remove(outlineMaskMaterial);
      materials.Remove(outlineFillMaterial);

      renderer.materials = materials.ToArray();
    }
  }

  void OnDestroy() {
    if (outlineMaskMaterial != null) {
      if (Application.isPlaying) Destroy(outlineMaskMaterial);
      else DestroyImmediate(outlineMaskMaterial);
      outlineMaskMaterial = null;
    }
    if (outlineFillMaterial != null) {
      if (Application.isPlaying) Destroy(outlineFillMaterial);
      else DestroyImmediate(outlineFillMaterial);
      outlineFillMaterial = null;
    }
  }

  void Bake() {
    // Generate smooth normals for each mesh
    var bakedMeshes = new HashSet<Mesh>();

    foreach (var meshFilter in GetComponentsInChildren<MeshFilter>(true)) {
      if (meshFilter == null || meshFilter.sharedMesh == null) continue;

      // Skip duplicates
      if (!bakedMeshes.Add(meshFilter.sharedMesh)) {
        continue;
      }

      // Serialize smooth normals safely
      var smoothNormals = SmoothNormals(meshFilter.sharedMesh);
      if (smoothNormals != null) {
        bakeKeys.Add(meshFilter.sharedMesh);
        bakeValues.Add(new ListVector3() { data = smoothNormals });
      }
    }
  }

  void LoadSmoothNormals() {
    // Retrieve or generate smooth normals
    foreach (var meshFilter in GetComponentsInChildren<MeshFilter>(true)) {
      if (meshFilter == null || meshFilter.sharedMesh == null) continue;

      // Skip if smooth normals have already been adopted
      if (!registeredMeshes.Add(meshFilter.sharedMesh)) {
        continue;
      }

      try {
        if (!meshFilter.sharedMesh.isReadable) continue;

        // Retrieve or generate smooth normals
        var index = bakeKeys.IndexOf(meshFilter.sharedMesh);
        var smoothNormals = (index >= 0) ? bakeValues[index].data : SmoothNormals(meshFilter.sharedMesh);

        if (smoothNormals != null && smoothNormals.Count > 0) {
          // Store smooth normals in UV3
          meshFilter.sharedMesh.SetUVs(3, smoothNormals);
        }

        // Combine submeshes
        var renderer = meshFilter.GetComponent<Renderer>();
        if (renderer != null) {
          CombineSubmeshes(meshFilter.sharedMesh, renderer.sharedMaterials);
        }
      } catch {
        // Fallback for non-readable meshes
      }
    }

    // Clear UV3 on skinned mesh renderers
    foreach (var skinnedMeshRenderer in GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
      if (skinnedMeshRenderer == null || skinnedMeshRenderer.sharedMesh == null) continue;

      if (!registeredMeshes.Add(skinnedMeshRenderer.sharedMesh)) {
        continue;
      }

      try {
        if (!skinnedMeshRenderer.sharedMesh.isReadable) continue;

        skinnedMeshRenderer.sharedMesh.uv4 = new Vector2[skinnedMeshRenderer.sharedMesh.vertexCount];
        CombineSubmeshes(skinnedMeshRenderer.sharedMesh, skinnedMeshRenderer.sharedMaterials);
      } catch {
        // Fallback
      }
    }
  }

  List<Vector3> SmoothNormals(Mesh mesh) {
    if (mesh == null) return null;

    try {
      if (!mesh.isReadable) {
        return mesh.normals != null ? new List<Vector3>(mesh.normals) : null;
      }

      var vertices = mesh.vertices;
      var normals = mesh.normals;
      if (vertices == null || normals == null || vertices.Length == 0) return null;

      // Group vertices by location
      var groups = vertices.Select((vertex, index) => new KeyValuePair<Vector3, int>(vertex, index)).GroupBy(pair => pair.Key);

      // Copy normals to a new list
      var smoothNormals = new List<Vector3>(normals);

      // Average normals for grouped vertices
      foreach (var group in groups) {
        var smoothNormal = Vector3.zero;

        foreach (var pair in group) {
          smoothNormal += smoothNormals[pair.Value];
        }

        smoothNormal.Normalize();

        // Assign smooth normal to each vertex
        foreach (var pair in group) {
          smoothNormals[pair.Value] = smoothNormal;
        }
      }

      return smoothNormals;
    } catch {
      return mesh.normals != null ? new List<Vector3>(mesh.normals) : null;
    }
  }

  void CombineSubmeshes(Mesh mesh, Material[] materials) {
    if (mesh == null || materials == null) return;

    try {
      if (!mesh.isReadable) return;

      // Skip meshes with a single submesh
      if (mesh.subMeshCount == 1) {
        return;
      }

      // Skip if submesh count exceeds material count
      if (mesh.subMeshCount > materials.Length) {
        return;
      }

      // Append combined submesh
      mesh.subMeshCount++;
      mesh.SetTriangles(mesh.triangles, mesh.subMeshCount - 1);
    } catch {
      // Ignore if mesh triangles cannot be modified
    }
  }

  public void UpdateMaterialProperties() {
    if (outlineFillMaterial == null || outlineMaskMaterial == null) return;

    // Apply properties according to mode
    outlineFillMaterial.SetColor("_OutlineColor", outlineColor);

    switch (outlineMode) {
      case Mode.OutlineAll:
        outlineMaskMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
        outlineFillMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
        outlineFillMaterial.SetFloat("_OutlineWidth", outlineWidth);
        break;

      case Mode.OutlineVisible:
        outlineMaskMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
        outlineFillMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
        outlineFillMaterial.SetFloat("_OutlineWidth", outlineWidth);
        break;

      case Mode.OutlineHidden:
        outlineMaskMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
        outlineFillMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Greater);
        outlineFillMaterial.SetFloat("_OutlineWidth", outlineWidth);
        break;

      case Mode.OutlineAndSilhouette:
        outlineMaskMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
        outlineFillMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
        outlineFillMaterial.SetFloat("_OutlineWidth", outlineWidth);
        break;

      case Mode.SilhouetteOnly:
        outlineMaskMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
        outlineFillMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Greater);
        outlineFillMaterial.SetFloat("_OutlineWidth", 0f);
        break;
    }
  }
}
}
