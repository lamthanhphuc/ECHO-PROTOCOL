using UnityEngine;

namespace EchoProtocol.Tools.Scanner
{
    public interface IMotionScannable
    {
        int TargetId { get; }
        Vector3 WorldPosition { get; }
        bool IsMoving { get; }
        float CurrentSpeed { get; }
        bool IsActiveTarget { get; }
    }
}

