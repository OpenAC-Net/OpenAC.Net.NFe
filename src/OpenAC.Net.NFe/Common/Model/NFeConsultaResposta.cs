
namespace OpenAC.Net.NFe.Common.Model;

public sealed class NFeConsultaResposta : NFeResposta<NFeConsultaResult>
{
    public NFeConsultaResposta(string xmlEnvio, string xmlRetorno, string envelopeEnvio = "", string envelopeRetorno = "")
        : base(xmlEnvio, xmlRetorno, envelopeEnvio, envelopeRetorno) { }
}
