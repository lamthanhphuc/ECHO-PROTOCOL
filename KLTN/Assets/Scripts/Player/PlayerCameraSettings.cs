using UnityEngine;

public static class PlayerCameraSettings
{
    public static float Intensity
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat("EchoProtocol.Camera.Motion", 1));
        set { PlayerPrefs.SetFloat("EchoProtocol.Camera.Motion", Mathf.Clamp01(value)); PlayerPrefs.Save(); }
    }
    public static bool ReduceMotion
    {
        get => PlayerPrefs.GetInt("EchoProtocol.Camera.ReduceMotion", 0) != 0;
        set { PlayerPrefs.SetInt("EchoProtocol.Camera.ReduceMotion", value ? 1 : 0); PlayerPrefs.Save(); }
    }
}
