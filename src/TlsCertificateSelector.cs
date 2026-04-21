using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;

namespace PeFi.Proxy;

public sealed class TlsCertificateSelector
    : IDisposable
{
    private readonly IReadOnlyDictionary<string, X509Certificate2> _hostCertificates;
    private readonly X509Certificate2? _defaultCertificate;
    private readonly IReadOnlyCollection<X509Certificate2> _loadedCertificates;
    private bool _disposed;

    private TlsCertificateSelector(
        IReadOnlyDictionary<string, X509Certificate2> hostCertificates,
        X509Certificate2? defaultCertificate,
        IReadOnlyCollection<X509Certificate2> loadedCertificates)
    {
        _hostCertificates = hostCertificates;
        _defaultCertificate = defaultCertificate;
        _loadedCertificates = loadedCertificates;
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
        var loadedCertificates = new List<X509Certificate2>();
        X509Certificate2? defaultCertificate = null;

        var certificates = tlsSection.GetSection("Certificates").Get<List<TlsCertificateConfiguration>>() ?? [];
        for (var index = 0; index < certificates.Count; index++)
        {
            var certificateConfig = certificates[index];
            if (string.IsNullOrWhiteSpace(certificateConfig.Path))
                continue;

            X509Certificate2 certificate;
            try
            {
                certificate = new X509Certificate2(certificateConfig.Path, certificateConfig.Password);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Unable to load TLS certificate at index {index} from path '{certificateConfig.Path}'.",
                    ex);
            }

            loadedCertificates.Add(certificate);
            defaultCertificate ??= certificate;

            foreach (var host in certificateConfig.Hosts ?? [])
            {
                if (string.IsNullOrWhiteSpace(host))
                    continue;

                hostCertificates[NormalizeHost(host)] = certificate;
            }
        }

        return new TlsCertificateSelector(hostCertificates, defaultCertificate, loadedCertificates);
    }

    private static string NormalizeHost(string host) => host.Trim().TrimEnd('.').ToLowerInvariant();

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        foreach (var certificate in _loadedCertificates)
            certificate.Dispose();
    }

    public sealed class TlsCertificateConfiguration
    {
        public string? Path { get; set; }
        public string? Password { get; set; }
        public string[]? Hosts { get; set; }
    }
}
