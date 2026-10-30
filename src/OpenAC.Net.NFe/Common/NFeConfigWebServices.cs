using System;
using System.Collections.Generic;
using System.Net;
using OpenAC.Net.DFe.Core.Common;
using OpenAC.Net.NFe.Common.Types;

namespace OpenAC.Net.NFe.Common;

public sealed class NFeConfigWebServices : DFeWebserviceConfigBase
{
    public NFeConfigWebServices() => Protocolos = SecurityProtocolType.Tls12;
    public DFeSiglaUF UF { get; set; } = DFeSiglaUF.SP;
    public TimeSpan TimeoutOperacao { get; set; } = TimeSpan.FromSeconds(60);
    public Dictionary<TipoServicoNFe, string> Enderecos { get; } = new();
}
