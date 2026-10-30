using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Security;
using System.Net;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using OpenAC.Net.DFe.Core.Http;
using OpenAC.Net.NFe.Common;

namespace OpenAC.Net.NFe.Web;

public interface INFeConfigurationProvider
{
    ValueTask<NFeConfig> GetAsync(string empresa, CancellationToken cancellationToken = default);
}
public interface INFeCertificateProvider
{
    /// <summary>Retorna certificado emprestado. O provider controla sua vida útil; a sessão cria uma cópia.</summary>
    ValueTask<X509Certificate2> GetAsync(string empresa, CancellationToken cancellationToken = default);
}
public interface IOpenNFeClientFactory
{
    ValueTask<NFeSession> CreateAsync(string empresa = "default", CancellationToken cancellationToken = default);
}
/// <summary>Descartar a sessão libera sua cópia do certificado. O cliente não mantém operações ou notas compartilhadas.</summary>
public sealed class NFeSession : IDisposable
{
    public IOpenNFeClient Client { get; }
    private readonly X509Certificate2 certificate;
    internal NFeSession(OpenNFe client, X509Certificate2 certificate) { Client = client; this.certificate = certificate; }
    public void Dispose() { certificate.Dispose(); }
}
internal sealed class NFeHttpProfile(X509Certificate2 certificate, DFeHttpTransportOptions options) : IDisposable
{
    internal X509Certificate2 Certificate { get; } = new(certificate);
    internal SslProtocols Protocols { get; } = options.Protocols;
    internal string Proxy { get; } = options.Proxy;
    internal bool ValidateServerCertificate { get; } = options.ValidateServerCertificate;
    internal bool UseCookies { get; } = options.UseCookies;
    internal bool AllowAutoRedirect { get; } = options.AllowAutoRedirect;
    internal DecompressionMethods AutomaticDecompression { get; } = options.AutomaticDecompression;
    public void Dispose() => Certificate.Dispose();
}
internal sealed class NFeClientRegistry : IDisposable
{
    private readonly ConcurrentDictionary<string, NFeHttpProfile> profiles = new();
    internal string Register(string tenant, X509Certificate2 certificate, Uri endpoint, DFeHttpTransportOptions options)
    {
        var key = $"{tenant}|{certificate.Thumbprint}|{endpoint.GetLeftPart(UriPartial.Authority)}|{(int)options.Protocols}|{options.Proxy}|{options.ValidateServerCertificate}|{options.UseCookies}|{options.AllowAutoRedirect}|{(int)options.AutomaticDecompression}";
        var name = "OpenAC.NFe:" + Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key)));
        lock (profiles)
        {
            if (!profiles.ContainsKey(name)) profiles[name] = new NFeHttpProfile(certificate, options);
        }
        return name;
    }
    internal bool TryGet(string name, out NFeHttpProfile profile) => profiles.TryGetValue(name, out profile!);
    public void Dispose() { foreach (var profile in profiles.Values) profile.Dispose(); profiles.Clear(); }
}
internal sealed class NFeHttpOptions(NFeClientRegistry registry) : IConfigureNamedOptions<HttpClientFactoryOptions>
{
    public void Configure(HttpClientFactoryOptions options) { }
    public void Configure(string? name, HttpClientFactoryOptions options)
    {
        if (name == null || !registry.TryGet(name, out var profile)) return;
        options.HandlerLifetime = TimeSpan.FromMinutes(5);
        options.HttpClientActions.Add(client => client.Timeout = Timeout.InfiniteTimeSpan);
        options.HttpMessageHandlerBuilderActions.Add(builder =>
        {
            var handler = new SocketsHttpHandler
            {
                AllowAutoRedirect = profile.AllowAutoRedirect,
                UseCookies = profile.UseCookies,
                AutomaticDecompression = profile.AutomaticDecompression,
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                SslOptions = new SslClientAuthenticationOptions
                {
                    ClientCertificates = new X509CertificateCollection { profile.Certificate },
                    EnabledSslProtocols = profile.Protocols
                }
            };
            if (!string.IsNullOrWhiteSpace(profile.Proxy)) { handler.Proxy = new WebProxy(profile.Proxy); handler.UseProxy = true; }
            if (!profile.ValidateServerCertificate) handler.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;
            builder.PrimaryHandler = handler;
        });
    }
}
internal sealed class NFeWebTransport(string empresa, IHttpClientFactory factory, NFeClientRegistry registry) : IDFeHttpTransport
{
    private static readonly ActivitySource Activities = new("OpenAC.Net.NFe.Web");
    private static readonly Meter Meter = new("OpenAC.Net.NFe.Web");
    private static readonly Counter<long> Requests = Meter.CreateCounter<long>("openac.nfe.requests");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("openac.nfe.duration", "ms");
    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, DFeHttpTransportOptions options, CancellationToken cancellationToken = default)
    {
        var endpoint = request.RequestUri ?? throw new ArgumentException("Endpoint não informado.");
        if (!endpoint.IsAbsoluteUri || endpoint.Scheme != "https") throw new ArgumentException("Serviços fiscais exigem HTTPS.");
        var cert = options.Certificate ?? throw new InvalidOperationException("Certificado não configurado.");
        using var activity = Activities.StartActivity("NFe.SOAP", ActivityKind.Client);
        activity?.SetTag("server.address", endpoint.Host);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var client = factory.CreateClient(registry.Register(empresa, cert, endpoint, options));
            var result = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
            Requests.Add(1);
            return result;
        }
        catch { activity?.SetStatus(ActivityStatusCode.Error); throw; }
        finally { Duration.Record(stopwatch.Elapsed.TotalMilliseconds); }
    }
}
internal sealed class OpenNFeClientFactory(INFeConfigurationProvider configurations, INFeCertificateProvider certificates, IHttpClientFactory http, NFeClientRegistry registry) : IOpenNFeClientFactory
{
    public async ValueTask<NFeSession> CreateAsync(string empresa = "default", CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(empresa)) throw new ArgumentException("Empresa não informada.", nameof(empresa));
        cancellationToken.ThrowIfCancellationRequested();
        var source = await configurations.GetAsync(empresa, cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException("Configuração não informada.");
        var cert = source.Certificado ?? await certificates.GetAsync(empresa, cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException("Certificado não informado.");
        if (!cert.HasPrivateKey || cert.NotBefore.ToUniversalTime() > DateTime.UtcNow || cert.NotAfter.ToUniversalTime() <= DateTime.UtcNow) throw new InvalidOperationException("Certificado inválido ou vencido.");
        var copy = new X509Certificate2(cert);
        try
        {
            var config = source.Copiar();
            config.Certificado = copy;
            config.HttpTransport = new NFeWebTransport(empresa, http, registry);
            return new NFeSession(new OpenNFe(config), copy);
        }
        catch { copy.Dispose(); throw; }
    }
}
internal sealed class DefaultConfigurationProvider(Action<NFeConfig> configure) : INFeConfigurationProvider
{
    public ValueTask<NFeConfig> GetAsync(string empresa, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); var c = new NFeConfig(); configure(c); return ValueTask.FromResult(c); }
}
internal sealed class DefaultCertificateProvider(INFeConfigurationProvider configurations) : INFeCertificateProvider
{
    public async ValueTask<X509Certificate2> GetAsync(string empresa, CancellationToken cancellationToken = default)
        => (await configurations.GetAsync(empresa, cancellationToken).ConfigureAwait(false)).Certificado ?? throw new InvalidOperationException("Configure Certificado ou registre INFeCertificateProvider.");
}
public static class NFeServiceCollectionExtensions
{
    public static IServiceCollection AddNFe(this IServiceCollection services, Action<NFeConfig> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        services.AddSingleton<INFeConfigurationProvider>(new DefaultConfigurationProvider(configure));
        services.TryAddScoped<INFeCertificateProvider, DefaultCertificateProvider>();
        return Infrastructure(services);
    }
    public static IServiceCollection AddOpenNFeWebMultiTenant<TConfiguration, TCertificate>(this IServiceCollection services)
        where TConfiguration : class, INFeConfigurationProvider where TCertificate : class, INFeCertificateProvider
    {
        services.AddScoped<INFeConfigurationProvider, TConfiguration>(); services.AddScoped<INFeCertificateProvider, TCertificate>(); return Infrastructure(services);
    }
    private static IServiceCollection Infrastructure(IServiceCollection services)
    {
        services.AddHttpClient(); services.TryAddSingleton<NFeClientRegistry>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<HttpClientFactoryOptions>, NFeHttpOptions>());
        services.TryAddScoped<IOpenNFeClientFactory, OpenNFeClientFactory>(); return services;
    }
}
