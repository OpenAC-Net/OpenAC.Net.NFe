// Contrato dos grupos de tributação dos leiautes NF-e 3.10 e 4.00.
using System;
using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Collection;
using OpenAC.Net.DFe.Core.Common;
using OpenAC.Net.DFe.Core.Document;
using OpenAC.Net.DFe.Core.Serializer;
using OpenAC.Net.NFe.Common.Types;
using OpenAC.Net.NFe.Common.Model;

namespace OpenAC.Net.NFe;

public sealed partial class NFeDetImposto : NFeItem<NFeDetImposto>
{
    private INFeImposto escolha;

    // As duas alternativas ocupam posições diferentes no XSD: IPI precede ISSQN.
    [DFeIgnore]
    public INFeImposto Imposto
    {
        get => escolha;
        set
        {
            escolha = value;
            if (value != null) value.Parent = Raiz;
        }
    }

    /// <summary>Valor estimado total de impostos federais, estaduais e municipais</summary>
    [DFeElement(TipoCampo.De2, "vTotTrib", Ordem = -10, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public decimal? VTotTrib { get; set; }

    [DFeElement("ICMS", Ordem = 0, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public NFeDetImpostoICMS ICMS
    {
        get => escolha as NFeDetImpostoICMS;
        set { if (value != null || escolha is NFeDetImpostoICMS) Imposto = value; }
    }

    private NFeDetImpostoIPI iPIValue;

    [DFeElement("IPI", Ordem = 10, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public NFeDetImpostoIPI IPI
    {
        get => iPIValue;
        set => iPIValue = Vincular(value);
    }

    private NFeDetImpostoII iIValue;

    /// <summary>Dados do Imposto de Importação</summary>
    [DFeElement("II", Ordem = 20, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public NFeDetImpostoII II
    {
        get => iIValue;
        set => iIValue = Vincular(value);
    }

    [DFeElement("ISSQN", Ordem = 30, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public NFeDetImpostoISSQN ISSQN
    {
        get => escolha as NFeDetImpostoISSQN;
        set { if (value != null || escolha is NFeDetImpostoISSQN) Imposto = value; }
    }

    private NFeDetImpostoPIS pISValue;

    /// <summary>Dados do PIS</summary>
    [DFeElement("PIS", Ordem = 40, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public NFeDetImpostoPIS PIS
    {
        get => pISValue;
        set => pISValue = Vincular(value);
    }

    private NFeDetImpostoPISST pISSTValue;

    /// <summary>Dados do PIS Substituição Tributária</summary>
    [DFeElement("PISST", Ordem = 50, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public NFeDetImpostoPISST PISST
    {
        get => pISSTValue;
        set => pISSTValue = Vincular(value);
    }

    private NFeDetImpostoCOFINS cOFINSValue;

    /// <summary>Dados do COFINS</summary>
    [DFeElement("COFINS", Ordem = 60, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public NFeDetImpostoCOFINS COFINS
    {
        get => cOFINSValue;
        set => cOFINSValue = Vincular(value);
    }

    private NFeDetImpostoCOFINSST cOFINSSTValue;

    /// <summary>Dados do COFINS da Substituição Tributaria;</summary>
    [DFeElement("COFINSST", Ordem = 70, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public NFeDetImpostoCOFINSST COFINSST
    {
        get => cOFINSSTValue;
        set => cOFINSSTValue = Vincular(value);
    }

    private NFeDetImpostoICMSUFDest iCMSUFDestValue;

    /// <summary>Grupo a ser informado nas vendas interestarduais para consumidor final, não contribuinte de ICMS</summary>
    [DFeElement("ICMSUFDest", Ordem = 80, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public NFeDetImpostoICMSUFDest ICMSUFDest
    {
        get => iCMSUFDestValue;
        set => iCMSUFDestValue = Vincular(value);
    }

    private NFeDetImpostoIS iSValue;

    /// <summary>Grupo de informações do Imposto Seletivo</summary>
    [DFeElement("IS", Ordem = 90, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public NFeDetImpostoIS IS
    {
        get => iSValue;
        set => iSValue = Vincular(value);
    }

    private NFeDetImpostoIBSCBS iBSCBSValue;

    /// <summary>Grupo de informações dos tributos IBS, CBS e Imposto Seletivo</summary>
    [DFeElement("IBSCBS", Ordem = 100, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public NFeDetImpostoIBSCBS IBSCBS
    {
        get => iBSCBSValue;
        set => iBSCBSValue = Vincular(value);
    }

    internal bool ShouldSerializeIS() => Leiaute == NFeVersao.v400;
    internal bool ShouldSerializeIBSCBS() => Leiaute == NFeVersao.v400;
    internal bool ShouldSerializeImposto() => escolha == null || Leiaute switch
    {
        NFeVersao.v310 => escolha is NFeDetImpostoICMS || escolha is NFeDetImpostoISSQN,
        NFeVersao.v400 => escolha is NFeDetImpostoICMS || escolha is NFeDetImpostoISSQN,
        _ => false
    };

    protected override void OnParentChanged()
    {
        base.OnParentChanged();
        Vincular(iPIValue);
        Vincular(iIValue);
        Vincular(pISValue);
        Vincular(pISSTValue);
        Vincular(cOFINSValue);
        Vincular(cOFINSSTValue);
        Vincular(iCMSUFDestValue);
        Vincular(iSValue);
        Vincular(iBSCBSValue);
        if (escolha != null) escolha.Parent = Raiz;
    }

}
