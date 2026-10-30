using System.Collections;
using EchoProtocol.Api;
using EchoProtocol.Auth;
using EchoProtocol.Core;
using EchoProtocol.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoProtocol.Auth
{
  public class BootstrapSceneFlowController : MonoBehaviour
  {
    private static bool _returnToLobbyAfterAuthentication;

    public static void RequestLobbyAfterAuthentication()
    {
      _returnToLobbyAfterAuthentication = true;
    }

    private void Start()
    {
      GameUIFeedback.Instance.ShowStartup();
      var runtime = AuthRuntime.EnsureExists();
      StartCoroutine(RunBootstrapFlow(runtime));
    }

    private IEnumerator RunBootstrapFlow(AuthRuntime runtime)
    {
      yield return null;
      if (!TokenStorage.HasUsableSession())
      {
        _returnToLobbyAfterAuthentication = false;
        if (TokenStorage.HasToken()
            || TokenStorage.HasRefreshToken()
            || TokenStorage.HasStoredExpiry())
        {
          runtime.AuthService.LogoutLocal();
        }

        runtime.SetRestoreState(SessionRestoreState.None);
        GameUIFeedback.LoadScene(GameConstants.SceneLogin);
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
        var destination = _returnToLobbyAfterAuthentication
          ? GameConstants.SceneLobby : GameConstants.SceneMainMenu;
        _returnToLobbyAfterAuthentication = false;
        FirstTimeAgreementController.Require(destination);
        yield break;
      }

      _returnToLobbyAfterAuthentication = false;
      if (meResult != null && AuthApiService.ShouldClearToken(meResult))
      {
        runtime.AuthService.LogoutLocal();
        runtime.SetRestoreState(SessionRestoreState.FailedUnauthorized);
        GameUIFeedback.LoadScene(GameConstants.SceneLogin);
        yield break;
      }

      runtime.SetRestoreState(SessionRestoreState.FailedNetwork);
      GameUIFeedback.LoadScene(GameConstants.SceneLogin);
    }
  }
}
