namespace EchoProtocol.Voice
{
    public static class VoiceTransmissionRules
    {
        public static bool CanCapture(bool joined, bool enabled, bool muted, bool deviceReady,
            bool testing, bool focused, bool paused, bool settingsOpen,
            bool pushToTalk = false, bool pushToTalkHeld = false) =>
            joined && enabled && !muted && deviceReady && !testing && focused && !paused && !settingsOpen
            && (!pushToTalk || pushToTalkHeld);
    }
}
