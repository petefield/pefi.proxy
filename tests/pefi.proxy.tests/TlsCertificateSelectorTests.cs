using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
using PeFi.Proxy;
using Xunit;

namespace PeFi.Proxy.Tests;

public class TlsCertificateSelectorTests
{
    private const int CertificateValidFromDaysOffset = -1;
    private const int CertificateValidToDaysOffset = 1;

    [Fact]
    public void FromConfiguration_WithMultipleCertificates_SelectsByHostAndFallsBackToDefault()
    {
        const string password = "test-password";

        var firstCertificatePath = CreateCertificateFile("pub.the-fields.net", password);
        var secondCertificatePath = CreateCertificateFile("tour.pefi.co.uk", password);

        try
        {
            using var firstCertificate = new X509Certificate2(firstCertificatePath, password);
            using var secondCertificate = new X509Certificate2(secondCertificatePath, password);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Tls:Certificates:0:Path"] = firstCertificatePath,
                    ["Tls:Certificates:0:Password"] = password,
                    ["Tls:Certificates:0:Hosts:0"] = "pub.the-fields.net",
                    ["Tls:Certificates:1:Path"] = secondCertificatePath,
                    ["Tls:Certificates:1:Password"] = password,
                    ["Tls:Certificates:1:Hosts:0"] = "tour.pefi.co.uk",
                })
                .Build();

            var selector = TlsCertificateSelector.FromConfiguration(configuration.GetSection("Tls"));

            Assert.True(selector.HasCertificates);
            Assert.Equal(firstCertificate.Thumbprint, selector.Select("pub.the-fields.net")?.Thumbprint);
            Assert.Equal(secondCertificate.Thumbprint, selector.Select("tour.pefi.co.uk")?.Thumbprint);
            Assert.Equal(firstCertificate.Thumbprint, selector.Select("unknown.pefi.co.uk")?.Thumbprint);
        }
        finally
        {
            TryDelete(firstCertificatePath);
            TryDelete(secondCertificatePath);
        }
    }

    [Fact]
    public void FromConfiguration_WithoutCertificates_ReturnsEmptySelector()
    {
        var configuration = new ConfigurationBuilder().Build();

        var selector = TlsCertificateSelector.FromConfiguration(configuration.GetSection("Tls"));

        Assert.False(selector.HasCertificates);
        Assert.Null(selector.Select("pub.the-fields.net"));
    }

    private static string CreateCertificateFile(string commonName, string password)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest($"CN={commonName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(CertificateValidFromDaysOffset),
            DateTimeOffset.UtcNow.AddDays(CertificateValidToDaysOffset));

        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pfx");
        File.WriteAllBytes(filePath, certificate.Export(X509ContentType.Pfx, password));

        return filePath;
    }

    private static void TryDelete(string filePath)
    {
        try
        {
            File.Delete(filePath);
        }
        catch
        {
            // best-effort cleanup for temporary test files
        }
    }
}
