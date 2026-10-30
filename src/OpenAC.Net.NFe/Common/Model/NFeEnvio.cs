using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Collection;
using OpenAC.Net.DFe.Core.Document;
using OpenAC.Net.DFe.Core.Serializer;

namespace OpenAC.Net.NFe.Common.Model;

[DFeRoot("enviNFe", Namespace = NFeConst.Namespace)]
public sealed partial class NFeEnvio : DFeDocument<NFeEnvio>
{
    [DFeAttribute(TipoCampo.Str, "versao", Ocorrencia = Ocorrencia.Obrigatoria)]
    public string Versao { get; set; } = "4.00";

    [DFeElement(TipoCampo.Str, "idLote", Ordem = 20, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Lote { get; set; }

    [DFeElement(TipoCampo.Str, "indSinc", Ordem = 30, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string IndicadorSincrono { get; set; }

    [DFeCollection("NFe", Ordem = 40, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public DFeCollection<NFe> Documentos { get; set; } = new();
}
