using System;
using System.IO;
using OpenAC.Net.DFe.Core.Common;
using OpenAC.Net.DFe.Core.Service;
using OpenAC.Net.NFe.Common.Types;
using OpenAC.Net.NFe.Services;

namespace OpenAC.Net.NFe.Common;

public sealed partial class NFeConfigArquivos : DFeArquivosConfigBase<SchemaNFe>
{
    private readonly NFeConfigGeral geral;

    internal NFeConfigArquivos(NFeConfigGeral geral)
    {
        this.geral = geral;
        PathSchemas = Path.Combine(AppContext.BaseDirectory, "Schemas");
    }

    public override string GetSchema(SchemaNFe schema)
    {
        var version = geral.VersaoDFe == NFeVersao.v310 ? "3.10" : "4.00";
        var file = schema switch
        {
            SchemaNFe.NFe => "nfe_v" + version,
            SchemaNFe.ProcNFe => "procNFe_v" + version,
            SchemaNFe.EnviNFe => "enviNFe_v" + version,
            SchemaNFe.ConsStatServ => "consStatServ_v" + version,
            SchemaNFe.ConsSitNFe => "consSitNFe_v" + version,
            SchemaNFe.ConsReciNFe => "consReciNFe_v" + version,
            SchemaNFe.InutNFe => "inutNFe_v" + version,
            SchemaNFe.EnvEvento => "envEvento_v1.00",
            SchemaNFe.ConsCad => "consCad_v2.00",
            SchemaNFe.DistDFeInt => "distDFeInt_v1.01",
            _ => throw new ArgumentOutOfRangeException(nameof(schema))
        };
        return Path.Combine(PathSchemas, file + ".xsd");
    }

    internal DFeServices<TipoServicoNFe> Catalogo { get; private set; }

    internal void CopiarCatalogo(NFeConfigArquivos origem)
    {
        if (origem.Catalogo == null) return;
        Catalogo = DFeServices<TipoServicoNFe>.Load(origem.Catalogo.GetXml());
    }

    protected override void ArquivoServicoChange()
    {
        if (string.IsNullOrWhiteSpace(ArquivoServicos)) { Catalogo = null; return; }
        using var stream = File.OpenRead(ArquivoServicos);
        var catalogo = NFeServiceManager.CarregarCatalogo(stream);
        Catalogo = catalogo;
    }
}
