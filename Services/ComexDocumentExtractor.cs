using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using ComexTool.Models;

namespace ComexTool.Services;

public class ComexDocumentExtractor
{
    private static readonly string[] Incoterms = { "EXW", "FCA", "CPT", "CIP", "DAP", "DPU", "DDP", "FAS", "FOB", "CFR", "CIF" };

    public (string extractedText, ExtractedData data) Extract(byte[] fileBytes, string contentType, string fileName)
    {
        string text = ExtractText(fileBytes, contentType, fileName);
        var data = new ExtractedData();

        if (string.IsNullOrWhiteSpace(text))
            return (text, data);

        // --- Campos genéricos ---
        data.NCM = ExtractNcm(text);
        data.Incoterm = ExtractIncoterm(text);
        data.Moeda = ExtractPattern(text, @"(?:MOEDA|CURRENCY|MON)[\s:\-]*([A-Z]{3})\b", @"\b(USD|EUR|BRL|CLP|ARS|GBP|JPY|CNY)\b");
        data.ValorTotal = ExtractPattern(text,
            @"(?:VALOR\s*TOTAL|TOTAL\s*VALUE|GRAND\s*TOTAL|TOTAL\s*GERAL|TOTAL\s*NF|VALOR\s*TOTAL\s*DA\s*NOTA)[\s:\-]*[\$R]*\s*([\d\.,]+)",
            null);
        data.PesoBruto = ExtractPattern(text, @"(?:PESO\s*BRUTO|GROSS\s*WEIGHT|P\.?\s*BRUTO)[\s:\-]*([\d\.,]+)\s*(?:KG|kg|t|T)?", null);
        data.PesoLiquido = ExtractPattern(text, @"(?:PESO\s*L[IÍ]QUIDO|NET\s*WEIGHT|P\.?\s*L[IÍ]Q)[\s:\-]*([\d\.,]+)\s*(?:KG|kg|t|T)?", null);
        data.VolumeTotalM3 = ExtractPattern(text, @"(?:VOLUME|CBM|M[³3])[\s:\-]*([\d\.,]+)", null);

        // --- Participantes ---
        data.Exportador = ExtractParticipante(text, @"(?:EXPORTADOR|EXPORTER|REMETENTE|SHIPPER|VENDEDOR|SELLER)", @"(?:IMPORTADOR|IMPORTER|COMPRADOR|BUYER|DESTINAT)");
        data.Importador = ExtractParticipante(text, @"(?:IMPORTADOR|IMPORTER|COMPRADOR|BUYER|DESTINAT[AÁ]RIO)", @"(?:CONSIGNAT|NOTIFY|TRANSPORT|DADOS|MERCADORIA|ITEM|PRODUTO)");
        data.Consignatario = ExtractParticipanteSingle(text, @"(?:CONSIGNAT[AÁ]RIO|CONSIGNEE)");
        data.NotifyParty = ExtractParticipanteSingle(text, @"(?:NOTIFY\s*PARTY|PARTE\s*A\s*NOTIFICAR)");

        // --- NF-e ---
        data.Nfe = ExtractNfe(text);

        // --- Fatura Comercial ---
        data.FaturaComercial = ExtractFaturaComercial(text);

        // --- DU-E ---
        data.Due = ExtractDue(text);

        // --- CRT/MIC-DTA ---
        data.Crt = ExtractCrt(text);

        // --- Certificado de Origem ---
        data.CertificadoOrigem = ExtractCertificadoOrigem(text);

        // --- Packing List ---
        data.PackingList = ExtractPackingList(text);

        // --- Itens ---
        data.Itens = ExtractItens(text);

        // --- Identificação geral ---
        data.NumeroDocumento = ExtractPattern(text,
            @"(?:N[UÚ]MERO|NUMBER|N[ºO]|NR|INVOICE\s*N)[\s:\.\-]*(\w[\w\-/\.]{2,30})", null);
        data.DataEmissao = ExtractPattern(text,
            @"(?:DATA\s*(?:DE\s*)?EMISS[AÃ]O|DATE\s*OF\s*ISSUE|FECHA)[\s:\-]*(\d{1,2}[\/\.\-]\d{1,2}[\/\.\-]\d{2,4})", null);

        // --- Transportador básico ---
        var transportadorInfo = ExtractTransportador(text);
        if (transportadorInfo != null) data.Transportador = transportadorInfo;

        // --- Condutor ---
        data.CondutorNome = ExtractPattern(text, @"(?:CONDUTOR|MOTORISTA|DRIVER)[\s:\-]*([A-ZÀ-Ú][a-zà-ú]+(?:\s+[A-ZÀ-Ú][a-zà-ú]+)+)", null);
        data.CondutorCpf = ExtractPattern(text, @"(?:CPF\s*(?:DO\s*)?(?:CONDUTOR|MOTORISTA))[\s:\-]*(\d{3}\.?\d{3}\.?\d{3}\-?\d{2})", null);

        // --- Despachante ---
        data.DespachanteNome = ExtractPattern(text, @"(?:DESPACHANTE|DECLARANTE)[\s:\-]*([A-ZÀ-Ú][a-zà-ú]+(?:\s+[A-ZÀ-Ú][a-zà-ú]+)+)", null);
        data.DespachanteCpf = ExtractPattern(text, @"(?:CPF\s*(?:DO\s*)?(?:DESPACHANTE|DECLARANTE))[\s:\-]*(\d{3}\.?\d{3}\.?\d{3}\-?\d{2})", null);

        return (text, data);
    }

