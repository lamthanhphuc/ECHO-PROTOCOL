using System.Security.Claims;
using System.Text.Json;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.MatchResults;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace EchoProtocol.Api.Controllers;
[ApiController,Authorize,Route("api/salvage/checkpoints")]
public sealed class SalvageCheckpointsController(AppDbContext db,TimeProvider clock,IRewardService rewards,IMatchProfilePostProcessingService profiles):ControllerBase
{
    public sealed class SaveRequest
    {
        public Guid MatchId {get;set;}
        public long Revision {get;set;}
        public string StateJson {get;set;}="";
        public bool Failed {get;set;}
        public SubmitMatchResultRequest Result {get;set;}=new();
    }
    private Guid UserId=>Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var id)?id:Guid.Empty;
    [HttpGet("latest")]
    public async Task<IActionResult> Latest(CancellationToken ct)
    {
        var row=await db.SalvageCheckpoints.AsNoTracking().Where(x=>x.OwnerId==UserId).OrderByDescending(x=>x.SavedAtUtc).FirstOrDefaultAsync(ct);
        return Ok(ApiResponse<object>.Ok(new {stateJson=row==null||row.Failed?"":row.StateJson,matchId=row?.MatchId,failed=row?.Failed??false}));
    }
    [HttpPut,RequestSizeLimit(1048576)]
    public async Task<IActionResult> Save(SaveRequest request,CancellationToken ct)
    {
        if(request.StateJson.Length>262144||request.Revision<1)return BadRequest();
        // Row locking serializes host handoff and prevents stale uploads from overwriting a newer day.
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        if(db.Database.IsNpgsql())await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"MatchAuthorityBindings\" WHERE \"MatchId\"={request.MatchId} FOR UPDATE",ct);
        var match=await db.MatchAuthorityBindings.Include(x=>x.Players).SingleOrDefaultAsync(x=>x.MatchId==request.MatchId,ct);
        var now=clock.GetUtcNow().UtcDateTime;
        if(match==null||match.HostUserId!=UserId||!MatchContentCatalog.IsSalvage(match.MapId)||match.Status!=MatchAuthorityStatus.InMatch||match.LeaseExpiresAtUtc<=now)return Forbid();
        if(request.Result.EvidenceVersion!=2||request.Result.SalvageCompletedLevels is not int levels||request.Result.SalvageCompletedDays is not int days
            ||!SalvageRewardPolicy.ValidEvidence(levels,days)||!request.Result.Players.Select(x=>x.UserId).Order().SequenceEqual(match.Players.Select(x=>x.UserId).Order()))return BadRequest();
        try
        {
            using var json=JsonDocument.Parse(request.StateJson);var root=json.RootElement;
            if(root.GetProperty("owner").GetString()!=UserId.ToString()||root.GetProperty("version").GetInt32()!=1
                ||root.GetProperty("cycle").GetInt32()<1||root.GetProperty("day").GetInt32() is <1 or >3
                ||root.GetProperty("funds").GetInt32()<0||root.GetProperty("cargo").GetArrayLength()!=128)return BadRequest();
        }catch(JsonException){return BadRequest();}catch(KeyNotFoundException){return BadRequest();}catch(InvalidOperationException){return BadRequest();}catch(FormatException){return BadRequest();}catch(OverflowException){return BadRequest();}
        var row=await db.SalvageCheckpoints.SingleOrDefaultAsync(x=>x.MatchId==request.MatchId,ct);
        if(row!=null&&request.Revision<=row.Revision)return Conflict(ApiResponse<object>.Fail("Stale checkpoint","SALVAGE_STALE_CHECKPOINT"));
        if(row==null){row=new SalvageCheckpoint {MatchId=request.MatchId};db.SalvageCheckpoints.Add(row);}
        row.OwnerId=UserId;row.Revision=request.Revision;row.StateJson=request.StateJson;row.Failed=request.Failed;
        row.ResultJson=JsonSerializer.Serialize(request.Result,new JsonSerializerOptions(JsonSerializerDefaults.Web));row.SavedAtUtc=now;
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        return Ok(ApiResponse<object>.Ok(new {revision=row.Revision}));
    }
    [HttpPost("recover")]
    public async Task<IActionResult> Recover(CancellationToken ct)
    {
        var ids=await db.SalvageCheckpoints.AsNoTracking().Where(x=>x.OwnerId==UserId).Select(x=>x.MatchId).ToListAsync(ct);
        var resultService=new MatchResultService(db,clock);int recovered=0;
        foreach(var id in ids)
        {
            var result=await resultService.RecoverSalvageCheckpointAsync(UserId,id,ct);
            if(!result.IsSuccess)continue;
            await rewards.ProcessAsync(id,ct);await profiles.EnqueueAsync(id,ct);recovered++;
        }
        return Ok(ApiResponse<object>.Ok(new {recovered}));
    }
}
