using System;
using OpenAC.Net.NFe.Common.Model;
using OpenAC.Net.NFe.Common.Types;

namespace OpenAC.Net.NFe;

public sealed partial class NFeDest
{
    internal bool ShouldSerializeCPF() => string.IsNullOrEmpty(CNPJ);
    internal bool ShouldSerializeCNPJ() => string.IsNullOrEmpty(CPF);
    internal bool ShouldSerializeEnderDest() => Raiz?.Ide?.Mod != ModeloNFe.NFCe || !string.IsNullOrEmpty(EnderDest?.XLgr);
    internal bool ShouldSerializeIE() => IndIEDest != IndIEDest.Isento && !string.IsNullOrEmpty(IE);
}

public sealed partial class NFeInfNFe
{
    internal bool ShouldSerializeAvulsa() => !string.IsNullOrEmpty(Avulsa?.CNPJ);
    // O legado verificava o complemento; o campo identificador do endereço é o logradouro.
    internal bool ShouldSerializeRetirada() => !string.IsNullOrEmpty(Retirada?.XLgr);
    internal bool ShouldSerializeEntrega() => !string.IsNullOrEmpty(Entrega?.XLgr);
}

public sealed partial class NFeIde
{
    internal bool ShouldSerializeDhSaiEnt() => Mod == ModeloNFe.NFe && DhSaiEnt.HasValue;
    internal bool ShouldSerializeDhCont() => TpEmis != TipoEmissao.Normal && DhCont.HasValue;
    internal bool ShouldSerializeXJust() => TpEmis != TipoEmissao.Normal && !string.IsNullOrEmpty(XJust);
}

public sealed partial class NFeDetImposto
{
    internal bool ShouldSerializeII() => Imposto is NFeDetImpostoICMS;
}
