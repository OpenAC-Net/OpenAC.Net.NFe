using System;
using System.Globalization;
using System.IO;
using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Common;
using OpenAC.Net.DFe.Core.Document;
using OpenAC.Net.DFe.Core.Serializer;

namespace OpenAC.Net.NFe.Common.Model;

/// <summary>Evento NF-e assinado pela infraestrutura do Core, com detalhe fiscal extensível.</summary>
[DFeSignInfoElement("infEvento")]
[DFeRoot("evento", Namespace = NFeConst.Namespace)]
public sealed partial class NFeEvento : DFeSignDocument<NFeEvento>
{
    [DFeAttribute(TipoCampo.Str, "versao", Ocorrencia = Ocorrencia.Obrigatoria)]
    public string Versao { get; set; } = "1.00";

    [DFeElement("infEvento", Ordem = 10, Ocorrencia = Ocorrencia.Obrigatoria)]
    public NFeEventoInfo InfEvento { get; set; } = new()
    {
        Sequencia = "1", Versao = "1.00",
        Data = DateTimeOffset.Now.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture)
    };

    [DFeIgnore]
    public string Chave { get => InfEvento.Chave; set => InfEvento.Chave = value; }

    [DFeIgnore]
    public string DocumentoAutor
    {
        get => InfEvento.CNPJ ?? InfEvento.CPF;
        set { InfEvento.CNPJ = value?.Length == 11 ? null : value; InfEvento.CPF = value?.Length == 11 ? value : null; }
    }

    [DFeIgnore]
    public int Codigo
    {
        get => int.TryParse(InfEvento.Codigo, out var valor) ? valor : 0;
        set => InfEvento.Codigo = value.ToString("D6", CultureInfo.InvariantCulture);
    }

    [DFeIgnore]
    public int Sequencia
    {
        get => int.TryParse(InfEvento.Sequencia, out var valor) ? valor : 0;
        set => InfEvento.Sequencia = value.ToString(CultureInfo.InvariantCulture);
    }

    [DFeIgnore]
    public int Orgao
    {
        get => int.TryParse(InfEvento.Orgao, out var valor) ? valor : 0;
        set => InfEvento.Orgao = value.ToString(CultureInfo.InvariantCulture);
    }

    [DFeIgnore]
    public DateTimeOffset Data
    {
        get => DateTimeOffset.Parse(InfEvento.Data, CultureInfo.InvariantCulture);
        set => InfEvento.Data = value.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);
    }

    [DFeIgnore]
    public NFeEventoDetalhe Detalhe { get => InfEvento.Detalhe; set => InfEvento.Detalhe = value; }

    public static NFeEvento Cancelamento(string chave, string autor, string protocolo, string justificativa) =>
        Criar(chave, autor, 110111, new NFeEventoDetalhe
        { Descricao = "Cancelamento", Protocolo = protocolo, Justificativa = justificativa });

    public static NFeEvento CartaCorrecao(string chave, string autor, int sequencia, string correcao)
    {
        var evento = Criar(chave, autor, 110110, new NFeEventoDetalhe
        { Descricao = "Carta de Correcao", Correcao = correcao, CondicaoUso = CondicaoCCe });
        evento.Sequencia = sequencia;
        return evento;
    }

    public static NFeEvento Manifestacao(string chave, string autor, int codigo, string justificativa = null)
    {
        var descricao = codigo switch
        {
            210200 => "Confirmacao da Operacao", 210210 => "Ciencia da Operacao",
            210220 => "Desconhecimento da Operacao", 210240 => "Operacao nao Realizada",
            _ => throw new ArgumentOutOfRangeException(nameof(codigo))
        };
        var evento = Criar(chave, autor, codigo, new NFeEventoDetalhe
        { Descricao = descricao, Justificativa = codigo == 210240 ? justificativa : null });
        evento.Orgao = 91;
        return evento;
    }

    public static NFeEvento CancelamentoSubstituicao(string chave, string autor, string protocolo,
        string chaveSubstituta, string justificativa, string versaoAplicativo = "OpenAC 1.0") =>
        Criar(chave, autor, 110112, new NFeEventoDetalhe
        {
            Descricao = "Cancelamento por substituicao", OrgaoAutor = chave?.Substring(0, 2), TipoAutor = "1",
            VersaoAplicativo = versaoAplicativo, Protocolo = protocolo, Justificativa = justificativa,
            ChaveReferenciada = chaveSubstituta
        });

    private static NFeEvento Criar(string chave, string autor, int codigo, NFeEventoDetalhe detalhe) =>
        new() { Chave = chave, DocumentoAutor = autor, Codigo = codigo, Detalhe = detalhe };

    public NFeEvento Assinar(NFeConfig configuracao)
    {
        if (configuracao == null) throw new ArgumentNullException(nameof(configuracao));
        NFeValidacao.Chave(Chave);
        NFeValidacao.Documento(DocumentoAutor);
        if (Sequencia < 1 || Sequencia > 99) throw new ArgumentOutOfRangeException(nameof(Sequencia));
        if (Codigo == 110110 && Chave.Substring(20, 2) == "65")
            throw new NotSupportedException("NFC-e não permite carta de correção.");
        if (Detalhe == null) throw new ArgumentException("Detalhe do evento não informado.");
        var certificado = configuracao.Certificado ?? throw new ArgumentException("Certificado não informado.");
        var codigo = Codigo.ToString("D6", CultureInfo.InvariantCulture);
        InfEvento.Id = "ID" + codigo + Chave + Sequencia.ToString("D2", CultureInfo.InvariantCulture);
        if (Orgao == 0) Orgao = int.Parse(Chave.Substring(0, 2), CultureInfo.InvariantCulture);
        InfEvento.TpAmb = ((int)configuracao.WebServices.Ambiente).ToString(CultureInfo.InvariantCulture);
        var arquivo = Path.Combine(configuracao.Arquivos.PathSchemas, "e" + codigo + "_v" + InfEvento.Versao + ".xsd");
        if (!File.Exists(arquivo))
            throw new NotSupportedException("Schema do detalhe do evento não encontrado: " + Path.GetFileName(arquivo));
        NFeValidacao.Schema(Detalhe.GetXml(), arquivo);
        AssinarDocumento(certificado, DFeSaveOptions.DisableFormatting | DFeSaveOptions.OmitDeclaration, false);
        return this;
    }

    public bool ValidarAssinatura() => !string.IsNullOrEmpty(Signature?.SignatureValue) &&
        DFe.Core.XmlSigning.ValidarAssinatura(this, true);

    protected override bool ShouldSerializeSignature() => Signature != null && base.ShouldSerializeSignature();

    public const string CondicaoCCe = "A Carta de Correcao e disciplinada pelo paragrafo 1o-A do art. 7o do Convenio S/N, de 15 de dezembro de 1970 e pode ser utilizada para regularizacao de erro ocorrido na emissao de documento fiscal, desde que o erro nao esteja relacionado com: I - as variaveis que determinam o valor do imposto tais como: base de calculo, aliquota, diferenca de preco, quantidade, valor da operacao ou da prestacao; II - a correcao de dados cadastrais que implique mudanca do remetente ou do destinatario; III - a data de emissao ou de saida.";
}
