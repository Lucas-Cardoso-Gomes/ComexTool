using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using ComexTool.Models;

namespace ComexTool.Services;

public enum ComparisonSeverity
{
    Success,     // Valores batem perfeitamente
    Warning,     // Pequena divergência ou campo ausente em algum dos documentos
    Danger       // Divergência crítica entre documentos
}

public class ComparisonItem
{
    public string Campo { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty; // "Valores", "Pesos", "Participantes", "Identificação", "Logística"
    public Dictionary<string, string?> ValoresPorDocumento { get; set; } = new(); // Nome/TipoDoc -> Valor
    public ComparisonSeverity Status { get; set; }
    public string? DetalheStatus { get; set; }
}

public class ComparisonSummary
{
    public int TotalCamposAnalisados { get; set; }
    public int Conformes { get; set; }
    public int Divergentes { get; set; }
    public int Alertas { get; set; }
    public List<ComparisonItem> Itens { get; set; } = new();
}

public class ProcessComparisonService
{
    public ComparisonSummary CompareProcessDocuments(ProcessoComex processo)
    {
        var summary = new ComparisonSummary();
        var docs = processo.Documentos.Where(d => d.ExtractedData != null).ToList();

        if (docs.Count == 0)
            return summary;

        // Lista de comparações a realizar
        CompareCampo(summary, docs, "Exportador / Razão Social", "Atores da Operação",
            d => d.ExtractedData?.Exportador?.RazaoSocial);

        CompareCampo(summary, docs, "CNPJ / ID Exportador", "Atores da Operação",
            d => d.ExtractedData?.Exportador?.CNPJ ?? d.ExtractedData?.Exportador?.IdentificacaoFiscal);

        CompareCampo(summary, docs, "Importador / Comprador", "Atores da Operação",
            d => d.ExtractedData?.Importador?.RazaoSocial);

        CompareCampo(summary, docs, "RUT / Tax ID Importador", "Atores da Operação",
            d => d.ExtractedData?.Importador?.IdentificacaoFiscal ?? d.ExtractedData?.Importador?.CNPJ);

        CompareCampo(summary, docs, "Consignatário", "Atores da Operação",
            d => d.ExtractedData?.Consignatario?.RazaoSocial);

        CompareCampo(summary, docs, "Transportador", "Atores da Operação",
            d => d.ExtractedData?.Transportador?.RazaoSocial);

        CompareCampo(summary, docs, "Placa do Veículo / Caminhão", "Transporte e Veículo",
            d => d.ExtractedData?.Transportador?.PlacaCaminhao ?? d.ExtractedData?.Nfe?.PlacaVeiculo);

        CompareCampo(summary, docs, "Placa do Reboque", "Transporte e Veículo",
            d => d.ExtractedData?.Transportador?.PlacaReboque);

        CompareCampo(summary, docs, "Incoterm", "Condições Comerciais",
            d => d.ExtractedData?.Incoterm ?? d.ExtractedData?.FaturaComercial?.Incoterm);

        CompareCampo(summary, docs, "Moeda da Operação", "Valores e Moedas",
            d => d.ExtractedData?.Moeda ?? d.ExtractedData?.FaturaComercial?.MoedaOperacao ?? d.ExtractedData?.Due?.MoedaNegociacao);

        CompareCampo(summary, docs, "Valor Total da Carga / Fatura", "Valores e Moedas",
            d => d.ExtractedData?.ValorTotal ?? d.ExtractedData?.FaturaComercial?.ValorTotalFatura ?? d.ExtractedData?.Nfe?.ValorTotalNF ?? d.ExtractedData?.Crt?.ValorFOB ?? d.ExtractedData?.Due?.VMLE,
            isNumeric: true);

        CompareCampo(summary, docs, "Valor do Frete", "Valores e Moedas",
            d => d.ExtractedData?.FaturaComercial?.ValorFrete ?? d.ExtractedData?.Nfe?.ValorFrete ?? d.ExtractedData?.Crt?.ValorFreteInternacional,
            isNumeric: true);

        CompareCampo(summary, docs, "Valor do Seguro", "Valores e Moedas",
            d => d.ExtractedData?.FaturaComercial?.ValorSeguro ?? d.ExtractedData?.Nfe?.ValorSeguro ?? d.ExtractedData?.Crt?.ValorSeguro,
            isNumeric: true);

        CompareCampo(summary, docs, "Peso Líquido Total (kg)", "Pesos e Volumes",
            d => d.ExtractedData?.PesoLiquido ?? d.ExtractedData?.Nfe?.PesoLiquido ?? d.ExtractedData?.FaturaComercial?.TotalPesoNeto ?? d.ExtractedData?.Crt?.PesoLiquido ?? d.ExtractedData?.Due?.PesoLiquidoTotal ?? d.ExtractedData?.PackingList?.PesoLiquidoTotal,
            isNumeric: true);

        CompareCampo(summary, docs, "Peso Bruto Total (kg)", "Pesos e Volumes",
            d => d.ExtractedData?.PesoBruto ?? d.ExtractedData?.Nfe?.PesoBruto ?? d.ExtractedData?.FaturaComercial?.TotalPesoBruto ?? d.ExtractedData?.Crt?.PesoBruto ?? d.ExtractedData?.PackingList?.PesoBrutoTotal,
            isNumeric: true);

        CompareCampo(summary, docs, "Quantidade de Volumes / Bultos", "Pesos e Volumes",
            d => d.ExtractedData?.Nfe?.QtdVolumes ?? d.ExtractedData?.Crt?.QtdVolumes ?? d.ExtractedData?.PackingList?.TotalCaixasVolumes,
            isNumeric: true);

        CompareCampo(summary, docs, "Volume em m³ (CBM)", "Pesos e Volumes",
            d => d.ExtractedData?.VolumeTotalM3 ?? d.ExtractedData?.FaturaComercial?.VolumeM3 ?? d.ExtractedData?.Crt?.VolumeM3,
            isNumeric: true);

        CompareCampo(summary, docs, "Número da Fatura Comercial", "Vínculos Cruzados",
            d => d.ExtractedData?.FaturaComercial?.NumeroFatura ?? d.ExtractedData?.CertificadoOrigem?.NumeroFatura);

        CompareCampo(summary, docs, "Número do Lote / Batch", "Rastreabilidade",
            d => d.ExtractedData?.PackingList?.NumeroLote);

        CompareCampo(summary, docs, "Número SIF / Sanitário", "Rastreabilidade",
            d => d.ExtractedData?.PackingList?.NumeroSIF);

        CompareCampo(summary, docs, "NCM Principal", "Classificação Fiscal",
            d => d.ExtractedData?.NCM ?? d.ExtractedData?.Itens?.FirstOrDefault()?.NCM);

        CompareCampo(summary, docs, "País de Destino", "Logística e Rota",
            d => d.ExtractedData?.CertificadoOrigem?.PaisDestino ?? d.ExtractedData?.Crt?.PaisDestino ?? d.ExtractedData?.Due?.PaisImportador ?? d.ExtractedData?.Importador?.Pais);

        summary.TotalCamposAnalisados = summary.Itens.Count;
        summary.Conformes = summary.Itens.Count(i => i.Status == ComparisonSeverity.Success);
        summary.Divergentes = summary.Itens.Count(i => i.Status == ComparisonSeverity.Danger);
        summary.Alertas = summary.Itens.Count(i => i.Status == ComparisonSeverity.Warning);

        return summary;
    }

