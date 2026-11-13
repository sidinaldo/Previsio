using System.Text.Json;

namespace Previsio.SondagemBling;

/// <summary>
/// O que o Bling devolveu para um recurso: status, quantos itens e os campos que aparecem.
/// </summary>
public class RespostaBling
{
    public int Status { get; set; }
    public string Corpo { get; set; } = "";
    public JsonElement? Dados { get; set; }

    public bool Sucesso
    {
        get { return Status >= 200 && Status < 300; }
    }

    /// <summary>Primeiro item da lista (ou o próprio objeto, num detalhe).</summary>
    public JsonElement? PrimeiroItem()
    {
        if (!Dados.HasValue)
        {
            return null;
        }
        JsonElement dados = Dados.Value;
        if (dados.ValueKind == JsonValueKind.Array)
        {
            if (dados.GetArrayLength() == 0)
            {
                return null;
            }
            return dados[0];
        }
        return dados;
    }

    public int Quantidade()
    {
        if (!Dados.HasValue)
        {
            return 0;
        }
        if (Dados.Value.ValueKind == JsonValueKind.Array)
        {
            return Dados.Value.GetArrayLength();
        }
        return 1;
    }

    /// <summary>Todos os caminhos de campos que aparecem nos itens (ex.: "categoria.id").</summary>
    public SortedSet<string> Campos()
    {
        var campos = new SortedSet<string>();
        if (!Dados.HasValue)
        {
            return campos;
        }
        JsonElement dados = Dados.Value;
        if (dados.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in dados.EnumerateArray())
            {
                JuntarCampos(item, "", campos);
            }
        }
        else
        {
            JuntarCampos(dados, "", campos);
        }
        return campos;
    }

    /// <summary>Procura um número em um caminho, por exemplo "borderos" (primeiro item) ou "id".</summary>
    public static long? LerId(JsonElement? item, string campo)
    {
        if (!item.HasValue || item.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        JsonElement valor;
        if (!item.Value.TryGetProperty(campo, out valor))
        {
            return null;
        }
        if (valor.ValueKind == JsonValueKind.Number)
        {
            return valor.GetInt64();
        }
        if (valor.ValueKind == JsonValueKind.Array && valor.GetArrayLength() > 0)
        {
            JsonElement primeiro = valor[0];
            if (primeiro.ValueKind == JsonValueKind.Number)
            {
                return primeiro.GetInt64();
            }
            return LerId(primeiro, "id");
        }
        if (valor.ValueKind == JsonValueKind.Object)
        {
            return LerId(valor, "id");
        }
        return null;
    }

    private static void JuntarCampos(JsonElement elemento, string prefixo, SortedSet<string> campos)
    {
        if (elemento.ValueKind != JsonValueKind.Object)
        {
            return;
        }
        foreach (JsonProperty propriedade in elemento.EnumerateObject())
        {
            string caminho = propriedade.Name;
            if (prefixo != "")
            {
                caminho = prefixo + "." + propriedade.Name;
            }
            if (propriedade.Value.ValueKind == JsonValueKind.Object)
            {
                JuntarCampos(propriedade.Value, caminho, campos);
            }
            else if (propriedade.Value.ValueKind == JsonValueKind.Array)
            {
                campos.Add(caminho + "[]");
                if (propriedade.Value.GetArrayLength() > 0)
                {
                    JuntarCampos(propriedade.Value[0], caminho + "[]", campos);
                }
            }
            else
            {
                campos.Add(caminho);
            }
        }
    }
}
