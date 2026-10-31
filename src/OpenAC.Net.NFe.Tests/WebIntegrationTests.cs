using System.Net;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using OpenAC.Net.DFe.Core.Storage;
using OpenAC.Net.NFe.Certificates;
using OpenAC.Net.NFe.Common;
using OpenAC.Net.NFe.Common.Types;
using OpenAC.Net.NFe.Storage;
using OpenAC.Net.NFe.Web;
using Polly.Timeout;

namespace OpenAC.Net.NFe.Tests;

public sealed class WebIntegrationTests
{
    [Test]
    [Arguments(false, true)]
    [Arguments(true, false)]
    public async Task FiscalAndSoapPersistenceRemainIndependent(bool fiscal, bool soap)
    {
        using var certificate = Fixtures.Certificate();
        var store = new RecordingStore();
        var services = Services(certificate, new RecordingClients(), configuration =>
        {
            configuration.Arquivos.Salvar = fiscal;
            configuration.WebServices.Salvar = soap;
        });
        services.AddSingleton<IDFeDocumentStore>(store);
        using var provider = services.BuildServiceProvider(ValidateServices);
        using var scope = provider.CreateScope();
        var client = await scope.ServiceProvider.GetRequiredService<IOpenNFeClientFactory>().CriarAsync();
        await client.ConsultarSituacaoServicoAsync();
        await Assert.That(store.Documents.Count).IsEqualTo(soap ? 2 : 0);
    }

    [Test]
    public async Task ClientsReuseTheirTenantPoolAndKeepTransportErrorsWithoutRetrying()
    {
        using var certificate = Fixtures.Certificate();
        var clients = new RecordingClients();
        using var provider = Services(certificate, clients).BuildServiceProvider(ValidateServices);
        using var scope = provider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IOpenNFeClientFactory>();
        var first = await factory.CriarAsync("empresa-a");
        var second = await factory.CriarAsync("empresa-b");
        await Assert.That((await first.ConsultarSituacaoServicoAsync()).CStat).IsEqualTo(107);
        await Assert.That((await second.ConsultarSituacaoServicoAsync()).CStat).IsEqualTo(107);
        await first.ConsultarSituacaoServicoAsync();
        await Assert.That(clients.Names[0]).IsNotEqualTo(clients.Names[1]);
        await Assert.That(clients.Names[0]).IsEqualTo(clients.Names[2]);
        clients.Failure = new HttpRequestException("Falha simulada.");
        var error = await Assert.ThrowsExactlyAsync<NFeTechnicalException>(() => first.ConsultarSituacaoServicoAsync());
        await Assert.That(error.InnerException).IsSameReferenceAs(clients.Failure);
        await Assert.That(clients.Calls).IsEqualTo(4);
    }

    [Test]
    public async Task FiscalRejectionsRemainInTheResult()
    {
        using var certificate = Fixtures.Certificate();
        var clients = new RecordingClients { Status = 108 };
        using var provider = Services(certificate, clients).BuildServiceProvider(ValidateServices);
        using var scope = provider.CreateScope();
        var client = await scope.ServiceProvider.GetRequiredService<IOpenNFeClientFactory>().CriarAsync();
        await Assert.That((await client.ConsultarSituacaoServicoAsync()).CStat).IsEqualTo(108);
        await Assert.That(clients.Calls).IsEqualTo(1);
    }

