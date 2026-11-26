using EchoProtocol.Api.Services;
using EchoProtocol.Api.Services.Interfaces;
using EchoProtocol.Api.Enums;
using Xunit;
namespace EchoProtocol.Api.Tests;
public class SalvageRewardPolicyTests
{
 [Theory]
 [InlineData(1,3,180,180,false,false,40,100)]
 [InlineData(2,6,600,600,false,false,80,200)]
 [InlineData(10,30,3600,3600,false,false,200,500)]
 [InlineData(0,3,600,600,false,false,0,0)]
 [InlineData(1,3,179,179,false,false,0,0)]
 [InlineData(1,3,600,119,false,false,0,0)]
 [InlineData(1,3,600,180,false,false,0,0)]
 [InlineData(1,3,600,600,true,false,0,0)]
 [InlineData(1,3,600,600,false,true,0,0)]
 public void RewardUsesBoundedServerTimeAndParticipation(int levels,int days,int duration,int participation,bool disconnected,bool debug,int credits,int xp)
 {
  var user=Guid.NewGuid();var player=new RewardPlayerSnapshot(user,true,disconnected,0,0,0,0,0,0,true,participation);
  var match=new RewardMatchSnapshot(Guid.NewGuid(),MatchOutcome.WIN,1,duration,new[]{player},MatchContentCatalog.SalvageMap,"NORMAL",levels,days,debug);
  Assert.Equal(credits,new SalvageRewardPolicy().Calculate(match).Single().CurrencyAmount);
  Assert.Equal(xp,new SalvageProgressionPolicy().Calculate(match).Single().ExperiencePoints);
 }
}
