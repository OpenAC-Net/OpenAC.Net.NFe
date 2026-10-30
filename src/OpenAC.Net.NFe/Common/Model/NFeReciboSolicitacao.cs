using System.Collections.Generic;

namespace OpenAC.Net.NFe;

public sealed class NFeReciboSolicitacao
{
    public string Recibo { get; set; }
    public IEnumerable<Common.Model.NFe> Documentos { get; set; } = [];
}
