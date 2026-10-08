using UnityEngine;
namespace EchoProtocol.Pets {
// Stable shop IDs: pet 1..4, skin 0..3. Skin 0 is the current default.
public static class PetSkinLibrary {
 public const int SkinCount = 4;
 private static readonly string[] Folders={"Nightmare","SoulEater","TerrorBringer","Usurper"};
 private static readonly string[][] Colors={
  new[]{"Green","Albino","Blue","Red"},
  new[]{"Purple","Blue","Green","Red"},
  new[]{"Grey","Albino","Green","Red"},
  new[]{"Red","Albino","Black","Blue"}
 };
 public static string GetColorName(int petId,int skinId) => petId>=1 && petId<=4 && skinId>=0 && skinId<SkinCount ? Colors[petId-1][skinId] : null;
 public static string GetResourcePath(int petId,int skinId) {
  var color=GetColorName(petId,skinId);
  return color==null ? null : "Pets/Skins/"+Folders[petId-1]+"/M_Pet_"+Folders[petId-1]+"_"+color;
 }
 public static Material Load(int petId,int skinId) {
  var path=GetResourcePath(petId,skinId);
  return path==null ? null : Resources.Load<Material>(path);
 }
}
}
