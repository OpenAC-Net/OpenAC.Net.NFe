namespace OpenAC.Net.NFe.Web;

public interface IOpenNFeClientFactory
{
    ValueTask<IOpenNFeClient> CriarAsync(string tenantId = "default", CancellationToken cancellationToken = default);
}
