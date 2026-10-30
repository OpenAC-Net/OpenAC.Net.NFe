using System.Collections.Generic;

namespace OpenAC.Net.NFe;

public sealed class NFeEventoSolicitacao
{
    public string Lote { get; set; } = "1";
    public IEnumerable<Common.Model.NFeEvento> Eventos { get; set; } = [];
}
