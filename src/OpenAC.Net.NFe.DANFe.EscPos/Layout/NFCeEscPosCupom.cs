using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using OpenAC.Net.EscPos;
using OpenAC.Net.EscPos.Commom;
using OpenAC.Net.NFe.Common.Model;
using OpenAC.Net.NFe.Common.Types;
using OpenAC.Net.NFe.DANFe.EscPos.Configuracao;

namespace OpenAC.Net.NFe.DANFe.EscPos.Layout;

/// <summary>Composição fiscal comum ao texto, à prévia e ao envio pelo componente.</summary>
internal sealed class NFCeEscPosCupom
{
    private readonly List<Linha> linhas = new();
    private readonly int colunas;
    private readonly CancellationToken cancelamento;
    private NFCeEscPosCupom(int colunas, CancellationToken cancelamento) { this.colunas = colunas; this.cancelamento = cancelamento; }

    private enum TipoLinha { Texto, QrCode, Imagem, Logo }
    private sealed class Linha
    {
        internal TipoLinha Tipo;
        internal string Texto;
        internal CmdAlinhamento Alinhamento;
        internal bool Negrito;
        internal byte[] Imagem;
    }

    internal static NFCeEscPosCupom Criar(NFeProc nota, DANFeEscPosConfig configuracao, int colunas, CancellationToken cancelamento)
    {
        cancelamento.ThrowIfCancellationRequested();
        if (nota?.NFe?.InfNFe?.Ide == null || nota.NFe.InfNFe.Emit == null || nota.NFe.InfNFe.Total?.ICMSTot == null)
            throw new ArgumentException("Informe identificação, emitente e totais da NFC-e.", nameof(nota));
        if (nota.Modelo != ModeloNFe.NFCe) throw new ArgumentException("O DANFE ESC/POS exige modelo 65.", nameof(nota));
        configuracao.Validar(colunas);
        if (Encoding.UTF8.GetByteCount(nota.QrCode) > 7089) throw new ArgumentException("Conteúdo excede o limite do QR Code.", nameof(nota));
        if (nota.ProtNFe != null) nota.ValidarProtocolo();
        var cupom = new NFCeEscPosCupom(colunas, cancelamento);
        cupom.Montar(nota, configuracao);
        return cupom;
    }

