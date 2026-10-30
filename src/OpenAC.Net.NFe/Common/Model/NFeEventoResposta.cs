using System.Collections.Generic;

namespace OpenAC.Net.NFe.Common.Model;

public sealed class NFeEventoResposta : NFeResposta<NFeEventoResult>
{
    public NFeEventoResposta(string xmlEnvio, string xmlRetorno, string envelopeEnvio = "", string envelopeRetorno = "")
        : base(xmlEnvio, xmlRetorno, envelopeEnvio, envelopeRetorno) { }

    public IReadOnlyList<NFeProcEvento> Eventos { get; internal set; } = [];
}
