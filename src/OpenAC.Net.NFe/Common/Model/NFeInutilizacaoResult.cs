using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Document;

namespace OpenAC.Net.NFe.Common.Model;

[DFeRoot("retInutNFe", Namespace = NFeConst.Namespace)]
public sealed partial class NFeInutilizacaoResult : NFeResultBase<NFeInutilizacaoResult>
{
    [DFeElement("infInut", Ordem = 110, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public NFeRetInutilizacaoInfo InfInut { get; set; }

    public override int Codigo => InfInut?.CStat ?? base.Codigo;
    public override string Motivo => InfInut?.XMotivo ?? base.Motivo;
    protected override bool ShouldSerializeCStat() => false;
    [DFeElement("Signature", Namespace = "http://www.w3.org/2000/09/xmldsig#", Ordem = int.MaxValue,
        Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public DFeSignature Signature { get; set; }
}
