using System;
using System.Linq;
using System.Threading;
using OpenAC.Net.NFe.Common.Model;
using OpenAC.Net.NFe.DANFe.PDFSharp.Configuracao;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace OpenAC.Net.NFe.DANFe.PDFSharp.Layout;

internal static class DANFeA4Layout
{
    internal static void Renderizar(PdfDocument pdf, NFeProc nota, DANFeConfig configuracao, CancellationToken token)
    {
        var info = nota.NFe.InfNFe;
        var ide = info.Ide;
        var emitente = info.Emit;
        var destino = info.Dest;
        var protocolo = nota.ProtNFe?.InfProt;
        var margem = XUnit.FromMillimeter(configuracao.MargemMilimetros).Point;
        using var r = new DANFeReport(pdf, XUnit.FromMillimeter(configuracao.Paisagem ? 297 : 210).Point,
            XUnit.FromMillimeter(configuracao.Paisagem ? 210 : 297).Point, margem, token, cabecalho =>
            {
                if (cabecalho.PrimeiraPagina && configuracao.ExibirCanhoto)
                {
                    cabecalho.Quadros(("RECEBEMOS DE " + emitente.XNome,
                        "Os produtos/serviços constantes da NF-e indicada ao lado.", 4),
                        ("NF-e", "Nº " + ide.NNF + " - SÉRIE " + ide.Serie, 1));
                    cabecalho.Quadros(("DATA DE RECEBIMENTO", "____/____/________", 1),
                        ("IDENTIFICAÇÃO E ASSINATURA DO RECEBEDOR", "_______________________________________", 4));
                    cabecalho.Separador();
                }
                cabecalho.Identificacao(emitente.XNome, NFeImpressao.Endereco(emitente.EnderEmit),
                    "CNPJ/CPF: " + NFeImpressao.Documento(emitente.CNPJ ?? emitente.CPF),
                    NFeImpressao.Texto(ide.NNF), NFeImpressao.Texto(ide.Serie), NFeImpressao.Texto(ide.TpNF), nota.Chave, configuracao.Logo);
                foreach (var aviso in NFeImpressao.Avisos(nota)) cabecalho.Texto(aviso, true, true);
            });
        r.Quadros(("NATUREZA DA OPERAÇÃO", ide.NatOp, 1),
            ("PROTOCOLO DE AUTORIZAÇÃO DE USO", protocolo?.NProt + " " + NFeImpressao.Data(protocolo?.DhRecbto), 1));
        r.Quadros(("INSCRIÇÃO ESTADUAL", emitente.IE, 1), ("IE DO SUBSTITUTO TRIBUTÁRIO", emitente.IEST, 1),
            ("CNPJ / CPF", NFeImpressao.Documento(emitente.CNPJ ?? emitente.CPF), 1));
        r.Titulo("DESTINATÁRIO / REMETENTE");
        r.Quadros(("NOME / RAZÃO SOCIAL", destino?.XNome, 3),
            ("CNPJ / CPF", NFeImpressao.Documento(destino?.CNPJ ?? destino?.CPF ?? destino?.IdEstrangeiro), 2),
            ("DATA DA EMISSÃO", NFeImpressao.Data(ide.DhEmi), 2));
        var endereco = destino?.EnderDest;
        r.Quadros(("ENDEREÇO", NFeImpressao.Endereco(endereco), 4), ("INSCRIÇÃO ESTADUAL", destino?.IE, 1),
            ("SAÍDA / ENTRADA", NFeImpressao.Data(ide.DhSaiEnt), 2));
        r.Quadros(("MUNICÍPIO / UF", endereco?.XMun + "/" + endereco?.UF, 3), ("CEP", endereco?.CEP, 1),
            ("FONE", endereco?.Fone, 2));
        r.Secao("LOCAL DE RETIRADA", NFeImpressao.Campos(info.Retirada));
        r.Secao("LOCAL DE ENTREGA", NFeImpressao.Campos(info.Entrega));
        if (info.Cobr != null)
        {
            var fatura = info.Cobr.Fat;
            if (fatura != null)
            {
                r.Titulo("FATURA");
                r.Quadros(("NÚMERO", fatura.NFat, 1), ("VALOR ORIGINAL", NFeImpressao.Dinheiro(fatura.VOrig), 1),
                    ("DESCONTO", NFeImpressao.Dinheiro(fatura.VDesc), 1), ("VALOR LÍQUIDO", NFeImpressao.Dinheiro(fatura.VLiq), 1));
            }
            if (info.Cobr.Dup.Count > 0)
            {
                r.Titulo("DUPLICATAS");
                r.Tabela(new[] { "NÚMERO", "VENCIMENTO", "VALOR R$" }, new double[] { 1, 1, 1 },
                    info.Cobr.Dup.Select(duplicata => new[] { duplicata.NDup, NFeImpressao.Data(duplicata.DVenc), NFeImpressao.Dinheiro(duplicata.VDup) }));
            }
        }
        var t = info.Total.ICMSTot;
        r.Titulo("CÁLCULO DO IMPOSTO");
        r.Quadros(("BASE DE CÁLC. DO ICMS", NFeImpressao.Dinheiro(t.VBC), 1),
            ("VALOR DO ICMS", NFeImpressao.Dinheiro(t.VICMS), 1), ("BASE DE CÁLC. ICMS ST", NFeImpressao.Dinheiro(t.VBCST), 1),
            ("VALOR DO ICMS ST", NFeImpressao.Dinheiro(t.VST), 1), ("TOTAL DOS PRODUTOS", NFeImpressao.Dinheiro(t.VProd), 1));
        r.Quadros(("VALOR DO FRETE", NFeImpressao.Dinheiro(t.VFrete), 1), ("VALOR DO SEGURO", NFeImpressao.Dinheiro(t.VSeg), 1),
            ("DESCONTO", NFeImpressao.Dinheiro(t.VDesc), 1), ("OUTRAS DESPESAS", NFeImpressao.Dinheiro(t.VOutro), 1),
            ("VALOR DO IPI", NFeImpressao.Dinheiro(t.VIPI), 1), ("TOTAL DA NOTA", NFeImpressao.Dinheiro(t.VNF), 1));
        r.Quadros(("IMPOSTO DE IMPORTAÇÃO", NFeImpressao.Dinheiro(t.VII), 1), ("VALOR DO PIS", NFeImpressao.Dinheiro(t.VPIS), 1),
            ("VALOR DA COFINS", NFeImpressao.Dinheiro(t.VCOFINS), 1), ("VALOR TOTAL DOS TRIBUTOS", NFeImpressao.Dinheiro(t.VTotTrib), 1));
        if (t.VFCP != 0 || t.VFCPST != 0 || t.VFCPSTRet != 0 || t.VFCPUFDest.HasValue || t.VICMSUFDest.HasValue || t.VICMSUFRemet.HasValue)
            r.Quadros(("FCP", NFeImpressao.Dinheiro(t.VFCP), 1), ("FCP ST", NFeImpressao.Dinheiro(t.VFCPST), 1),
                ("FCP ST RETIDO", NFeImpressao.Dinheiro(t.VFCPSTRet), 1), ("FCP UF DESTINO", NFeImpressao.Dinheiro(t.VFCPUFDest), 1),
                ("ICMS UF DESTINO", NFeImpressao.Dinheiro(t.VICMSUFDest), 1), ("ICMS UF REMETENTE", NFeImpressao.Dinheiro(t.VICMSUFRemet), 1));
        var transporte = info.Transp;
        var transportador = transporte?.Transporta;
        r.Titulo("TRANSPORTADOR / VOLUMES TRANSPORTADOS");
        r.Quadros(("RAZÃO SOCIAL / DOCUMENTO", transportador?.XNome + " " + NFeImpressao.Documento(transportador?.CNPJ ?? transportador?.CPF), 3),
            ("FRETE POR CONTA", NFeImpressao.Frete(transporte?.ModFrete), 1),
            ("PLACA / UF", transporte?.VeicTransp?.Placa + " " + transporte?.VeicTransp?.UF, 1));
        if (transportador != null)
            r.Quadros(("ENDEREÇO", transportador.XEnder, 3), ("MUNICÍPIO / UF", transportador.XMun + "/" + transportador.UF, 2), ("INSCRIÇÃO ESTADUAL", transportador.IE, 1));
        if (transporte != null) foreach (var volume in transporte.Vol)
            r.Quadros(("QUANTIDADE / ESPÉCIE", volume.QVol + " " + volume.Esp, 1), ("MARCA / NUMERAÇÃO", volume.Marca + " " + volume.NVol, 1),
                ("PESO BRUTO / LÍQUIDO", NFeImpressao.Numero(volume.PesoB, 3) + " / " + NFeImpressao.Numero(volume.PesoL, 3), 1));
        r.Titulo("DADOS DOS PRODUTOS / SERVIÇOS");
        var titulos = new[] { "CÓDIGO", "DESCRIÇÃO DO PRODUTO / SERVIÇO", "NCM/SH", NFeImpressao.Texto(emitente.CRT) == "3" ? "O/CST" : "O/CSOSN", "CFOP", "UN", "QTD", "VL UNIT", "VL TOTAL", "BC ICMS", "VL ICMS", "VL IPI", "%ICMS", "%IPI" };
        var pesos = new double[] { 8, 28, 7, 5, 5, 3, 6, 8, 8, 7, 7, 6, 5, 5 };
        var issqn = NFeImpressao.Campos(info.Total.ISSQNtot).ToArray();
        var ibsCbs = NFeImpressao.Campos(info.Total.IBSCBSTot).ToArray();
        var linhasAdicionais = Math.Max(r.Quebrar(info.InfAdic?.InfCpl, r.LarguraUtil * .75 - 6).Count(),
            r.Quebrar(info.InfAdic?.InfAdFisco, r.LarguraUtil * .25 - 6).Count());
        var reserva = 45 + linhasAdicionais * 10;
        if (issqn.Length > 0) reserva += 20 + issqn.Sum(linha => r.Quebrar(linha, r.LarguraUtil).Count()) * 11;
        if (ibsCbs.Length > 0) reserva += 20 + ibsCbs.Sum(linha => r.Quebrar(linha, r.LarguraUtil).Count()) * 11;
        r.Tabela(titulos, pesos, info.Det.Select(Produto), reserva);
        r.Secao("CÁLCULO DO ISSQN", issqn);
        r.Secao("IBS / CBS", ibsCbs);
        r.Titulo("DADOS ADICIONAIS");
        r.Quadros(("INFORMAÇÕES COMPLEMENTARES", info.InfAdic?.InfCpl, 3), ("RESERVADO AO FISCO", info.InfAdic?.InfAdFisco, 1));
    }

    private static string[] Produto(NFeDet item)
    {
        var p = item.Prod;
        var icms = NFeImpressao.Alternativa(item.Imposto?.ICMS);
        var ipi = item.Imposto?.IPI?.IPITrib;
        return new[]
        {
            p.CProd, p.XProd + " " + item.InfAdProd, p.NCM,
            NFeImpressao.Valor(icms, "Orig") + NFeImpressao.Valor(icms, "CST") + NFeImpressao.Valor(icms, "CSOSN"),
            NFeImpressao.Texto(p.CFOP), p.UCom, NFeImpressao.Numero(p.QCom), NFeImpressao.PrecoUnitario(p.VUnCom), NFeImpressao.Dinheiro(p.VProd),
            NFeImpressao.Dinheiro(NFeImpressao.Decimal(icms, "VBC")), NFeImpressao.Dinheiro(NFeImpressao.Decimal(icms, "VICMS")),
            NFeImpressao.Dinheiro(ipi?.VIPI), NFeImpressao.Numero(NFeImpressao.Decimal(icms, "PICMS")), NFeImpressao.Numero(ipi?.PIPI)
        };
    }
}