    [Test]
    public async Task CertificateConfigurationAndClientSnapshotsDoNotChangeTheProviderConfiguration()
    {
        using var certificate = Fixtures.Certificate();
        var configuration = Configuration(certificate);
        configuration.Certificados.Senha = "senha-original";
        configuration.Arquivos.Salvar = configuration.WebServices.Salvar = true;
        var clients = new RecordingClients();
        var store = new RecordingStore();
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddSingleton<IHttpClientFactory>(clients);
        services.AddSingleton<IDFeDocumentStore>(store);
        services.AddNFe<SharedConfigurationProvider, TenantCertificateProvider>();
        using var provider = services.BuildServiceProvider(ValidateServices);
        using var scope = provider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IOpenNFeClientFactory>();
        var first = await factory.CriarAsync("empresa-a");
        configuration.WebServices.Enderecos[TipoServicoNFe.NFeStatusServico] = "https://segunda.test/status";
        var second = await factory.CriarAsync("empresa-b");
        await first.ConsultarSituacaoServicoAsync();
        await second.ConsultarSituacaoServicoAsync();
        await Assert.That(configuration.Certificados.Senha).IsEqualTo("senha-original");
        await Assert.That(configuration.Arquivos.Salvar).IsTrue();
        await Assert.That(configuration.WebServices.Salvar).IsTrue();
        await Assert.That(clients.Endpoints[0].Host).IsEqualTo("example.test");
        await Assert.That(clients.Endpoints[1].Host).IsEqualTo("segunda.test");
        await Assert.That(store.Documents.Count).IsEqualTo(4);
    }

    [Test]
    public async Task PersistenceUsesTheInjectedStoreAndASeparateDirectoryForEachTenant()
    {
        using var certificate = Fixtures.Certificate();
        var clients = new RecordingClients();
        var store = new RecordingStore();
        var root = Path.Combine(Path.GetTempPath(), "nfe-web-" + Guid.NewGuid().ToString("N"));
        var services = Services(certificate, clients, options =>
        {
            options.Arquivos.Salvar = options.WebServices.Salvar = true;
            options.Arquivos.PathSalvar = root;
        });
        services.AddSingleton<IDFeDocumentStore>(store);
        using var provider = services.BuildServiceProvider(ValidateServices);
        using var scope = provider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IOpenNFeClientFactory>();
        var first = await factory.CriarAsync("../../empresa-a");
        var second = await factory.CriarAsync("empresa-b");
        await first.ConsultarSituacaoServicoAsync();
        await second.ConsultarSituacaoServicoAsync();
        await Assert.That(store.Documents.Count).IsEqualTo(4);
        var paths = store.Documents.Select(document => Path.GetDirectoryName(document.Caminho)!).Distinct().ToArray();
        await Assert.That(paths.Length).IsEqualTo(2);
        foreach (var path in paths)
        {
            await Assert.That(Path.GetDirectoryName(path)).IsEqualTo(root);
            await Assert.That(Path.GetFileName(path)).Matches("^[A-F0-9]{64}$");
        }
        await Assert.That(Directory.Exists(root)).IsFalse();
    }

