using OpenAC.Net.NFe.Certificates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using OpenAC.Net.NFe.Common;
using OpenAC.Net.NFe.Storage;
using OpenAC.Net.DFe.Core.Storage;

namespace OpenAC.Net.NFe.Web;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNFe(this IServiceCollection services, Action<NFeConfig> configure)
    {
        if (configure == null) throw new ArgumentNullException(nameof(configure));
        services.AddSingleton<INFeConfigurationProvider<NFeConfig>>(new DefaultNFeConfigurationProvider(configure));
        services.TryAddSingleton<INFeCertificateProvider, DefaultNFeCertificateProvider>();
        return AddInfrastructure(services);
    }

    public static IServiceCollection AddNFe<TConfigurationProvider, TCertificateProvider>(
        this IServiceCollection services)
        where TConfigurationProvider : class, INFeConfigurationProvider<NFeConfig>
        where TCertificateProvider : class, INFeCertificateProvider
    {
        services.AddScoped<INFeConfigurationProvider<NFeConfig>, TConfigurationProvider>();
        services.AddScoped<INFeCertificateProvider, TCertificateProvider>();
        return AddInfrastructure(services);
    }

    private static IServiceCollection AddInfrastructure(IServiceCollection services)
    {
        services.TryAddSingleton<INFeCertificadoProvider, NFeCertificadoProvider>();
        services.AddHttpClient();
        services.TryAddSingleton<NFeHttpClientRegistry>();
        services.TryAddEnumerable(ServiceDescriptor
            .Singleton<IConfigureOptions<HttpClientFactoryOptions>, NFeHttpClientOptions>());
        services.TryAddSingleton<IDFeDocumentStore, LocalDocumentStore>();
        services.TryAddScoped<IOpenNFeClientFactory, OpenNFeClientFactory>();
        return services;
    }
}