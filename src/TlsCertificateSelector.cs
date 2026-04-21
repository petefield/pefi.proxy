using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace PeFi.Proxy;

public sealed class TlsCertificateSelector
    : IDisposable
{
    private const string SubjectAlternativeNameOid = "2.5.29.17";
    private static readonly Regex DnsNameRegex = new(@"DNS Name=(?<host>[^\r\n,]+)", RegexOptions.Compiled);

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
                certificate = LoadCertificate(certificateConfig.Path, certificateConfig.Password, certificateConfig.KeyPath);
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

        var certificateDirectoryPath = tlsSection.GetValue<string>("Directory");
        if (!string.IsNullOrWhiteSpace(certificateDirectoryPath) && Directory.Exists(certificateDirectoryPath))
        {
            var directoryPassword = tlsSection.GetValue<string>("DirectoryPassword");
            var certificatePaths = Directory.EnumerateFiles(certificateDirectoryPath, "*", SearchOption.TopDirectoryOnly)
                .Where(path =>
                {
                    var extension = Path.GetExtension(path);
                    return extension.Equals(".pfx", StringComparison.OrdinalIgnoreCase)
                        || extension.Equals(".p12", StringComparison.OrdinalIgnoreCase)
                        || extension.Equals(".pem", StringComparison.OrdinalIgnoreCase);
                })
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

            foreach (var certificatePath in certificatePaths)
            {
                X509Certificate2 certificate;
                try
                {
                    certificate = LoadCertificate(certificatePath, directoryPassword);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Unable to load TLS certificate from directory path '{certificatePath}'.",
                        ex);
                }

                loadedCertificates.Add(certificate);
                defaultCertificate ??= certificate;

                foreach (var host in GetCertificateHosts(certificate))
                    hostCertificates[NormalizeHost(host)] = certificate;
            }
        }

        return new TlsCertificateSelector(hostCertificates, defaultCertificate, loadedCertificates);
    }

    private static X509Certificate2 LoadCertificate(string certificatePath, string? password, string? keyPath = null)
    {
        var extension = Path.GetExtension(certificatePath);
        if (extension.Equals(".pfx", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".p12", StringComparison.OrdinalIgnoreCase))
        {
            return new X509Certificate2(certificatePath, password);
        }

        if (!extension.Equals(".pem", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Unsupported TLS certificate file extension '{extension}' for '{certificatePath}'.");

        var effectiveKeyPath = ResolvePemKeyPath(certificatePath, keyPath);
        return string.IsNullOrWhiteSpace(password)
            ? X509Certificate2.CreateFromPemFile(certificatePath, effectiveKeyPath)
            : X509Certificate2.CreateFromEncryptedPemFile(certificatePath, password, effectiveKeyPath);
    }

    private static string ResolvePemKeyPath(string certificatePath, string? keyPath)
    {
        if (!string.IsNullOrWhiteSpace(keyPath))
            return keyPath;

        var candidateKeyPath = Path.ChangeExtension(certificatePath, ".key");
        return File.Exists(candidateKeyPath) ? candidateKeyPath : certificatePath;
    }

    private static string NormalizeHost(string host) => host.Trim().TrimEnd('.').ToLowerInvariant();

    private static IEnumerable<string> GetCertificateHosts(X509Certificate2 certificate)
    {
        var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var dnsName = certificate.GetNameInfo(X509NameType.DnsName, false);
        if (!string.IsNullOrWhiteSpace(dnsName))
            hosts.Add(dnsName);

        var subjectAlternativeName = certificate.Extensions[SubjectAlternativeNameOid];
        if (subjectAlternativeName is null)
            return hosts;

        if (subjectAlternativeName is X509SubjectAlternativeNameExtension typedSubjectAlternativeName)
        {
            foreach (var host in typedSubjectAlternativeName.EnumerateDnsNames())
            {
                if (!string.IsNullOrWhiteSpace(host))
                    hosts.Add(host);
            }

            return hosts;
        }

        var matches = DnsNameRegex.Matches(subjectAlternativeName.Format(true));
        foreach (Match match in matches)
        {
            var host = match.Groups["host"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(host))
                hosts.Add(host);
        }

        return hosts;
    }

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
        public string? KeyPath { get; set; }
        public string[]? Hosts { get; set; }
    }
}
