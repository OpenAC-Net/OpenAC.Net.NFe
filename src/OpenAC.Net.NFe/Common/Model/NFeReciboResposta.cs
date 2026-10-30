using System.Collections.Generic;

namespace OpenAC.Net.NFe.Common.Model;

public sealed class NFeReciboResposta : NFeResposta<NFeReciboResult>
{
    public NFeReciboResposta(string xmlEnvio, string xmlRetorno, string envelopeEnvio = "", string envelopeRetorno = "")
        : base(xmlEnvio, xmlRetorno, envelopeEnvio, envelopeRetorno) { }

    public IReadOnlyList<NFeProc> Documentos { get; internal set; } = [];
}
