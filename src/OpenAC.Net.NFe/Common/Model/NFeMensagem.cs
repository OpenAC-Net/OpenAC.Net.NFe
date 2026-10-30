namespace OpenAC.Net.NFe.Common.Model;

/// <summary>Item de uma mensagem complementar do protocolo: código ou texto.</summary>
public abstract class NFeMensagem
{
    public abstract string Valor { get; set; }
}
