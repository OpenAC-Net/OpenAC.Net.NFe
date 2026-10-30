using System.Threading;
using OpenAC.Net.NFe.Common.Model;
using OpenAC.Net.NFe.DANFe.PDFSharp.Configuracao;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace OpenAC.Net.NFe.DANFe.PDFSharp.Layout;

internal static class DANFeInutilizacaoLayout
{
    internal static void Renderizar(PdfDocument pdf, NFeProcInutilizacao documento, DANFeConfig configuracao, CancellationToken token)
    {
        var pedido = documento.Inutilizacao.InfInut;
        var retorno = documento.Retorno?.InfInut;
        using var r = new DANFeReport(pdf, XUnit.FromMillimeter(210).Point, XUnit.FromMillimeter(297).Point,
            XUnit.FromMillimeter(configuracao.MargemMilimetros).Point, token, cabecalho =>
            {
                cabecalho.Logo(configuracao.Logo);
                cabecalho.Texto("INUTILIZAÇÃO DE NUMERAÇÃO NF-e / NFC-e", true, true);
                if (NFeImpressao.Texto(pedido.TpAmb) == "2") cabecalho.Texto("HOMOLOGAÇÃO - SEM VALOR FISCAL", true);
                if (retorno?.CStat != 102) cabecalho.Texto("INUTILIZAÇÃO NÃO HOMOLOGADA", true);
            });
        r.Titulo("IDENTIFICAÇÃO DA SOLICITAÇÃO");
        r.Quadros(("CNPJ", NFeImpressao.Documento(pedido.CNPJ), 2), ("ANO", pedido.Ano, 1),
            ("MODELO", pedido.Modelo, 1), ("SÉRIE", pedido.Serie, 1));
        r.Quadros(("NÚMERO INICIAL", pedido.NumeroInicial, 1), ("NÚMERO FINAL", pedido.NumeroFinal, 1));
        r.Secao("JUSTIFICATIVA", new[] { pedido.Justificativa });
        if (retorno != null)
        {
            r.Titulo("PROTOCOLO DE INUTILIZAÇÃO");
            r.Quadros(("PROTOCOLO", retorno.Protocolo, 1), ("DATA DO RECEBIMENTO", NFeImpressao.Data(retorno.DataRecebimento), 1));
            r.Texto("Situação: " + retorno.CStat + " " + retorno.XMotivo);
        }
    }
}