    private void Montar(NFeProc nota, DANFeEscPosConfig configuracao)
    {
        var info = nota.NFe.InfNFe;
        var emitente = info.Emit;
        if (configuracao.Logo is { Length: > 0 }) linhas.Add(new Linha { Tipo = TipoLinha.Imagem, Imagem = configuracao.Logo });
        else if (configuracao.UsarLogoDaImpressora) linhas.Add(new Linha { Tipo = TipoLinha.Logo });
        if (configuracao.ExibirNomeFantasia && !string.IsNullOrWhiteSpace(emitente.XFant)) Texto(emitente.XFant, CmdAlinhamento.Centro, true);
        Texto(emitente.XNome, CmdAlinhamento.Centro, true);
        Texto("CNPJ/CPF: " + NFeImpressao.Documento(emitente.CNPJ ?? emitente.CPF), CmdAlinhamento.Centro);
        Texto("IE: " + emitente.IE, CmdAlinhamento.Centro);
        Texto(NFeImpressao.Endereco(emitente.EnderEmit), CmdAlinhamento.Centro);
        Separador();
        Texto("DANFE NFC-e", CmdAlinhamento.Centro, true);
        Texto("Documento Auxiliar da Nota Fiscal de Consumidor Eletrônica", CmdAlinhamento.Centro);
        Texto("Não permite aproveitamento de crédito de ICMS", CmdAlinhamento.Centro);
        foreach (var aviso in NFeImpressao.Avisos(nota)) Texto(aviso, CmdAlinhamento.Centro, true);
        Separador();
        if (configuracao.ExibirItens)
        {
            Texto("CÓDIGO / DESCRIÇÃO", negrito: true);
            Par("QTD x UN x VL UNIT", "VL TOTAL R$", true);
            foreach (var item in info.Det)
            {
                cancelamento.ThrowIfCancellationRequested();
                var p = item.Prod;
                if (p == null) throw new ArgumentException("Produto não informado.", nameof(nota));
                Texto(p.CProd + " " + p.XProd);
                Par(NFeImpressao.Numero(p.QCom) + " " + p.UCom + " x " + NFeImpressao.PrecoUnitario(p.VUnCom), NFeImpressao.Dinheiro(p.VProd));
                if (p.VDesc > 0) Par("Desconto do item R$", NFeImpressao.Dinheiro(p.VDesc));
                if (p.VOutro > 0) Par("Acréscimo do item R$", NFeImpressao.Dinheiro(p.VOutro));
                if (!string.IsNullOrWhiteSpace(item.InfAdProd)) Texto(item.InfAdProd);
            }
            Separador();
        }
        var total = info.Total.ICMSTot;
        Par("Qtd. total de itens", info.Det.Count.ToString());
        Par("Valor total R$", NFeImpressao.Dinheiro(total.VProd + (info.Total.ISSQNtot?.VServ ?? 0)));
        if (total.VDesc > 0) Par("Descontos R$", NFeImpressao.Dinheiro(total.VDesc));
        if (total.VSeg + total.VOutro > 0) Par("Acréscimos R$", NFeImpressao.Dinheiro(total.VSeg + total.VOutro));
        if (total.VFrete > 0) Par("Frete R$", NFeImpressao.Dinheiro(total.VFrete));
        Par("Valor a pagar R$", NFeImpressao.Dinheiro(total.VNF), true);
        Separador();
        Par("FORMA DE PAGAMENTO", "VALOR PAGO R$", true);
        foreach (var pagamento in NFeImpressao.Pagamentos(info)) Par(pagamento.Descricao, NFeImpressao.Dinheiro(pagamento.Valor));
        foreach (var pagamento in info.Pag) if (pagamento.VTroco.HasValue) Par("Troco R$", NFeImpressao.Dinheiro(pagamento.VTroco));
        Separador();
        Texto("Consulte pela chave de acesso em:", CmdAlinhamento.Centro, true);
        Texto(nota.NFe.InfNFeSupl?.UrlChave, CmdAlinhamento.Centro);
        Texto(NFeImpressao.Chave(nota.Chave), CmdAlinhamento.Centro);
        var destino = info.Dest;
        var documento = destino?.CPF ?? destino?.CNPJ ?? destino?.IdEstrangeiro;
        Texto(string.IsNullOrEmpty(documento) ? "CONSUMIDOR NÃO IDENTIFICADO" : "CONSUMIDOR: " + NFeImpressao.Documento(documento), CmdAlinhamento.Centro, true);
        if (!string.IsNullOrEmpty(documento)) Texto(destino.XNome, CmdAlinhamento.Centro);
        Texto("NFC-e nº " + info.Ide.NNF + " Série " + info.Ide.Serie, CmdAlinhamento.Centro, true);
        Texto("Emissão: " + NFeImpressao.Data(info.Ide.DhEmi), CmdAlinhamento.Centro);
        var protocolo = nota.ProtNFe?.InfProt;
        if (protocolo != null)
        {
            Texto("Protocolo de autorização: " + protocolo.NProt, CmdAlinhamento.Centro);
            Texto("Autorização: " + NFeImpressao.Data(protocolo.DhRecbto), CmdAlinhamento.Centro);
        }
        if (!string.IsNullOrWhiteSpace(nota.QrCode)) linhas.Add(new Linha { Tipo = TipoLinha.QrCode, Texto = nota.QrCode });
        Separador();
        if (!string.IsNullOrWhiteSpace(info.InfAdic?.InfCpl)) Texto(info.InfAdic.InfCpl);
        if (info.InfAdic != null) foreach (var observacao in info.InfAdic.ObsCont) Texto(observacao.XCampo + ": " + observacao.XTexto);
        Texto("Tributos totais incidentes (Lei Federal 12.741/2012) R$: " + NFeImpressao.Dinheiro(total.VTotTrib));
        foreach (var campo in NFeImpressao.Campos(info.Total.IBSCBSTot)) Texto(campo);
        if (!string.IsNullOrWhiteSpace(configuracao.MensagemRodape)) { Separador(); Texto(configuracao.MensagemRodape, CmdAlinhamento.Centro); }
    }

