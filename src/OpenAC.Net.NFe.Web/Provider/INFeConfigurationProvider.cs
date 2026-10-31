namespace OpenAC.Net.NFe.Web;

/// <summary>Fornece a configuração da empresa. A fábrica captura uma cópia antes de configurar a operação.</summary>
public interface INFeConfigurationProvider<TConfiguration>
{
    ValueTask<TConfiguration> GetConfigurationAsync(string tenantId = "default",
        CancellationToken cancellationToken = default);
}
