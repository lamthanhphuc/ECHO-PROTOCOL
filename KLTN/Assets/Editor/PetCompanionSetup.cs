using System;
using System.Linq;
using EchoProtocol.Pets;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
public static class PetCompanionSetup {
 private const string Root="Assets/Resources/Pets";
 static PetCompanionSetup(){EditorApplication.delayCall+=AutoBuild;}
 private static void AutoBuild(){
  if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling)return;
  var existing=AssetDatabase.LoadAssetAtPath<PetCatalog>(Root+"/PetCatalog.asset");
  if(existing==null || existing.buildVersion<6) Build();
 }
 [MenuItem("Echo Protocol/Pets/Build Default Companions")]
 public static void Build(){
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode first");
  System.IO.Directory.CreateDirectory(Root);AssetDatabase.Refresh();
  var shader=Shader.Find("Universal Render Pipeline/Lit");
  if(shader==null)throw new InvalidOperationException("URP Lit shader missing");
  foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets/FourEvilDragonsPBR/Materials"})) {
   var m=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
   if(m.shader==shader)continue;
   var albedo=m.GetTexture("_MainTex");var normal=m.GetTexture("_BumpMap");
   var metallic=m.HasProperty("_MetallicGlossMap")?m.GetTexture("_MetallicGlossMap"):null;
   var spec=m.HasProperty("_SpecGlossMap")?m.GetTexture("_SpecGlossMap"):null;
   var ao=m.GetTexture("_OcclusionMap");var emission=m.GetTexture("_EmissionMap");
   var color=m.GetColor("_Color");var glow=m.GetColor("_EmissionColor");
   var bump=m.GetFloat("_BumpScale");var smooth=m.GetFloat("_Glossiness");
   bool specular=m.shader.name.Contains("Specular");
   Undo.RecordObject(m,"Convert dragon material to URP");m.shader=shader;m.shaderKeywords=Array.Empty<string>();
   m.SetTexture("_BaseMap",albedo);m.SetColor("_BaseColor",color);m.SetTexture("_BumpMap",normal);m.SetFloat("_BumpScale",bump);
   m.SetTexture("_MetallicGlossMap",metallic);m.SetTexture("_SpecGlossMap",spec);m.SetFloat("_WorkflowMode",specular?0:1);
   m.SetTexture("_OcclusionMap",ao);m.SetTexture("_EmissionMap",emission);m.SetColor("_EmissionColor",glow);m.SetFloat("_Smoothness",smooth);
   if(specular)m.EnableKeyword("_SPECULAR_SETUP");
   if(normal!=null)m.EnableKeyword("_NORMALMAP");
   if((specular?spec:metallic)!=null)m.EnableKeyword("_METALLICSPECGLOSSMAP");
   if(ao!=null)m.EnableKeyword("_OCCLUSIONMAP");if(emission!=null)m.EnableKeyword("_EMISSION");
   EditorUtility.SetDirty(m);AssetDatabase.SaveAssetIfDirty(m);
  }
  var catalog=AssetDatabase.LoadAssetAtPath<PetCatalog>(Root+"/PetCatalog.asset");
  if(catalog==null){catalog=ScriptableObject.CreateInstance<PetCatalog>();AssetDatabase.CreateAsset(catalog,Root+"/PetCatalog.asset");}
  catalog.entries=new PetCatalog.Entry[4];
  string[] models={"DragonNightmare","DragonSoulEater","DragonTerrorBringer","DragonUsurper"};
  string[] folders={"DragonNightMare","DragonSoulEater","DragonTerrorBringer","DragonUsurper"};
  string[] colors={"Green","Purple","Grey","Red"};
  string[][] clips={new[]{"idle01","walk","run","Sleep","idle02","Jump"},new[]{"Idle","Walk","Run","Sleeping","Take Off","Fly Float","Fly Forward","Fly Glide","Land"},new[]{"idle01","walk","Run","sleep","TakeOff","FlyIdle","FlyForward","FlyGlide","Landing"},new[]{"idle01","Walk","Run","Sleep","takeOff","FlyIdle","FlyForward","FlyGlide","Land"}};
  string[] states={"Idle","Walk","Run","Sleep","TakeOff","FlyIdle","FlyForward","FlyGlide","Land"};
  for(int i=0;i<4;i++){
   var controllerPath=Root+"/AC_"+models[i]+".controller";
   var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
   if(controller==null)controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
   var machine=controller.layers[0].stateMachine;
   foreach(var state in machine.states)machine.RemoveState(state.state);
   float takeoff=1,land=1,jump=1;
   for(int j=0;j<clips[i].Length;j++) {
    string path="Assets/FourEvilDragonsPBR/Animations/"+folders[i]+"/"+clips[i][j]+".fbx";
    var clip=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(x=>!x.name.StartsWith("__"));
    if(clip==null)throw new InvalidOperationException("Missing pet animation "+path);
    var state=machine.AddState(i==0 && j>=4 ? (j==4 ? "IdleAlt" : "Jump") : states[j]);state.motion=clip;if(j==0)machine.defaultState=state;
    if(i>0 && j==4)takeoff=clip.length;if(j==8)land=clip.length;if(i==0 && j==5)jump=clip.length;
   }
   EditorUtility.SetDirty(controller);AssetDatabase.SaveAssetIfDirty(controller);
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FourEvilDragonsPBR/Prefab/"+models[i]+"/"+colors[i]+".prefab");
   if(prefab==null)prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/PF_Pet_"+models[i]+".prefab");
   if(prefab==null)throw new InvalidOperationException("Missing pet prefab "+models[i]);
   var sample=UnityEngine.Object.Instantiate(prefab);sample.hideFlags=HideFlags.HideAndDontSave;
   sample.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
   var renderers=sample.GetComponentsInChildren<Renderer>();var bounds=new Bounds();bool first=true;
   foreach(var r in renderers){if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
   float scale=0.55f/Mathf.Max(0.01f,bounds.size.y);
   sample.name="PF_Pet_"+models[i];
   sample.GetComponentInChildren<Animator>().runtimeAnimatorController=controller;
   sample.GetComponentInChildren<Animator>().applyRootMotion=false;
   foreach(var c in sample.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);
   foreach(var a in sample.GetComponentsInChildren<AudioSource>()) UnityEngine.Object.DestroyImmediate(a);
   foreach(var body in sample.GetComponentsInChildren<Rigidbody>()) UnityEngine.Object.DestroyImmediate(body);
   foreach(var t in sample.GetComponentsInChildren<Transform>())t.gameObject.layer=2;
   sample.hideFlags=HideFlags.None;
   var petPrefab=PrefabUtility.SaveAsPrefabAsset(sample,Root+"/PF_Pet_"+models[i]+".prefab");
   UnityEngine.Object.DestroyImmediate(sample);
   catalog.entries[i]=new PetCatalog.Entry{displayName=PetCatalog.Name(i+1),prefab=petPrefab,controller=controller,canFly=i>0,scale=scale,groundOffset=-bounds.min.y*scale,jumpSeconds=jump,takeOffSeconds=takeoff,landSeconds=land};
  }
  catalog.buildVersion=6;
  EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);
  Validate();
  Debug.Log("[Pets] Built four default companions, animation controllers and URP materials.");
 }
 [MenuItem("Echo Protocol/Pets/Validate Default Companions")]
 public static void Validate() {
  var catalog=AssetDatabase.LoadAssetAtPath<PetCatalog>(Root+"/PetCatalog.asset");
  if(catalog==null || catalog.entries.Length!=4)throw new InvalidOperationException("Pet catalog must contain four entries");
  int states=0;
  foreach(var entry in catalog.entries) {
   if(entry.prefab==null || entry.controller==null || entry.scale<=0 || entry.scale>1 || Mathf.Abs(entry.groundOffset)>1)throw new InvalidOperationException("Invalid pet entry "+entry.displayName);
   if(entry.prefab.GetComponentsInChildren<Collider>(true).Length!=0 || entry.prefab.GetComponentsInChildren<AudioSource>(true).Length!=0 || entry.prefab.GetComponentsInChildren<Rigidbody>(true).Length!=0)throw new InvalidOperationException("Pet must be cosmetic only");
   var controller=(AnimatorController)entry.controller;
   foreach(var child in controller.layers[0].stateMachine.states) {
    if(child.state.motion==null)throw new InvalidOperationException("Missing animation "+child.state.name);
    states++;
   }
   if(controller.layers[0].stateMachine.states.Length!=(entry.canFly?9:6))throw new InvalidOperationException("Missing pet states");
   foreach(var renderer in entry.prefab.GetComponentsInChildren<Renderer>(true))
    foreach(var material in renderer.sharedMaterials)
     if(material==null || material.shader.name!="Universal Render Pipeline/Lit" || material.GetTexture("_BaseMap")==null)throw new InvalidOperationException("Invalid URP pet material");
  }
  System.IO.File.WriteAllText("Library/PetsValidation.txt","PASS: 4 cosmetic prefabs, 33 animation states, URP materials and base textures. States="+states);
  Debug.Log("[Pets] Validation PASS: four cosmetic prefabs and "+states+" animation states.");
 }

}
