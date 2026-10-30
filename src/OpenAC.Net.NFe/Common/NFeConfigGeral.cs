using OpenAC.Net.DFe.Core.Common;
using OpenAC.Net.NFe.Common.Types;

namespace OpenAC.Net.NFe.Common;

public sealed class NFeConfigGeral : DFeGeralConfigBase<NFeVersao>
{
    public NFeConfigGeral() { VersaoDFe = NFeVersao.v400; FormaEmissao = DFeTipoEmissao.Normal; }
    public ModeloNFe Modelo { get; set; } = ModeloNFe.NFe;
    public NFeVersaoQRCode VersaoQRCode { get; set; } = NFeVersaoQRCode.v300;
    public string IdCSC { get; set; }
    public string CSC { get; set; }
}
