namespace OpenAC.Net.NFe.Web;

public sealed class NFeTechnicalException(string message, Exception innerException)
    : Exception(message, innerException);
