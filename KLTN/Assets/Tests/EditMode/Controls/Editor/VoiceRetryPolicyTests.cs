using EchoProtocol.Voice;
using NUnit.Framework;
namespace EchoProtocol.Tests.EditMode.Controls
{
    public sealed class VoiceRetryPolicyTests
    {
        [TestCase(1,2f)] [TestCase(2,4f)] [TestCase(3,30f)]
        [TestCase(4,30f)] [TestCase(5,60f)] [TestCase(100,60f)]
        public void RetryDelay_ContinuesAfterThirdFailureAndStaysBounded(int failures,float expected)
        {
            Assert.That(VoiceRetryPolicy.DelaySeconds(failures),Is.EqualTo(expected));
        }
    }
}