    private void CompareCampo(ComparisonSummary summary, List<Document> docs, string nomeCampo, string categoria, Func<Document, string?> selector, bool isNumeric = false)
    {
        var item = new ComparisonItem
        {
            Campo = nomeCampo,
            Categoria = categoria
        };

        var presentValues = new Dictionary<string, string>();

        foreach (var doc in docs)
        {
            var val = selector(doc);
            var docKey = $"{doc.Type} ({doc.FileName})";
            item.ValoresPorDocumento[docKey] = val;

            if (!string.IsNullOrWhiteSpace(val))
            {
                presentValues[docKey] = val.Trim();
            }
        }

        if (presentValues.Count == 0)
        {
            // Nenhum documento tem esse valor, não adiciona ruído
            return;
        }

        if (presentValues.Count == 1)
        {
            item.Status = ComparisonSeverity.Warning;
            item.DetalheStatus = "Presente em apenas 1 documento (não comparável)";
        }
        else
        {
            // Comparar valores
            bool todosIguais;
            if (isNumeric)
            {
                todosIguais = AreNumbersEqual(presentValues.Values);
            }
            else
            {
                todosIguais = AreStringsSimilar(presentValues.Values);
            }

            if (todosIguais)
            {
                item.Status = ComparisonSeverity.Success;
                item.DetalheStatus = "Conforme em todos os documentos comparados";
            }
            else
            {
                item.Status = ComparisonSeverity.Danger;
                item.DetalheStatus = "Divergência detectada entre os documentos!";
            }
        }

        summary.Itens.Add(item);
    }

    private bool AreNumbersEqual(IEnumerable<string> values)
    {
        decimal? first = null;
        foreach (var val in values)
        {
            // Limpa formatação: remove moedas, pontos de milhar, etc.
            var clean = Regex.Replace(val, @"[^\d,\.]", "");
            if (clean.Contains(',') && clean.Contains('.'))
            {
                // Se tem ambos, assume que ponto ou vírgula é milhar
                if (clean.LastIndexOf(',') > clean.LastIndexOf('.'))
                    clean = clean.Replace(".", "").Replace(',', '.');
                else
                    clean = clean.Replace(",", "");
            }
            else if (clean.Contains(','))
            {
                clean = clean.Replace(',', '.');
            }

            if (decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
            {
                if (first == null) first = num;
                else if (Math.Abs(first.Value - num) > 0.01m) return false;
            }
            else
            {
                // Fallback para comparação de texto
                if (first == null) return false;
            }
        }
        return true;
    }

    private bool AreStringsSimilar(IEnumerable<string> values)
    {
        string? firstNormalized = null;
        foreach (var val in values)
        {
            var norm = NormalizeString(val);
            if (firstNormalized == null)
            {
                firstNormalized = norm;
            }
            else
            {
                // Verifica igualdade ou se uma contém a outra (ex: "JBS S/A" e "JBS S.A. - FILIAL")
                if (firstNormalized != norm && !firstNormalized.Contains(norm) && !norm.Contains(firstNormalized))
                {
                    return false;
                }
            }
        }
        return true;
    }

    private string NormalizeString(string text)
    {
        var clean = Regex.Replace(text.ToUpperInvariant(), @"[^\w]", "");
        return clean;
    }
}
