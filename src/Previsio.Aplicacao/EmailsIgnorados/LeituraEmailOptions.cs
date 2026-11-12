namespace Previsio.Aplicacao.EmailsIgnorados;

/// <summary>
/// Seção "LeituraEmail" do appsettings (opcional).
/// </summary>
public class LeituraEmailOptions
{
    public const string Secao = "LeituraEmail";
    public const int DiasGuardarIgnoradosPadrao = 30;

    // Por quantos dias os e-mails ignorados ficam na tela Saúde (1 a 365)
    public int DiasGuardarIgnorados { get; set; } = DiasGuardarIgnoradosPadrao;

    public int DiasGuardarIgnoradosValidos()
    {
        if (DiasGuardarIgnorados < 1 || DiasGuardarIgnorados > 365)
        {
            return DiasGuardarIgnoradosPadrao;
        }
        return DiasGuardarIgnorados;
    }
}
