
namespace OpenAC.Net.NFe.Common.Model;

public sealed class NFeStatusServicoResposta : NFeResposta<NFeStatusServicoResult>
{
    public NFeStatusServicoResposta(string xmlEnvio, string xmlRetorno, string envelopeEnvio = "", string envelopeRetorno = "")
        : base(xmlEnvio, xmlRetorno, envelopeEnvio, envelopeRetorno) { }
}
