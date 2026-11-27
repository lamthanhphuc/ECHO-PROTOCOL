using System.Threading.Tasks;
using EchoProtocol.Salvage;
namespace EchoProtocol.Networking.Authority
{
    public sealed partial class MatchAuthorityRuntime
    {
        public async Task<string> LoadSalvageCloudAsync()
        {
            if(_api==null)throw new System.InvalidOperationException("Chưa kết nối dịch vụ lưu tiến độ.");
            // A failed recovery never authorizes local data; it is retried on the next load.
            await _api.RecoverSalvageRewardsAsync();
            var response=await _api.GetSalvageCheckpointAsync();
            if(response==null||!response.IsSuccess||response.Data?.data==null)throw new System.InvalidOperationException("Không tải được tiến độ. Kiểm tra kết nối hoặc cập nhật backend.");
            return response.Data.data.stateJson??"";
        }
        public async Task<bool> SaveSalvageCloudAsync(string json,long revision,bool failed)
        {
            if(!HasBinding||!IsHostBinding||_api==null||!TryBuildMatchResultRequest(out var result))return false;
            result.outcome=result.salvageCompletedLevels>0?"WIN":"LOSE";result.objectiveCompletion=result.salvageCompletedLevels>0?1:0;
            var response=await _api.SaveSalvageCheckpointAsync(MatchId,json,revision,failed,result);
            return response!=null&&response.IsSuccess;
        }
    }
}
