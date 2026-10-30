using OpenAC.Net.DFe.Core.Attributes;

namespace OpenAC.Net.NFe.Common.Model;

[DFeRoot("retConsCad", Namespace = NFeConst.Namespace)]
public sealed partial class NFeCadastroResult : NFeResultBase<NFeCadastroResult>
{
    [DFeElement("infCons", Ordem = 110, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public NFeRetCadastroInfo InfCons { get; set; }

    public override int Codigo => InfCons?.CStat ?? base.Codigo;
    public override string Motivo => InfCons?.XMotivo ?? base.Motivo;
    protected override bool ShouldSerializeCStat() => false;
}
