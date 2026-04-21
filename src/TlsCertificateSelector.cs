using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;

namespace PeFi.Proxy;

public sealed class TlsCertificateSelector
{
    private readonly IReadOnlyDictionary<string, X509Certificate2> _hostCertificates;
    private readonly X509Certificate2? _defaultCertificate;

    private TlsCertificateSelector(IReadOnlyDictionary<string, X509Certificate2> hostCertificates, X509Certificate2? defaultCertificate)
    {
        _hostCertificates = hostCertificates;
        _defaultCertificate = defaultCertificate;
    }

    public bool HasCertificates => _defaultCertificate is not null;

    public X509Certificate2? Select(string? serverName)
    {
        if (!string.IsNullOrWhiteSpace(serverName))
        {
            var normalizedServerName = NormalizeHost(serverName);
            if (_hostCertificates.TryGetValue(normalizedServerName, out var certificate))
                return certificate;
        }

        return _defaultCertificate;
    }

    public static TlsCertificateSelector FromConfiguration(IConfiguration tlsSection)
    {
        var hostCertificates = new Dictionary<string, X509Certificate2>(StringComparer.OrdinalIgnoreCase);
        X509Certificate2? defaultCertificate = null;

        var certificates = tlsSection.GetSection("Certificates").Get<List<TlsCertificateConfiguration>>() ?? [];
        foreach (var certificateConfig in certificates)
        {
            if (string.IsNullOrWhiteSpace(certificateConfig.Path))
                continue;

            var certificate = new X509Certificate2(certificateConfig.Path, certificateConfig.Password);
            defaultCertificate ??= certificate;

            foreach (var host in certificateConfig.Hosts ?? [])
            {
                if (string.IsNullOrWhiteSpace(host))
                    continue;

                hostCertificates[NormalizeHost(host)] = certificate;
            }
        }

        return new TlsCertificateSelector(hostCertificates, defaultCertificate);
    }

    private static string NormalizeHost(string host) => host.Trim().TrimEnd('.').ToLowerInvariant();

    public sealed class TlsCertificateConfiguration
    {
        public string? Path { get; set; }
        public string? Password { get; set; }
        public string[]? Hosts { get; set; }
    }
}
