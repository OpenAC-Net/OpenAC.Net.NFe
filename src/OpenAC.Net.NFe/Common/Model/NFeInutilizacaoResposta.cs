namespace OpenAC.Net.NFe.Common.Model;

public sealed class NFeInutilizacaoResposta : NFeResposta<NFeInutilizacaoResult>
{
    public NFeInutilizacaoResposta(string xmlEnvio, string xmlRetorno, string envelopeEnvio = "", string envelopeRetorno = "")
        : base(xmlEnvio, xmlRetorno, envelopeEnvio, envelopeRetorno) { }

    public NFeProcInutilizacao Documento { get; internal set; }
}
