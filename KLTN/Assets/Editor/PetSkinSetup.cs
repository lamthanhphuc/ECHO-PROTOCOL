using System;
using EchoProtocol.Pets;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class PetSkinSetup {
 static PetSkinSetup(){EditorApplication.delayCall+=EnsureSkins;}
 private static void EnsureSkins(){
  if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)return;
  if(AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/"+PetSkinLibrary.GetResourcePath(4,3)+".mat")==null)Build();
 }
 [MenuItem("Echo Protocol/Pets/Build Shop Skin Materials")]
 public static void Build(){
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode first");
  string[] sourceFolders={"DragonNightmare","DragonSoulEater","DragonTerrorBringer","DragonUsurper"};
  for(int pet=1;pet<=4;pet++)for(int skin=0;skin<PetSkinLibrary.SkinCount;skin++) {
   string color=PetSkinLibrary.GetColorName(pet,skin);
   string sourceColor=pet==4 && color=="Black" ? "Dark" : color;
   string sourcePath="Assets/FourEvilDragonsPBR/Materials/"+sourceFolders[pet-1]+"/"+sourceColor+"PBR.mat";
   var source=AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
   if(source==null || source.shader==null || source.shader.name!="Universal Render Pipeline/Lit")throw new InvalidOperationException("Convert source to URP first: "+sourcePath);
   string targetPath="Assets/Resources/"+PetSkinLibrary.GetResourcePath(pet,skin)+".mat";
   EnsureFolder(System.IO.Path.GetDirectoryName(targetPath).Replace('\\','/'));
   var target=AssetDatabase.LoadAssetAtPath<Material>(targetPath);
   if(target==null){target=new Material(source);AssetDatabase.CreateAsset(target,targetPath);}
   else {Undo.RecordObject(target,"Refresh pet skin");target.CopyPropertiesFromMaterial(source);}
   target.name=System.IO.Path.GetFileNameWithoutExtension(targetPath);
   EditorUtility.SetDirty(target);AssetDatabase.SaveAssetIfDirty(target);
   if(target.GetTexture("_BaseMap")==null)throw new InvalidOperationException("Missing skin albedo: "+targetPath);
   foreach(string dependency in AssetDatabase.GetDependencies(targetPath))
    if(dependency.StartsWith("Assets/") && AssetDatabase.LoadMainAssetAtPath(dependency)==null)throw new InvalidOperationException("Missing skin dependency: "+dependency);
  }
  System.IO.File.WriteAllText("Library/PetSkinsValidation.txt","PASS: 16 URP skin materials, four pet folders, valid albedo and asset dependencies. Skin 0 is the default for every pet.");
  Debug.Log("[Pets] Prepared and validated 16 shop skin materials. Existing default pet visuals are preserved.");
 }
 private static void EnsureFolder(string path){
  if(AssetDatabase.IsValidFolder(path))return;
  string parent=System.IO.Path.GetDirectoryName(path).Replace('\\','/');
  EnsureFolder(parent);AssetDatabase.CreateFolder(parent,System.IO.Path.GetFileName(path));
 }
}
