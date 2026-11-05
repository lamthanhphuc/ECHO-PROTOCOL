using System;
using EchoProtocol.Api;
using NUnit.Framework;

public sealed class AEDPilotEndpointTests
{
    [TestCase("http://localhost:5072", "Local", false)]
    [TestCase("http://127.0.0.1:5072/", "Local", false)]
    [TestCase("http://[::1]:5072", "Local", false)]
    [TestCase(ApiConfig.ProductionBaseUrl, "Production", true)]
    public void AllowsOnlyExplicitSupportedTargets(string url, string environment, bool allow)
    { Assert.That(ApiConfiguration.ValidatePilotEndpoint(url, environment, allow, true), Is.EqualTo(url.TrimEnd('/'))); }

    [TestCase(ApiConfig.ProductionBaseUrl, "Production", false)]
    [TestCase(ApiConfig.ProductionBaseUrl, "Local", true)]
    [TestCase("http://api.3.1.238.6.sslip.io", "Production", true)]
    [TestCase("https://other.example", "Production", true)]
    [TestCase("https://api.3.1.238.6.sslip.io.evil.example", "Production", true)]
    [TestCase("https://api.3.1.238.6.sslip.io:444", "Production", true)]
    [TestCase("https://api.3.1.238.6.sslip.io/api", "Production", true)]
    [TestCase("https://user:pass@api.3.1.238.6.sslip.io", "Production", true)]
    [TestCase("https://api.3.1.238.6.sslip.io?token=x", "Production", true)]
    [TestCase("https://api.3.1.238.6.sslip.io#fragment", "Production", true)]
    [TestCase("http://localhost:5072", "Production", true)]
    [TestCase("http://localhost:5072/api", "Local", false)]
    [TestCase("http://localhost:5072?x=1", "Local", false)]
    [TestCase("invalid", "Local", false)]
    [TestCase(null, "Local", false)]
    public void RejectsUnapprovedOrAmbiguousTargets(string url, string environment, bool allow)
    { Assert.Throws<InvalidOperationException>(() => ApiConfiguration.ValidatePilotEndpoint(url, environment, allow, true)); }

    [Test] public void ReleaseBuildCannotEnableHarnessEvenWithProductionOptIn()
    { Assert.Throws<InvalidOperationException>(() => ApiConfiguration.ValidatePilotEndpoint(ApiConfig.ProductionBaseUrl, "Production", true, false)); }
}
