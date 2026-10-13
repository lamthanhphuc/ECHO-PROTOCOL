using System;
using UnityEngine;
namespace EchoProtocol.Pets {
public enum PetMotion { Hidden, Idle, Walk, Run, Sleep, TakeOff, FlyIdle, FlyForward, FlyGlide, Land, IdleAlt, Jump }
[CreateAssetMenu(menuName="Echo Protocol/Pet Catalog")]
public sealed class PetCatalog : ScriptableObject {
 [Serializable] public sealed class Entry {
  public string displayName;
  public GameObject prefab;
  public RuntimeAnimatorController controller;
  public bool canFly;
  public float scale = 0.2f;
  public float groundOffset;
  public float jumpSeconds=1f;
  public float takeOffSeconds = 1f, landSeconds = 1f;
 }
 public int buildVersion;
 public Entry[] entries = new Entry[4];
 public Entry Get(int id) => id > 0 && id <= entries.Length ? entries[id-1] : null;
 public static string Name(int id) => id switch {1=>"NIGHTMARE",2=>"SOUL EATER",3=>"TERROR BRINGER",4=>"USURPER",_=>"NONE"};
}
}