    [Test]
    public async Task OperationDeadlineIncludesTheStoreBeforeSendingSoap()
    {
        using var certificate = Fixtures.Certificate();
        var clients = new RecordingClients();
        var store = new RecordingStore { Wait = true };
        var services = Services(certificate, clients, options =>
        {
            options.Arquivos.Salvar = options.WebServices.Salvar = true;
            options.WebServices.TimeoutOperacao = TimeSpan.FromMilliseconds(150);
        });
        services.AddSingleton<IDFeDocumentStore>(store);
        using var provider = services.BuildServiceProvider(ValidateServices);
        using var scope = provider.CreateScope();
        var client = await scope.ServiceProvider.GetRequiredService<IOpenNFeClientFactory>().CriarAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => client.ConsultarSituacaoServicoAsync().WaitAsync(TimeSpan.FromSeconds(5)));
        await Assert.That(store.Documents.Count).IsEqualTo(1);
        await Assert.That(clients.Calls).IsEqualTo(0);
    }

    [Test]
    public async Task CallerCancellationRemainsCancellation()
    {
        using var certificate = Fixtures.Certificate();
        var clients = new RecordingClients { Wait = true };
        using var provider = Services(certificate, clients).BuildServiceProvider(ValidateServices);
        using var scope = provider.CreateScope();
        var client = await scope.ServiceProvider.GetRequiredService<IOpenNFeClientFactory>().CriarAsync();
        using var cancellation = new CancellationTokenSource();
        var operation = client.ConsultarSituacaoServicoAsync(cancellation.Token);
        try
        {
            await clients.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(() => operation);
            await Assert.That(clients.Calls).IsEqualTo(1);
        }
        finally { cancellation.Cancel(); }
    }

    [Test]
    public async Task FactoryAndEachOperationDisposeTheirOwnCertificate()
    {
        using var certificate = Fixtures.Certificate();
        var reader = new RecordingCertificateReader(certificate);
        var services = Services(certificate, new RecordingClients());
        services.AddSingleton<INFeCertificadoProvider>(reader);
        using var provider = services.BuildServiceProvider(ValidateServices);
        using var scope = provider.CreateScope();
        var client = await scope.ServiceProvider.GetRequiredService<IOpenNFeClientFactory>().CriarAsync();
        await client.ConsultarSituacaoServicoAsync();
        await client.ConsultarSituacaoServicoAsync();
        await Assert.That(reader.Certificates.Count).IsEqualTo(3);
        foreach (var owned in reader.Certificates)
            await Assert.That(owned.Handle).IsEqualTo(IntPtr.Zero);
        await Assert.That(certificate.HasPrivateKey).IsTrue();
    }

    [Test]
    public async Task FactoryRejectsCertificatesWithinTheExpirationMargin()
    {
        using var certificate = Fixtures.Certificate();
        var services = Services(certificate, new RecordingClients(), options => options.Certificados.MargemValidadeCertificado = TimeSpan.FromDays(2));
        using var provider = services.BuildServiceProvider(ValidateServices);
        using var scope = provider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IOpenNFeClientFactory>();
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await factory.CriarAsync());
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task HttpPolicyMatchesTheCTePoolAndAddsResilienceWithoutRetrying(bool breaker)
    {
        using var certificate = Fixtures.Certificate();
        var clients = new RecordingClients();
        var services = Services(certificate, clients, options =>
        {
            options.WebServices.CircuitBreakerHabilitado = breaker;
            options.WebServices.TempoVidaHandler = TimeSpan.FromMinutes(7);
            options.WebServices.TempoVidaConexaoPool = TimeSpan.FromMinutes(3);
        });
        using var provider = services.BuildServiceProvider(ValidateServices);
        using var scope = provider.CreateScope();
        var client = await scope.ServiceProvider.GetRequiredService<IOpenNFeClientFactory>().CriarAsync();
        await client.ConsultarSituacaoServicoAsync();
        var name = clients.Names.Single();
        var options = provider.GetRequiredService<IOptionsMonitor<HttpClientFactoryOptions>>().Get(name);
        var builder = new ProbeBuilder { Name = name };
        foreach (var configure in options.HttpMessageHandlerBuilderActions) configure(builder);
        using var primary = (SocketsHttpHandler)builder.PrimaryHandler;
        try
        {
            await Assert.That(options.HandlerLifetime).IsEqualTo(TimeSpan.FromMinutes(7));
            await Assert.That(primary.PooledConnectionLifetime).IsEqualTo(TimeSpan.FromMinutes(3));
            await Assert.That(primary.AllowAutoRedirect).IsFalse();
            await Assert.That(primary.UseCookies).IsFalse();
            await Assert.That(primary.AutomaticDecompression).IsEqualTo(DecompressionMethods.All);
            await Assert.That(primary.SslOptions.EnabledSslProtocols).IsEqualTo(SslProtocols.Tls12);
            await Assert.That(primary.SslOptions.ClientCertificates!.Count).IsEqualTo(1);
            await Assert.That(builder.AdditionalHandlers.Count).IsEqualTo(1);
            await Assert.That(builder.AdditionalHandlers[0]).IsAssignableTo<ResilienceHandler>();
            var attempt = new AttemptHandler();
            builder.AdditionalHandlers[0].InnerHandler = attempt;
            using var http = new HttpClient(builder.AdditionalHandlers[0], false);
            await Assert.ThrowsExactlyAsync<HttpRequestException>(() => http.PostAsync("https://example.test/probe", new StringContent("<fiscal/>")));
            await Assert.That(attempt.Calls).IsEqualTo(1);
        }
        finally { foreach (var handler in builder.AdditionalHandlers) handler.Dispose(); }
    }

    [Test]
    public async Task AttemptTimeoutIsAppliedByTheResiliencePipeline()
    {
        using var certificate = Fixtures.Certificate();
        var clients = new RecordingClients();
        using var provider = Services(certificate, clients, value => value.WebServices.TimeoutTentativa = TimeSpan.FromMilliseconds(70))
            .BuildServiceProvider(ValidateServices);
        using var scope = provider.CreateScope();
        var client = await scope.ServiceProvider.GetRequiredService<IOpenNFeClientFactory>().CriarAsync();
        await client.ConsultarSituacaoServicoAsync();
        var options = provider.GetRequiredService<IOptionsMonitor<HttpClientFactoryOptions>>().Get(clients.Names.Single());
        var builder = new ProbeBuilder();
        foreach (var configure in options.HttpMessageHandlerBuilderActions) configure(builder);
        using var primary = builder.PrimaryHandler;
        using var resilience = builder.AdditionalHandlers.Single();
        var attempt = new AttemptHandler { Wait = true };
        resilience.InnerHandler = attempt;
        using var http = new HttpClient(resilience, false);
        await Assert.ThrowsExactlyAsync<TimeoutRejectedException>(() => http.GetAsync("https://example.test/probe").WaitAsync(TimeSpan.FromSeconds(5)));
        await Assert.That(attempt.Calls).IsEqualTo(1);
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    [Arguments(4)]
    [Arguments(5)]
    public async Task InvalidInfrastructureOptionsFailValidation(int option)
    {
        using var certificate = Fixtures.Certificate();
        var services = Services(certificate, new RecordingClients(), value =>
        {
            switch (option)
            {
                case 0: value.WebServices.TimeoutOperacao = TimeSpan.Zero; break;
                case 1: value.WebServices.TimeoutTentativa = TimeSpan.Zero; break;
                case 2: value.WebServices.TempoVidaHandler = TimeSpan.Zero; break;
                case 3: value.WebServices.TempoVidaConexaoPool = TimeSpan.Zero; break;
                case 4: value.Certificados.MargemValidadeCertificado = TimeSpan.FromMinutes(-1); break;
                case 5: value.Arquivos.Salvar = true; value.Arquivos.PathSalvar = ""; break;
            }
        });
        using var provider = services.BuildServiceProvider(ValidateServices);
        using var scope = provider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IOpenNFeClientFactory>();
        await Assert.That(async () => await factory.CriarAsync()).Throws<ArgumentException>();
    }

    [Test]
    public async Task AtomicDocumentStoreKeepsCompleteDocumentsUnderConcurrentReplacement()
    {
        var directory = Path.Combine(Path.GetTempPath(), "nfe-store-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "documento.xml");
        try
        {
            var store = new LocalDocumentStore();
            var values = Enumerable.Range(0, 12).Select(index => $"<doc>{new string((char)('a' + index), 8192)}</doc>").ToArray();
            await Task.WhenAll(values.Select(value => store.SaveAsync(new DFeDocument(path, value, DFeArquivoExiste.Sobrescrever))));
            await Assert.That(values).Contains(await File.ReadAllTextAsync(path));
            await Assert.That(Directory.GetFiles(directory).Length).IsEqualTo(1);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    private static ServiceProviderOptions ValidateServices => new() { ValidateScopes = true, ValidateOnBuild = true };

    private static NFeConfig Configuration(X509Certificate2 certificate)
    {
        var configuration = new NFeConfig();
        configuration.Certificados.CertificadoBytes = certificate.Export(X509ContentType.Pfx, "test");
        configuration.Certificados.Senha = "test";
        configuration.Arquivos.PathSchemas = Path.Combine(AppContext.BaseDirectory, "Schemas");
        configuration.WebServices.Enderecos[TipoServicoNFe.NFeStatusServico] = "https://example.test/status";
        return configuration;
    }

    private static ServiceCollection Services(X509Certificate2 certificate, RecordingClients clients, Action<NFeConfig>? infrastructure = null)
    {
        var configuration = Configuration(certificate);
        var services = new ServiceCollection();
        services.AddSingleton<IHttpClientFactory>(clients);
        services.AddNFe(value =>
        {
            value.Certificados.CertificadoBytes = configuration.Certificados.CertificadoBytes;
            value.Certificados.Senha = configuration.Certificados.Senha;
            value.Arquivos.PathSchemas = configuration.Arquivos.PathSchemas;
            value.WebServices.Enderecos[TipoServicoNFe.NFeStatusServico] = configuration.WebServices.Enderecos[TipoServicoNFe.NFeStatusServico];
            infrastructure?.Invoke(value);
        });
        return services;
    }

    private sealed class SharedConfigurationProvider(NFeConfig configuration) : INFeConfigurationProvider<NFeConfig>
    {
        public ValueTask<NFeConfig> GetConfigurationAsync(string tenantId = "default", CancellationToken cancellationToken = default) => new(configuration);
    }

    private sealed class TenantCertificateProvider : INFeCertificateProvider
    {
        public ValueTask ConfigureAsync(string tenantId, NFeConfigCertificados configuration, CancellationToken cancellationToken = default)
        {
            configuration.Senha = "test";
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingCertificateReader(X509Certificate2 certificate) : INFeCertificadoProvider
    {
        internal List<X509Certificate2> Certificates { get; } = [];
        public Task<X509Certificate2> ObterAsync(NFeConfigCertificados configuration, CancellationToken cancellationToken = default)
        {
            var owned = new X509Certificate2(certificate);
            Certificates.Add(owned);
            return Task.FromResult(owned);
        }
    }

    private sealed class RecordingClients : IHttpClientFactory
    {
        internal List<string> Names { get; } = [];
        internal List<Uri> Endpoints { get; } = [];
        internal TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Exception? Failure;
        internal bool Wait;
        internal int Calls;
        internal int Status = 107;
        public HttpClient CreateClient(string name)
        {
            Names.Add(name);
            return new HttpClient(new Handler(this));
        }

        private sealed class Handler(RecordingClients owner) : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                owner.Calls++;
                owner.Endpoints.Add(request.RequestUri!);
                owner.Started.TrySetResult(true);
                if (owner.Failure != null) throw owner.Failure;
                if (owner.Wait) await Task.Delay(Timeout.Infinite, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($"<s:Envelope xmlns:s='http://www.w3.org/2003/05/soap-envelope'><s:Body><retConsStatServ xmlns='http://www.portalfiscal.inf.br/nfe' versao='4.00'><tpAmb>2</tpAmb><cStat>{owner.Status}</cStat><xMotivo>Resposta simulada</xMotivo></retConsStatServ></s:Body></s:Envelope>")
                };
            }
        }
    }

    private sealed class RecordingStore : IDFeDocumentStore
    {
        internal List<IDFeDocument> Documents { get; } = [];
        internal bool Wait;
        public async Task SaveAsync(IDFeDocument document, CancellationToken cancellationToken = default)
        {
            Documents.Add(document);
            if (Wait) await Task.Delay(Timeout.Infinite, cancellationToken);
        }
    }

    private sealed class ProbeBuilder : HttpMessageHandlerBuilder
    {
        public override string? Name { get; set; }
        public override HttpMessageHandler PrimaryHandler { get; set; } = null!;
        public override IList<DelegatingHandler> AdditionalHandlers { get; } = new List<DelegatingHandler>();
        public override HttpMessageHandler Build() => PrimaryHandler;
    }

    private sealed class AttemptHandler : HttpMessageHandler
    {
        internal bool Wait;
        internal int Calls;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            if (Wait) await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new HttpRequestException("Falha simulada na tentativa HTTP.");
        }
    }
}
