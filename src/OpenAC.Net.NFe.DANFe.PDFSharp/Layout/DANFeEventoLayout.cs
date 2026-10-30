using System.Threading;
using OpenAC.Net.NFe.Common.Model;
using OpenAC.Net.NFe.DANFe.PDFSharp.Configuracao;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace OpenAC.Net.NFe.DANFe.PDFSharp.Layout;

internal static class DANFeEventoLayout
{
    internal static void Renderizar(PdfDocument pdf, NFeProcEvento evento, DANFeConfig configuracao, CancellationToken token)
    {
        var info = evento.Evento.InfEvento;
        var retorno = evento.RetEvento?.InfEvento;
        using var r = new DANFeReport(pdf, XUnit.FromMillimeter(210).Point, XUnit.FromMillimeter(297).Point,
            XUnit.FromMillimeter(configuracao.MargemMilimetros).Point, token, cabecalho =>
            {
                cabecalho.Logo(configuracao.Logo);
                cabecalho.Texto("REPRESENTAÇÃO DO EVENTO NF-e / NFC-e", true, true);
                cabecalho.Texto(info.Detalhe?.Descricao ?? "Evento NF-e", true, true);
                cabecalho.Texto("Chave: " + NFeImpressao.Chave(info.Chave));
                if (NFeImpressao.Texto(info.TpAmb) == "2") cabecalho.Texto("HOMOLOGAÇÃO - SEM VALOR FISCAL", true);
                if (retorno?.CStat is not (135 or 136 or 155)) cabecalho.Texto("EVENTO SEM REGISTRO", true);
            });
        r.Titulo("IDENTIFICAÇÃO");
        r.Quadros(("TIPO DO EVENTO", info.Codigo, 1), ("SEQUÊNCIA", info.Sequencia, 1),
            ("DATA DO EVENTO", NFeImpressao.Data(info.Data), 2));
        r.Quadros(("AUTOR CNPJ / CPF", NFeImpressao.Documento(info.CNPJ ?? info.CPF), 2), ("ÓRGÃO", info.Orgao, 1));
        r.CodigoBarras(info.Chave);
        r.Secao("DETALHES DO EVENTO", NFeImpressao.Campos(info.Detalhe));
        if (retorno != null)
        {
            r.Titulo("REGISTRO SEFAZ");
            r.Quadros(("PROTOCOLO", retorno.Protocolo, 1), ("DATA DO REGISTRO", NFeImpressao.Data(retorno.DataRegistro), 1));
            r.Texto("Situação: " + retorno.CStat + " " + retorno.XMotivo);
        }
    }
}
