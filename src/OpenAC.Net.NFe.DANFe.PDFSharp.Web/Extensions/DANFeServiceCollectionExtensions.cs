using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenAC.Net.NFe.DANFe.PDFSharp.Configuracao;

namespace OpenAC.Net.NFe.DANFe.PDFSharp.Web;

public static class DANFeServiceCollectionExtensions
{
    public static IServiceCollection AddDANFePdfSharp(this IServiceCollection services, Action<DANFeConfig>? configurar = null)
    {
        services.TryAddSingleton<IDANFeConfigurationProvider>(new DANFeConfigurationProvider(configurar ?? (_ => { })));
        services.TryAddScoped<IOpenDANFeWeb, OpenDANFeWeb>();
        return services;
    }
}
