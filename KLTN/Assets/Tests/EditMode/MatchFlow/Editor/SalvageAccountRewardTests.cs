using NUnit.Framework;
using UnityEngine;
using EchoProtocol.Salvage;
using EchoProtocol.Networking.Authority;
public class SalvageAccountRewardTests
{
 [TestCase(1,1,false,0,600,0,0)]
 [TestCase(1,1,true,600,600,0,1)]
 [TestCase(1,3,true,599,600,0,3)]
 [TestCase(1,3,true,600,600,1,3)]
 [TestCase(2,1,false,0,900,1,3)]
 [TestCase(2,3,true,900,900,2,6)]
 public void OnlyCompletedThreeDayLevelsEarnRewards(int level,int day,bool completed,int sold,int quota,int expectedLevels,int expectedDays)
 {
  Assert.That(SalvageLedger.CompletedLevelsFor(level,day,completed,sold,quota),Is.EqualTo(expectedLevels));
  Assert.That(SalvageLedger.CompletedDaysFor(level,day,completed),Is.EqualTo(expectedDays));
 }
 [Test] public void OutboxPersistsSalvageEvidenceAndDebugDisqualification()
 {
  var queue=new PendingMatchResultQueue();queue.items.Add(new PendingMatchResultRecord{request=new SubmitMatchResultRequestDto{
   salvageEvidence=true,salvageCompletedLevels=2,salvageCompletedDays=6,usedDebugCheats=true}});
  var restored=JsonUtility.FromJson<PendingMatchResultQueue>(JsonUtility.ToJson(queue));var result=restored.items[0].request;
  Assert.That(result.salvageEvidence,Is.True);Assert.That(result.salvageCompletedLevels,Is.EqualTo(2));Assert.That(result.salvageCompletedDays,Is.EqualTo(6));Assert.That(result.usedDebugCheats,Is.True);
 }
}
