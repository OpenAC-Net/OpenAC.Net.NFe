using System.Collections.Generic;

namespace OpenAC.Net.NFe.Common.Model;

public sealed class NFeAutorizacaoResposta : NFeResposta<NFeAutorizacaoResult>
{
    public NFeAutorizacaoResposta(string xmlEnvio, string xmlRetorno, string envelopeEnvio = "", string envelopeRetorno = "")
        : base(xmlEnvio, xmlRetorno, envelopeEnvio, envelopeRetorno) { }

    public string Recibo => Resultado.InfRec?.Recibo ?? "";
    public IReadOnlyList<NFe> DocumentosEnviados { get; internal set; } = [];
    public IReadOnlyList<NFeProc> Documentos { get; internal set; } = [];
}
