using System.Collections;
using EchoProtocol.Api;
using EchoProtocol.Auth;
using EchoProtocol.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoProtocol.Auth
{
  public class BootstrapSceneFlowController : MonoBehaviour
  {
    private void Start()
    {
      var runtime = AuthRuntime.EnsureExists();
      StartCoroutine(RunBootstrapFlow(runtime));
    }

    private IEnumerator RunBootstrapFlow(AuthRuntime runtime)
    {
      if (!TokenStorage.HasToken() || TokenStorage.IsExpired())
      {
        if (TokenStorage.HasToken() || !string.IsNullOrEmpty(TokenStorage.GetExpiresAt()))
        {
          runtime.AuthService.LogoutLocal();
        }

        runtime.SetRestoreState(SessionRestoreState.None);
        SceneManager.LoadScene(GameConstants.SceneLogin);
        yield break;
      }

      var completed = false;
      ApiResult<MeApiResponse> meResult = null;

      runtime.AuthService.GetCurrentUser(result =>
      {
        meResult = result;
        completed = true;
      });

      yield return new WaitUntil(() => completed);

      if (meResult != null && meResult.IsSuccess && meResult.Data != null && meResult.Data.success)
      {
        runtime.SetRestoreState(SessionRestoreState.Succeeded);
        SceneManager.LoadScene(GameConstants.SceneMainMenu);
        yield break;
      }

      if (meResult != null && AuthApiService.ShouldClearToken(meResult))
      {
        runtime.AuthService.LogoutLocal();
        runtime.SetRestoreState(SessionRestoreState.FailedUnauthorized);
        SceneManager.LoadScene(GameConstants.SceneLogin);
        yield break;
      }

      runtime.SetRestoreState(SessionRestoreState.FailedNetwork);
      SceneManager.LoadScene(GameConstants.SceneLogin);
    }
  }
}