    private string ExtractText(byte[] fileBytes, string contentType, string fileName)
    {
        if (contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var stream = new MemoryStream(fileBytes);
                using var document = PdfDocument.Open(stream);
                var pages = document.GetPages().Select(p => p.Text);
                return string.Join(Environment.NewLine, pages);
            }
            catch { return string.Empty; }
        }
        return string.Empty;
    }

    // ==================== EXTRACTION HELPERS ====================

    private string? ExtractPattern(string text, string primaryPattern, string? fallbackPattern)
    {
        var m = Regex.Match(text, primaryPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        if (m.Success) return m.Groups.Count > 1 ? m.Groups[1].Value.Trim() : m.Value.Trim();
        if (fallbackPattern != null)
        {
            m = Regex.Match(text, fallbackPattern, RegexOptions.IgnoreCase);
            if (m.Success) return m.Groups.Count > 1 ? m.Groups[1].Value.Trim() : m.Value.Trim();
        }
        return null;
    }

    private string? ExtractNcm(string text)
    {
        var m = Regex.Match(text, @"\b\d{4}\.\d{2}\.\d{2}\b");
        if (m.Success) return m.Value.Replace(".", "");

        m = Regex.Match(text, @"(?:NCM|HS\s*CODE|CLASSIFICA[CÇ][AÃ]O\s*FISCAL|NALADI)[\s:\.\-]*(\d{4}[\.\s]?\d{2}[\.\s]?\d{2}|\d{8}|\d{6})", RegexOptions.IgnoreCase);
        if (m.Success) return m.Groups[1].Value.Replace(".", "").Replace(" ", "");

        return null;
    }

    private string? ExtractIncoterm(string text)
    {
        foreach (var term in Incoterms)
        {
            if (Regex.IsMatch(text, @"(?:INCOTERM|TERMS|CONDI[CÇ][AÃ]O|CONDICAO)[\s:\.\-]*\b" + term + @"\b", RegexOptions.IgnoreCase))
                return term;
        }
        foreach (var term in Incoterms)
        {
            if (Regex.IsMatch(text, @"\b" + term + @"\b"))
                return term;
        }
        return null;
    }

    // ==================== PARTICIPANTES ====================

    private ParticipanteInfo? ExtractParticipante(string text, string startPattern, string endPattern)
    {
        var m = Regex.Match(text, startPattern + @"[\s:\-]*(.{10,500}?)(?=" + endPattern + @"|$)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!m.Success) return null;
        var block = m.Groups[1].Value;
        return ParseParticipanteBlock(block);
    }

    private ParticipanteInfo? ExtractParticipanteSingle(string text, string label)
    {
        var m = Regex.Match(text, label + @"[\s:\-]*(.{10,300}?)(?=\n\n|\r\n\r\n|$)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!m.Success) return null;
        return ParseParticipanteBlock(m.Groups[1].Value);
    }

    private ParticipanteInfo ParseParticipanteBlock(string block)
    {
        var info = new ParticipanteInfo();
        var lines = block.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length > 0) info.RazaoSocial = lines[0].Trim().TrimEnd(':');

        info.CNPJ = ExtractPattern(block, @"CNPJ[\s:\-]*(\d{2}\.?\d{3}\.?\d{3}\/?\d{4}\-?\d{2})", null);
        info.InscricaoEstadual = ExtractPattern(block, @"(?:INSCRI[CÇ][AÃ]O\s*ESTADUAL|IE)[\s:\-]*(\d[\d\.\/\-]+)", null);
        info.IdentificacaoFiscal = ExtractPattern(block, @"(?:RUT|TAX\s*ID|NIF|TIN)[\s:\-]*([\w\.\-]+)", null);
        info.Telefone = ExtractPattern(block, @"(?:TEL|FONE|PHONE|FAX)[\s:\-]*([\d\(\)\+\-\.\s]{7,20})", null);
        info.Email = ExtractPattern(block, @"([\w\.\-]+@[\w\.\-]+\.\w{2,})", null);
        info.CEP = ExtractPattern(block, @"(?:CEP|ZIP|C[OÓ]DIGO\s*POSTAL)[\s:\-]*(\d{5}\-?\d{3}|\d{4,8})", null);

        return info;
    }

    // ==================== NF-e ====================

    private NfeInfo? ExtractNfe(string text)
    {
        // Só tenta se parece NF-e
        if (!Regex.IsMatch(text, @"(?:NF-?E|NOTA\s*FISCAL|DANFE|CHAVE\s*(?:DE\s*)?ACESSO)", RegexOptions.IgnoreCase))
            return null;

        var nfe = new NfeInfo();
        nfe.ChaveAcesso = ExtractPattern(text, @"(?:CHAVE\s*(?:DE\s*)?ACESSO)[\s:\-]*([\d\s]{44,60})", null);
        if (nfe.ChaveAcesso != null) nfe.ChaveAcesso = Regex.Replace(nfe.ChaveAcesso, @"\s", "");
        nfe.NumeroNF = ExtractPattern(text, @"(?:N[UÚ]MERO\s*(?:DA\s*)?(?:NOTA|NF)|NF\s*N[ºO]?)[\s:\-]*(\d+)", null);
        nfe.Serie = ExtractPattern(text, @"S[EÉ]RIE[\s:\-]*(\d+)", null);
        nfe.NaturezaOperacao = ExtractPattern(text, @"(?:NATUREZA\s*(?:DA\s*)?OPERA[CÇ][AÃ]O)[\s:\-]*(.{5,80}?)(?:\n|$)", null);
        nfe.DataEmissao = ExtractPattern(text, @"(?:DATA\s*(?:DE\s*)?EMISS[AÃ]O)[\s:\-]*(\d{1,2}[\/\.\-]\d{1,2}[\/\.\-]\d{2,4})", null);
        nfe.BaseCalculoICMS = ExtractPattern(text, @"(?:BASE\s*(?:DE\s*)?C[AÁ]LCULO\s*(?:DO\s*)?ICMS)[\s:\-]*([\d\.,]+)", null);
        nfe.ValorICMS = ExtractPattern(text, @"(?:VALOR\s*(?:DO\s*)?ICMS)[\s:\-]*([\d\.,]+)", null);
        nfe.ValorTotalProdutos = ExtractPattern(text, @"(?:VALOR\s*TOTAL\s*(?:DOS\s*)?PRODUTOS)[\s:\-]*([\d\.,]+)", null);
        nfe.ValorFrete = ExtractPattern(text, @"(?:VALOR\s*(?:DO\s*)?FRETE)[\s:\-]*([\d\.,]+)", null);
        nfe.ValorSeguro = ExtractPattern(text, @"(?:VALOR\s*(?:DO\s*)?SEGURO)[\s:\-]*([\d\.,]+)", null);
        nfe.Desconto = ExtractPattern(text, @"(?:DESCONTO)[\s:\-]*([\d\.,]+)", null);
        nfe.ValorIPI = ExtractPattern(text, @"(?:VALOR\s*(?:(?:DO|TOTAL)\s*)?IPI)[\s:\-]*([\d\.,]+)", null);
        nfe.ValorTotalNF = ExtractPattern(text, @"(?:VALOR\s*TOTAL\s*(?:DA\s*)?(?:NOTA|NF))[\s:\-]*([\d\.,]+)", null);
        nfe.PesoBruto = ExtractPattern(text, @"(?:PESO\s*BRUTO)[\s:\-]*([\d\.,]+)", null);
        nfe.PesoLiquido = ExtractPattern(text, @"(?:PESO\s*L[IÍ]QUIDO)[\s:\-]*([\d\.,]+)", null);
        nfe.QtdVolumes = ExtractPattern(text, @"(?:QUANTIDADE\s*(?:DE\s*)?VOLUMES|QTD\.?\s*VOL)[\s:\-]*(\d+)", null);
        nfe.EspecieVolumes = ExtractPattern(text, @"(?:ESP[EÉ]CIE)[\s:\-]*(\w+)", null);
        nfe.PlacaVeiculo = ExtractPattern(text, @"(?:PLACA)[\s:\-]*([A-Z]{3}\-?\d{1}[A-Z0-9]\d{2})", null);
        nfe.ProtocoloAutorizacao = ExtractPattern(text, @"(?:PROTOCOLO\s*(?:DE\s*)?AUTORIZA[CÇ][AÃ]O)[\s:\-]*([\d\s]+)", null);
        nfe.Observacoes = ExtractPattern(text, @"(?:DADOS\s*ADICIONAIS|INFORMA[CÇ][OÕ]ES\s*COMPLEMENTARES|OBSERVA[CÇ][OÕ]ES)[\s:\-]*(.{10,500}?)(?=\n\n|$)", null);

        return HasAnyValue(nfe) ? nfe : null;
    }

    // ==================== FATURA COMERCIAL ====================

    private FaturaComercialInfo? ExtractFaturaComercial(string text)
    {
        if (!Regex.IsMatch(text, @"(?:FATURA|FACTURA|INVOICE|COMMERCIAL\s*INVOICE)", RegexOptions.IgnoreCase))
            return null;

        var fc = new FaturaComercialInfo();
        fc.NumeroFatura = ExtractPattern(text, @"(?:FATURA|FACTURA|INVOICE)\s*(?:N[ºO]?|NR|NUMBER)?[\s:\.\-]*(\w[\w\-/\.]{2,20})", null);
        fc.DataFatura = ExtractPattern(text, @"(?:FECHA|DATA\s*(?:DA\s*)?FATURA|INVOICE\s*DATE|DATE)[\s:\-]*(\d{1,2}[\/\.\-]\d{1,2}[\/\.\-]\d{2,4})", null);
        fc.DataEmbarque = ExtractPattern(text, @"(?:EMBARCADO|SHIPPED|EMBARQUE|SHIPPING\s*DATE)[\s:\-]*(\d{1,2}[\/\.\-]\d{1,2}[\/\.\-]\d{2,4})", null);
        fc.CondicoesPagamento = ExtractPattern(text, @"(?:CONDI[CÇ][OÕ]ES?\s*(?:DE\s*)?PAGAMENTO|PAYMENT\s*TERMS|TERMOS?\s*(?:DE\s*)?PAGAMENTO)[\s:\-]*(.{5,80}?)(?:\n|$)", null);
        fc.Incoterm = ExtractIncoterm(text);
        fc.OrigemEmbarque = ExtractPattern(text, @"(?:ORIGEM|FROM|PORT\s*OF\s*LOADING|PORTO\s*(?:DE\s*)?CARGA|DE[\s:\-])[\s:\-]*([A-ZÀ-Ú][a-zA-ZÀ-ú\s\-,]{3,40}?)(?:\n|$)", null);
        fc.DestinoFinal = ExtractPattern(text, @"(?:DESTINO|TO|PORT\s*OF\s*DISCHARGE|DESTINATION|A[\s:\-])[\s:\-]*([A-ZÀ-Ú][a-zA-ZÀ-ú\s\-,]{3,40}?)(?:\n|$)", null);
        fc.MeioTransporte = ExtractPattern(text, @"(?:MEIO\s*(?:DE\s*)?TRANSPORTE|TRANSPORT|BUQUE|VESSEL|NAVIO)[\s:\-]*(.{3,50}?)(?:\n|$)", null);
        fc.NumeroContainer = ExtractPattern(text, @"(?:CONTAINER|CONT[EÊ]INER|REMESSA)[\s:\-]*([A-Z]{4}\d{7}|\w{5,20})", null);
        fc.MoedaOperacao = ExtractPattern(text, @"(?:MOEDA|CURRENCY)[\s:\-]*([A-Z]{3})", null);
        fc.TaxaCambio = ExtractPattern(text, @"(?:TAXA\s*(?:DE\s*)?C[AÂ]MBIO|EXCHANGE\s*RATE|COTIZA[CÇ][AÃ]O)[\s:\-]*([\d\.,]+)", null);
        fc.ValorTotalFatura = ExtractPattern(text, @"(?:TOTAL\s*(?:DA\s*)?FATURA|TOTAL\s*(?:INVOICE\s*)?VALUE|GRAND\s*TOTAL)[\s:\-]*[A-Z$R]*\s*([\d\.,]+)", null);
        fc.ValorFrete = ExtractPattern(text, @"(?:VALOR\s*(?:DO\s*)?FRETE|FREIGHT)[\s:\-]*[A-Z$R]*\s*([\d\.,]+)", null);
        fc.ValorSeguro = ExtractPattern(text, @"(?:VALOR\s*(?:DO\s*)?SEGURO|INSURANCE)[\s:\-]*[A-Z$R]*\s*([\d\.,]+)", null);

        return HasAnyValue(fc) ? fc : null;
    }

    // ==================== DU-E ====================

    private DueInfo? ExtractDue(string text)
    {
        if (!Regex.IsMatch(text, @"(?:DU-?E|DECLARA[CÇ][AÃ]O\s*[UÚ]NICA\s*(?:DE\s*)?EXPORTA[CÇ][AÃ]O)", RegexOptions.IgnoreCase))
            return null;

        var due = new DueInfo();
        due.NumeroDUE = ExtractPattern(text, @"(?:DU-?E|N[UÚ]MERO\s*(?:DA\s*)?DU-?E)[\s:\-]*([\dA-Z\-\/]+)", null);
        due.RUC = ExtractPattern(text, @"(?:RUC)[\s:\-]*([\w\-]+)", null);
        due.ChaveAcesso = ExtractPattern(text, @"(?:CHAVE\s*(?:DE\s*)?ACESSO\s*DU-?E)[\s:\-]*([\d\s]{20,60})", null);
        due.FormaExportacao = ExtractPattern(text, @"(?:FORMA\s*(?:DE\s*)?EXPORTA[CÇ][AÃ]O)[\s:\-]*(.{5,40}?)(?:\n|$)", null);
        due.PaisImportador = ExtractPattern(text, @"(?:PA[IÍ]S\s*(?:DO\s*)?IMPORTADOR)[\s:\-]*(.{3,30}?)(?:\n|$)", null);
        due.MoedaNegociacao = ExtractPattern(text, @"(?:MOEDA\s*(?:DE\s*)?NEGOCIA[CÇ][AÃ]O)[\s:\-]*([A-Z]{3})", null);
        due.VMLE = ExtractPattern(text, @"(?:VMLE)[\s:\-]*([\d\.,]+)", null);
        due.VMCV = ExtractPattern(text, @"(?:VMCV)[\s:\-]*([\d\.,]+)", null);
        due.ValorTotalReais = ExtractPattern(text, @"(?:VALOR\s*TOTAL\s*(?:DAS\s*)?MERCADORIAS\s*(?:EM\s*)?R(?:EAIS|\$))[\s:\-]*([\d\.,]+)", null);
        due.PesoLiquidoTotal = ExtractPattern(text, @"(?:PESO\s*L[IÍ]QUIDO\s*TOTAL)[\s:\-]*([\d\.,]+)", null);

        return HasAnyValue(due) ? due : null;
    }

    // ==================== CRT ====================

    private CrtInfo? ExtractCrt(string text)
    {
        if (!Regex.IsMatch(text, @"(?:CRT|MIC[\s\/\-]*DTA|CONHECIMENTO\s*(?:DE\s*)?TRANSPORTE\s*INTERNACIONAL)", RegexOptions.IgnoreCase))
            return null;

        var crt = new CrtInfo();
        crt.NumeroCRT = ExtractPattern(text, @"(?:CRT|CONHECIMENTO\s*(?:DE\s*)?TRANSPORTE)[\s:\-N[ºO]*]*([\d\-\/]+)", null);
        crt.NumeroMICDTA = ExtractPattern(text, @"(?:MIC[\s\/\-]*DTA)[\s:\-N[ºO]*]*([\d\-\/]+)", null);
        crt.CidadeDestino = ExtractPattern(text, @"(?:DESTINO\s*FINAL|CIDADE\s*(?:DE\s*)?DESTINO)[\s:\-]*(.{3,40}?)(?:\n|$)", null);
        crt.PaisDestino = ExtractPattern(text, @"(?:PA[IÍ]S\s*(?:DE\s*)?DESTINO)[\s:\-]*(.{3,30}?)(?:\n|$)", null);
        crt.ValorFOB = ExtractPattern(text, @"(?:VALOR\s*FOB|FOT)[\s:\-]*([\d\.,]+)", null);
        crt.ValorFreteInternacional = ExtractPattern(text, @"(?:FRETE\s*INTERNACIONAL|FRETE\s*EXTERNO)[\s:\-]*([\d\.,]+)", null);
        crt.ValorSeguro = ExtractPattern(text, @"(?:VALOR\s*(?:DO\s*)?SEGURO)[\s:\-]*([\d\.,]+)", null);
        crt.QtdVolumes = ExtractPattern(text, @"(?:QUANTIDADE\s*(?:DE\s*)?VOLUMES|QTD\.?\s*VOL)[\s:\-]*(\d+)", null);
        crt.PesoBruto = ExtractPattern(text, @"(?:PESO\s*BRUTO)[\s:\-]*([\d\.,]+)", null);
        crt.PesoLiquido = ExtractPattern(text, @"(?:PESO\s*L[IÍ]QUIDO)[\s:\-]*([\d\.,]+)", null);
        crt.VolumeM3 = ExtractPattern(text, @"(?:VOLUME|CBM|M[³3])[\s:\-]*([\d\.,]+)", null);
        crt.LacresCarga = ExtractPattern(text, @"(?:LACRE|PRECINTA|SEAL)[\s:\-]*([\w\-\s,]+?)(?:\n|$)", null);

        return HasAnyValue(crt) ? crt : null;
    }

    // ==================== CERTIFICADO DE ORIGEM ====================

    private CertificadoOrigemInfo? ExtractCertificadoOrigem(string text)
    {
        if (!Regex.IsMatch(text, @"(?:CERTIFICADO\s*(?:DE\s*)?ORIGEM|CERTIFICATE\s*OF\s*ORIGIN|ACE\s*\d+)", RegexOptions.IgnoreCase))
            return null;

        var co = new CertificadoOrigemInfo();
        co.NumeroCertificado = ExtractPattern(text, @"(?:CERTIFICADO|CERTIFICATE)\s*(?:N[ºO]?|NR)[\s:\-]*([\w\-\/]+)", null);
        co.EntidadeEmissora = ExtractPattern(text, @"(?:ENTIDADE\s*EMISSORA|ISSUING\s*AUTHORITY)[\s:\-]*(.{5,80}?)(?:\n|$)", null);
        co.AcordoComercial = ExtractPattern(text, @"(?:ACORDO|ACE|AGREEMENT)[\s:\-]*(.{5,60}?)(?:\n|$)", null);
        co.PaisDestino = ExtractPattern(text, @"(?:PA[IÍ]S\s*(?:DE\s*)?DESTINO|DESTINATION\s*COUNTRY)[\s:\-]*(.{3,30}?)(?:\n|$)", null);
        co.MeioTransporte = ExtractPattern(text, @"(?:MEIO\s*(?:DE\s*)?TRANSPORTE|MODE\s*OF\s*TRANSPORT)[\s:\-]*(.{3,40}?)(?:\n|$)", null);

        return HasAnyValue(co) ? co : null;
    }

    // ==================== PACKING LIST ====================

    private PackingListInfo? ExtractPackingList(string text)
    {
        if (!Regex.IsMatch(text, @"(?:PACKING\s*LIST|ROMANEIO|RASTREABILIDADE|LIVRE\s*VENDA|SIF)", RegexOptions.IgnoreCase))
            return null;

        var pl = new PackingListInfo();
        pl.SKU = ExtractPattern(text, @"(?:SKU|C[OÓ]DIGO\s*(?:DO\s*)?MATERIAL)[\s:\-]*([\w\-]+)", null);
        pl.NumeroLote = ExtractPattern(text, @"(?:LOTE|LOT|BATCH)[\s:\-NºO]*([\w\-\/]+)", null);
        pl.DataFabricacao = ExtractPattern(text, @"(?:DATA\s*(?:DE\s*)?FABRICA[CÇ][AÃ]O|PRODUCTION\s*DATE|SLAUGHTER)[\s:\-]*(\d{1,2}[\/\.\-]\d{1,2}[\/\.\-]\d{2,4})", null);
        pl.DataValidade = ExtractPattern(text, @"(?:VALIDADE|VENCIMENTO|EXPIRY|BEST\s*BEFORE)[\s:\-]*(\d{1,2}[\/\.\-]\d{1,2}[\/\.\-]\d{2,4})", null);
        pl.NumeroSIF = ExtractPattern(text, @"(?:SIF)[\s:\-NºO]*([\d\-\/]+)", null);
        pl.Calibre = ExtractPattern(text, @"(?:CALIBRE|GAUGE|SPECIFICATION)[\s:\-]*(.{3,30}?)(?:\n|$)", null);

        return HasAnyValue(pl) ? pl : null;
    }

    // ==================== ITENS ====================

    private List<ItemMercadoria>? ExtractItens(string text)
    {
        // Tenta achar linhas com NCM + descrição + valores
        var items = new List<ItemMercadoria>();
        var ncmMatches = Regex.Matches(text, @"\b(\d{4}\.\d{2}\.\d{2})\b");
        foreach (Match ncm in ncmMatches)
        {
            var startIdx = Math.Max(0, ncm.Index - 50);
            var endIdx = Math.Min(text.Length, ncm.Index + ncm.Length + 200);
            var context = text.Substring(startIdx, endIdx - startIdx);

            var item = new ItemMercadoria
            {
                NCM = ncm.Value.Replace(".", ""),
            };

            // Tenta pegar descrição após NCM
            var descMatch = Regex.Match(context.Substring(ncm.Index - startIdx + ncm.Length),
                @"[\s\-]*(.{5,120}?)(?:\d{1,3}[\.,]\d|$|\n)", RegexOptions.Singleline);
            if (descMatch.Success) item.DescricaoProduto = descMatch.Groups[1].Value.Trim();

            item.CFOP = ExtractPattern(context, @"\b(\d{4})\b", null);
            item.UnidadeMedida = ExtractPattern(context, @"\b(KG|UN|CX|M3|PC|TON|M/T|LT)\b", null);

            items.Add(item);
        }

        return items.Count > 0 ? items : null;
    }

    // ==================== TRANSPORTADOR ====================

    private TransportadorInfo? ExtractTransportador(string text)
    {
        if (!Regex.IsMatch(text, @"(?:TRANSPORTADOR|CARRIER|TRANSPORTISTA)", RegexOptions.IgnoreCase))
            return null;

        var t = new TransportadorInfo();
        t.RazaoSocial = ExtractPattern(text, @"(?:TRANSPORTADOR|CARRIER|TRANSPORTISTA)[\s:\-]*([^\n]{5,60})", null);
        t.CNPJ = ExtractPattern(text, @"CNPJ[\s:\-]*(\d{2}\.?\d{3}\.?\d{3}\/?\d{4}\-?\d{2})", null);
        t.PlacaCaminhao = ExtractPattern(text, @"(?:PLACA\s*(?:DO\s*)?(?:CAMINH[AÃ]O|TRATOR|VE[IÍ]CULO))[\s:\-]*([A-Z]{3}\-?\d{1}[A-Z0-9]\d{2})", null);
        if (t.PlacaCaminhao == null)
            t.PlacaCaminhao = ExtractPattern(text, @"PLACA[\s:\-]*([A-Z]{3}\-?\d{1}[A-Z0-9]\d{2})", null);
        t.PlacaReboque = ExtractPattern(text, @"(?:REBOQUE|SEMI[\s\-]?REBOQUE|CARRETA)[\s:\-]*(?:PLACA[\s:\-]*)?([A-Z]{3}\-?\d{1}[A-Z0-9]\d{2})", null);
        t.RegistroANTT = ExtractPattern(text, @"(?:ANTT|RNTRC)[\s:\-]*([\d\-]+)", null);

        return HasAnyValue(t) ? t : null;
    }

    // ==================== UTILITY ====================

    private bool HasAnyValue(object obj)
    {
        var props = obj.GetType().GetProperties();
        foreach (var p in props)
        {
            var val = p.GetValue(obj);
            if (val is string s && !string.IsNullOrWhiteSpace(s)) return true;
            if (val != null && val is not string) return true;
        }
        return false;
    }
}