    private void Separador() => Texto(new string('-', colunas));
    private void Par(string rotulo, string valor, bool negrito = false)
    {
        var esquerda = Quebrar(Limpar(rotulo), colunas - 1).ToArray();
        var direita = Limpar(valor);
        if (direita.Length >= colunas)
        {
            Texto(rotulo, negrito: negrito); Texto(valor, CmdAlinhamento.Direita, negrito); return;
        }
        for (var i = 0; i < esquerda.Length - 1; i++) Texto(esquerda[i], negrito: negrito);
        var ultima = esquerda[esquerda.Length - 1];
        if (ultima.Length + direita.Length + 1 <= colunas)
            Texto(ultima + new string(' ', colunas - ultima.Length - direita.Length) + direita, negrito: negrito);
        else { Texto(ultima, negrito: negrito); Texto(direita, CmdAlinhamento.Direita, negrito); }
    }
    private void Texto(string texto, CmdAlinhamento alinhamento = CmdAlinhamento.Esquerda, bool negrito = false)
    {
        foreach (var paragrafo in (texto ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            foreach (var linha in Quebrar(Limpar(paragrafo), colunas))
            {
                cancelamento.ThrowIfCancellationRequested();
                linhas.Add(new Linha { Tipo = TipoLinha.Texto, Texto = linha, Alinhamento = alinhamento, Negrito = negrito });
            }
    }
    private static string Limpar(string texto) => new((texto ?? "").Select(c => char.IsControl(c) ? ' ' : c).ToArray());
    private static IEnumerable<string> Quebrar(string texto, int largura)
    {
        while (texto.Length > largura)
        {
            var posicao = texto.LastIndexOf(' ', largura - 1, largura);
            if (posicao < 1) posicao = largura;
            yield return texto.Substring(0, posicao);
            texto = texto.Substring(posicao).TrimStart();
        }
        yield return texto;
    }

    internal string GerarTexto()
    {
        var texto = new StringBuilder();
        foreach (var linha in linhas.Where(linha => linha.Tipo == TipoLinha.Texto))
        {
            var espacos = colunas - linha.Texto.Length;
            if (linha.Alinhamento == CmdAlinhamento.Centro) texto.Append(' ', espacos / 2);
            else if (linha.Alinhamento == CmdAlinhamento.Direita) texto.Append(' ', espacos);
            texto.Append(linha.Texto).Append('\n');
        }
        return texto.ToString();
    }
    internal void Aplicar(EscPosPrinter impressora, DANFeEscPosConfig configuracao)
    {
        foreach (var linha in linhas)
        {
            switch (linha.Tipo)
            {
                case TipoLinha.Texto:
                    impressora.ImprimirTexto(linha.Texto, CmdFonte.Normal, CmdTamanhoFonte.Normal, linha.Alinhamento,
                        linha.Negrito ? CmdEstiloFonte.Negrito : null);
                    break;
                case TipoLinha.QrCode:
                    impressora.ImprimirQrCode(linha.Texto, QrCodeTipo.Model2, (QrCodeModSize)configuracao.ModuloQrCode, QrCodeErrorLevel.LevelM, CmdAlinhamento.Centro);
                    impressora.PularLinhas(1);
                    break;
                case TipoLinha.Imagem: impressora.ImprimirImagem(linha.Imagem, CmdAlinhamento.Centro); break;
                case TipoLinha.Logo: impressora.ImprimirLogo(); break;
            }
        }
        if (configuracao.CortarPapel)
        {
            var anterior = impressora.LinhasEntreCupons;
            try { impressora.LinhasEntreCupons = (byte)configuracao.LinhasAvanco; impressora.CortarPapel(configuracao.CorteParcial); }
            finally { impressora.LinhasEntreCupons = anterior; }
        }
        else if (configuracao.LinhasAvanco > 0) impressora.PularLinhas((byte)configuracao.LinhasAvanco);
    }
}
