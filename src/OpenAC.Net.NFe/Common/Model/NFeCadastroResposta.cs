
namespace OpenAC.Net.NFe.Common.Model;

public sealed class NFeCadastroResposta : NFeResposta<NFeCadastroResult>
{
    public NFeCadastroResposta(string xmlEnvio, string xmlRetorno, string envelopeEnvio = "", string envelopeRetorno = "")
        : base(xmlEnvio, xmlRetorno, envelopeEnvio, envelopeRetorno) { }
}
