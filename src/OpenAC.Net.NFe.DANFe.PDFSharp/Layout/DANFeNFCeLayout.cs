using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OpenAC.Net.NFe.Common.Model;
using OpenAC.Net.NFe.DANFe.PDFSharp.Configuracao;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace OpenAC.Net.NFe.DANFe.PDFSharp.Layout;

internal static class DANFeNFCeLayout
{
    internal static void Renderizar(PdfDocument pdf, NFeProc nota, DANFeConfig configuracao, CancellationToken token)
    {
        var largura = XUnit.FromMillimeter(configuracao.LarguraNFCeMilimetros).Point;
        var margem = XUnit.FromMillimeter(configuracao.MargemNFCeMilimetros).Point;
        IReadOnlyList<double> alturas;
        using (var medida = new PdfDocument())
        {
            var relatorio = new DANFeReport(medida, largura, 14400, margem, token, r => Continuacao(r, nota));
            using (relatorio) Desenhar(relatorio, nota, configuracao, token);
            alturas = relatorio.AlturasMedidas.ToArray();
        }
        using var r = new DANFeReport(pdf, largura, 14400, margem, token, r => Continuacao(r, nota), alturas);
        Desenhar(r, nota, configuracao, token);
    }

    private static void Continuacao(DANFeReport r, NFeProc nota)
    {
        if (r.PrimeiraPagina) return;
        r.Texto(nota.NFe.InfNFe.Emit.XNome, true, true);
        r.Texto("NFC-e nº " + nota.NFe.InfNFe.Ide.NNF + " Série " + nota.NFe.InfNFe.Ide.Serie, true, true);
        r.Texto("CONTINUAÇÃO", true, true);
        r.Separador();
    }

    private static void Desenhar(DANFeReport r, NFeProc nota, DANFeConfig configuracao, CancellationToken token)
    {
        var info = nota.NFe.InfNFe;
        var emitente = info.Emit;
        r.Logo(configuracao.Logo);
        r.Texto(emitente.XNome, true, true);
        r.Texto("CNPJ/CPF: " + NFeImpressao.Documento(emitente.CNPJ ?? emitente.CPF), centralizar: true);
        r.Texto("IE: " + emitente.IE, centralizar: true);
        r.Texto(NFeImpressao.Endereco(emitente.EnderEmit), centralizar: true);
        r.Separador();
        r.Texto("DANFE NFC-e", true, true);
        r.Texto("Documento Auxiliar da Nota Fiscal de Consumidor Eletrônica", centralizar: true);
        r.Texto("Não permite aproveitamento de crédito de ICMS", centralizar: true);
        foreach (var aviso in NFeImpressao.Avisos(nota)) r.Texto(aviso, true, true);
        r.Separador();
        r.Texto("CÓDIGO / DESCRIÇÃO", true);
        r.Par("QTD x UN x VL UNIT", "VL TOTAL R$", true);
        foreach (var item in info.Det)
        {
            token.ThrowIfCancellationRequested();
            var p = item.Prod;
            r.Texto(p.CProd + " " + p.XProd);
            r.Par(NFeImpressao.Numero(p.QCom) + " " + p.UCom + " x " + NFeImpressao.PrecoUnitario(p.VUnCom), NFeImpressao.Dinheiro(p.VProd));
            if (p.VDesc > 0) r.Par("Desconto do item", NFeImpressao.Dinheiro(p.VDesc));
        }
        r.Separador();
        var t = info.Total.ICMSTot;
        r.Par("Qtd. total de itens", info.Det.Count.ToString());
        r.Par("Valor total R$", NFeImpressao.Dinheiro(t.VProd));
        if (t.VDesc > 0) r.Par("Descontos R$", NFeImpressao.Dinheiro(t.VDesc));
        var acrescimos = t.VFrete + t.VSeg + t.VOutro;
        if (acrescimos > 0) r.Par("Acréscimos R$", NFeImpressao.Dinheiro(acrescimos));
        r.Par("Valor a pagar R$", NFeImpressao.Dinheiro(t.VNF), true);
        r.Separador();
        r.Par("FORMA DE PAGAMENTO", "VALOR PAGO R$", true);
        foreach (var pagamento in NFeImpressao.Pagamentos(info)) r.Par(pagamento.Descricao, NFeImpressao.Dinheiro(pagamento.Valor));
        foreach (var pagamento in info.Pag) if (pagamento.VTroco.HasValue) r.Par("Troco R$", NFeImpressao.Dinheiro(pagamento.VTroco));
        r.Separador();
        r.Texto("Consulte pela chave de acesso em:", centralizar: true);
        r.Texto(nota.NFe.InfNFeSupl?.UrlChave, centralizar: true);
        r.Texto(NFeImpressao.Chave(nota.Chave), centralizar: true);
        var destinatario = info.Dest;
        var documento = destinatario?.CPF ?? destinatario?.CNPJ ?? destinatario?.IdEstrangeiro;
        r.Separador();
        r.Texto(string.IsNullOrEmpty(documento) ? "CONSUMIDOR NÃO IDENTIFICADO" : "CONSUMIDOR: " + NFeImpressao.Documento(documento), true, true);
        if (!string.IsNullOrEmpty(documento)) r.Texto(destinatario.XNome, centralizar: true);
        r.Texto("NFC-e nº " + info.Ide.NNF + " Série " + info.Ide.Serie, true, true);
        r.Texto("Emissão: " + NFeImpressao.Data(info.Ide.DhEmi), centralizar: true);
        var protocolo = nota.ProtNFe?.InfProt;
        if (protocolo != null)
        {
            r.Texto("Protocolo de autorização: " + protocolo.NProt, centralizar: true);
            r.Texto("Autorização: " + NFeImpressao.Data(protocolo.DhRecbto), centralizar: true);
        }
        r.QrCode(nota.QrCode);
        r.Separador();
        r.Texto("Tributos totais incidentes (Lei Federal 12.741/2012) R$: " + NFeImpressao.Dinheiro(t.VTotTrib));
        r.Secao("IBS / CBS", NFeImpressao.Campos(info.Total.IBSCBSTot));
        if (!string.IsNullOrWhiteSpace(info.InfAdic?.InfCpl)) r.Texto(info.InfAdic.InfCpl);
    }
}
