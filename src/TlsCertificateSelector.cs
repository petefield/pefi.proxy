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
        X509Certificate2? certificate = null;

        if (!string.IsNullOrWhiteSpace(serverName))
        {
            var normalizedServerName = NormalizeHost(serverName);

            if (!_hostCertificates.TryGetValue(normalizedServerName, out certificate))
            {
                var wildcardServerName = "*." + string.Join('.', normalizedServerName.Split('.').Skip(1));
                
                if (!_hostCertificates.TryGetValue(wildcardServerName, out certificate))
                {
                    Console.WriteLine($"No TLS certificate found for server name '{serverName}' (normalized: '{normalizedServerName}', wildcard: '{wildcardServerName}').");
                }
            }
        }

        certificate ??= _defaultCertificate;

        Console.WriteLine($"TLS certificate selected for server name '{serverName}': {(certificate != null ? certificate.Subject : "none")}.");

        return certificate;
    }

    public static TlsCertificateSelector FromDirectory(IConfiguration tlsSection)
    {
        var hostCertificates = new Dictionary<string, X509Certificate2>(StringComparer.OrdinalIgnoreCase);
        var loadedCertificates = new List<X509Certificate2>();
        X509Certificate2? defaultCertificate = null;

        var certificateDirectoryPath = tlsSection.GetValue<string>("Directory");
        Console.WriteLine($"Certificate Directory Path {certificateDirectoryPath}");

        if (!string.IsNullOrWhiteSpace(certificateDirectoryPath) && Directory.Exists(certificateDirectoryPath))
        {
            Console.WriteLine($"Certificate Directory Path {certificateDirectoryPath} - exists");

            var certificatePaths = Directory.EnumerateFiles(certificateDirectoryPath, "*", SearchOption.AllDirectories)
                .Where(path =>
                {
                    var filename = Path.GetFileName(path);
                    return String.Equals (filename, "fullchain.pem", StringComparison.OrdinalIgnoreCase) ;
                });

            foreach (var certificatePath in certificatePaths)
            {
               Console.WriteLine($" loading  {certificatePath}");

                X509Certificate2? certificate;
                try
                {
                    certificate = LoadCertificate(certificatePath);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Unable to load TLS certificate from directory path '{certificatePath}'.",
                        ex);
                }

                if(certificate is null)
                {
                    Console.WriteLine($" skipped  {certificatePath}");
                    continue;
                }

                loadedCertificates.Add(certificate);
                defaultCertificate ??= certificate;

                foreach (var host in GetCertificateHosts(certificate))
                    hostCertificates[NormalizeHost(host)] = certificate;
            }
        }

        return new TlsCertificateSelector(hostCertificates, defaultCertificate, loadedCertificates);
    }

   private static X509Certificate2? LoadCertificate(string certificatePath)
{
    var fileName = Path.GetFileName(certificatePath);
    var directory = Path.GetDirectoryName(certificatePath);
    X509Certificate2 certificate;

    if(fileName.Equals("fullchain.pem", StringComparison.OrdinalIgnoreCase))
    {
        var effectiveKeyPath = Path.Combine(directory!, "privkey.pem");
        Console.WriteLine($"Loading PEM certificate with separate key file: cert={certificatePath}, key={effectiveKeyPath}");
        certificate = X509Certificate2.CreateFromPemFile(certificatePath, effectiveKeyPath);

        if (!certificate.HasPrivateKey)
        {
            certificate.Dispose();
            throw new InvalidOperationException(
                $"TLS certificate '{certificatePath}' was loaded without a private key. " +
                "For PEM certificates, configure the matching key file via Tls:Certificates:*:KeyPath or place a matching .key file alongside the .pem file.");
        }

        return certificate;
    }

    return null;
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
