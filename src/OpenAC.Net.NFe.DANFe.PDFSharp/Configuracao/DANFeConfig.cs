using System;

namespace OpenAC.Net.NFe.DANFe.PDFSharp.Configuracao;

/// <summary>Configuração da geração PDF, independente do cliente de emissão.</summary>
public sealed class DANFeConfig
{
    public bool Paisagem { get; set; }
    public int LarguraNFCeMilimetros { get; set; } = 80;
    public double MargemMilimetros { get; set; } = 5;
    public double MargemNFCeMilimetros { get; set; } = 3;
    public bool ExibirCanhoto { get; set; } = true;
    public string MensagemRodape { get; set; } = "OpenAC.Net.NFe";
    public byte[] Logo { get; set; }
    public string SenhaUsuario { get; set; }
    public string SenhaProprietario { get; set; }
    public bool PermitirImpressao { get; set; } = true;
    public bool PermitirCopia { get; set; } = true;

    public DANFeConfig Copiar()
    {
        var copia = (DANFeConfig)MemberwiseClone();
        copia.Logo = Logo == null ? null : (byte[])Logo.Clone();
        return copia;
    }

    internal void Validar()
    {
        if (LarguraNFCeMilimetros != 58 && LarguraNFCeMilimetros != 80)
            throw new ArgumentOutOfRangeException(nameof(LarguraNFCeMilimetros), "Largura NFC-e deve ser 58 ou 80 mm.");
        if (double.IsNaN(MargemMilimetros) || MargemMilimetros < 0 || MargemMilimetros > 20)
            throw new ArgumentOutOfRangeException(nameof(MargemMilimetros));
        if (double.IsNaN(MargemNFCeMilimetros) || MargemNFCeMilimetros < 0 || MargemNFCeMilimetros > 8)
            throw new ArgumentOutOfRangeException(nameof(MargemNFCeMilimetros));
    }
}
