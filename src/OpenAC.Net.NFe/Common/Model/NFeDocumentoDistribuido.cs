namespace OpenAC.Net.NFe.Common.Model;

public sealed class NFeDocumentoDistribuido(string nsu, string schema, string xml)
{
    public string NSU { get; } = nsu;
    public string Schema { get; } = schema;
    public string Xml { get; } = xml;
}