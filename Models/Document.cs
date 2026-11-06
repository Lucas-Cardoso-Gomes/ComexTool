using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace ComexTool.Models
{
    public enum DocumentType
    {
        [Display(Name = "Fatura Comercial")]
        Fatura,
        [Display(Name = "Packing List / Romaneio")]
        PackingList,
        [Display(Name = "Fatura Proforma")]
        Proforma,
        [Display(Name = "Nota Fiscal Eletrônica (NF-e)")]
        NotaFiscal,
        [Display(Name = "Certificado Sanitário Internacional (CSI)")]
        CSI,
        [Display(Name = "COCD")]
        COCD,
        [Display(Name = "Declaração Única de Exportação (DU-E)")]
        DUE,
        [Display(Name = "Conhecimento de Transporte (CRT)")]
        CRT,
        [Display(Name = "Manifesto Internacional (MIC/DTA)")]
        MIC_DTA,
        [Display(Name = "Certificado de Origem (ACE 35 / Mercosul)")]
        CertificadoOrigem,
        [Display(Name = "Certificado de Livre Venda")]
        CertificadoLivreVenda,
        [Display(Name = "Conhecimento de Embarque Marítimo (B/L)")]
        BillOfLading,
        [Display(Name = "Conhecimento Aéreo (AWB)")]
        AirWayBill,
        [Display(Name = "Outro Documento")]
        Outro
    }

    public enum StatusProcesso
    {
        EmAndamento,
        Conforme,
        ComDivergencia,
        Finalizado
    }

    public class ProcessoComex
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [StringLength(100)]
        public string NumeroProcesso { get; set; } = string.Empty;

        public string? Descricao { get; set; }

        public string? ReferenciaCliente { get; set; }

        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

        public DateTime? AtualizadoEm { get; set; }

        public StatusProcesso Status { get; set; } = StatusProcesso.EmAndamento;

        // Documentos anexados a este processo
        public List<Document> Documentos { get; set; } = new();
    }

    public class Document
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid? ProcessoId { get; set; }

        [ForeignKey(nameof(ProcessoId))]
        public ProcessoComex? Processo { get; set; }

        [Required]
        public DocumentType Type { get; set; }

        public string FileName { get; set; } = string.Empty;

        public byte[]? Content { get; set; }

        public string? ExtractedText { get; set; }

        /// <summary>JSON serialized ExtractedData object</summary>
        public string? ExtractedDataJson { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        [NotMapped]
        public ExtractedData? ExtractedData
        {
            get => string.IsNullOrEmpty(ExtractedDataJson) ? null : JsonSerializer.Deserialize<ExtractedData>(ExtractedDataJson);
            set => ExtractedDataJson = value == null ? null : JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = false });
        }
    }

    // ====================================================================
    //  Modelo completo de dados extraídos de documentos COMEX
    // ====================================================================

    public class ExtractedData
    {
        // --- Identificação Geral ---
        public string? NumeroDocumento { get; set; }
        public string? DataEmissao { get; set; }

        // --- Participantes (Atores da Operação) ---
        public ParticipanteInfo? Exportador { get; set; }
        public ParticipanteInfo? Importador { get; set; }
        public ParticipanteInfo? Consignatario { get; set; }
        public ParticipanteInfo? NotifyParty { get; set; }
        public TransportadorInfo? Transportador { get; set; }
        public string? CondutorNome { get; set; }
        public string? CondutorCpf { get; set; }
        public string? DespachanteNome { get; set; }
        public string? DespachanteCpf { get; set; }
        public string? DespachanteRegistro { get; set; }

        // --- NF-e / DANFE ---
        public NfeInfo? Nfe { get; set; }

        // --- Fatura Comercial ---
        public FaturaComercialInfo? FaturaComercial { get; set; }

        // --- DU-E ---
        public DueInfo? Due { get; set; }

        // --- CRT / MIC-DTA ---
        public CrtInfo? Crt { get; set; }

        // --- Certificado de Origem (ACE 35) ---
        public CertificadoOrigemInfo? CertificadoOrigem { get; set; }

        // --- Packing List, Rastreabilidade e Livre Venda ---
        public PackingListInfo? PackingList { get; set; }

        // --- Itens de Mercadoria ---
        public List<ItemMercadoria>? Itens { get; set; }

        // --- Campos genéricos consolidados ---
        public string? NCM { get; set; }
        public string? Incoterm { get; set; }
        public string? Moeda { get; set; }
        public string? ValorTotal { get; set; }
        public string? PesoBruto { get; set; }
        public string? PesoLiquido { get; set; }
        public string? VolumeTotalM3 { get; set; }
    }

    public class ParticipanteInfo
    {
        public string? RazaoSocial { get; set; }
        public string? CNPJ { get; set; }
        public string? InscricaoEstadual { get; set; }
        public string? IESubTributario { get; set; }
        public string? IdentificacaoFiscal { get; set; } // RUT, Tax ID, Rol de Contribuyente
        public string? Endereco { get; set; }
        public string? Rua { get; set; }
        public string? Numero { get; set; }
        public string? Bairro { get; set; }
        public string? Cidade { get; set; }
        public string? Estado { get; set; }
        public string? Pais { get; set; }
        public string? CEP { get; set; }
        public string? Telefone { get; set; }
        public string? Fax { get; set; }
        public string? Email { get; set; }
    }

    public class TransportadorInfo : ParticipanteInfo
    {
        public string? RegistroANTT { get; set; }
        public string? VencimentoANTT { get; set; }
        public string? LicencaOrigem { get; set; }
        public string? VencimentoLicencaOrigem { get; set; }
        public string? ApoliceSeguroCarga { get; set; }
        public string? VencimentoApoliceSeguro { get; set; }
        public string? PlacaCaminhao { get; set; }
        public string? PlacaReboque { get; set; }
        public string? MarcaVeiculo { get; set; }
        public string? NumeroChassi { get; set; }
        public string? CapacidadeTracao { get; set; }
        public string? AnoFabricacao { get; set; }
        public string? TaraCavalo { get; set; }
        public string? TaraCarreta { get; set; }
    }

    public class NfeInfo
    {
        public string? ChaveAcesso { get; set; }
        public string? NumeroNF { get; set; }
        public string? Serie { get; set; }
        public string? Folha { get; set; }
        public string? TipoOperacao { get; set; } // 0-Entrada / 1-Saída
        public string? NaturezaOperacao { get; set; }
        public string? ProtocoloAutorizacao { get; set; }
        public string? DataHoraAutorizacao { get; set; }
        public string? DataEmissao { get; set; }
        public string? DataSaidaEntrada { get; set; }
        public string? HoraSaida { get; set; }
        public string? BaseCalculoICMS { get; set; }
        public string? ValorICMS { get; set; }
        public string? BaseCalculoICMSST { get; set; }
        public string? ValorICMSST { get; set; }
        public string? ValorTotalProdutos { get; set; }
        public string? ValorFrete { get; set; }
        public string? ValorSeguro { get; set; }
        public string? Desconto { get; set; }
        public string? OutrasDespesas { get; set; }
        public string? ValorIPI { get; set; }
        public string? ValorTotalNF { get; set; }
        public string? ModalFrete { get; set; }
        public string? CodigoANTT { get; set; }
        public string? PlacaVeiculo { get; set; }
        public string? UFPlaca { get; set; }
        public string? QtdVolumes { get; set; }
        public string? EspecieVolumes { get; set; }
        public string? MarcaVolumes { get; set; }
        public string? NumeracaoVolumes { get; set; }
        public string? PesoBruto { get; set; }
        public string? PesoLiquido { get; set; }
        public string? Observacoes { get; set; }
    }

    public class FaturaComercialInfo
    {
        public string? NumeroFatura { get; set; }
        public string? DataFatura { get; set; }
        public string? DataEmbarque { get; set; }
        public string? CondicoesPagamento { get; set; }
        public string? Incoterm { get; set; }
        public string? LocalEntrega { get; set; }
        public string? OrigemEmbarque { get; set; }
        public string? DestinoFinal { get; set; }
        public string? MeioTransporte { get; set; }
        public string? MarcasMercadoria { get; set; }
        public string? TipoEmbalagem { get; set; }
        public string? NumeroContainer { get; set; }
        public string? MoedaOperacao { get; set; }
        public string? MoedasConversao { get; set; }
        public string? TaxaCambio { get; set; }
        public string? DataCotizacao { get; set; }
        public string? ValorTotalFatura { get; set; }
        public string? ValorFCA { get; set; }
        public string? ValorFrete { get; set; }
        public string? ValorSeguro { get; set; }
        public string? TotalPesoNeto { get; set; }
        public string? TotalPesoBruto { get; set; }
        public string? VolumeM3 { get; set; }
    }

    public class DueInfo
    {
        public string? NumeroDUE { get; set; }
        public string? RUC { get; set; }
        public string? ChaveAcesso { get; set; }
        public string? SituacaoControleAduaneiro { get; set; }
        public string? SituacaoControleAdministrativo { get; set; }
        public string? SituacaoControleCarga { get; set; }
        public string? FormaExportacao { get; set; }
        public string? TipoDocumentoFiscal { get; set; }
        public string? PaisImportador { get; set; }
        public string? MoedaNegociacao { get; set; }
        public string? VMLE { get; set; }
        public string? VMCV { get; set; }
        public string? ValorTotalReais { get; set; }
        public string? PesoLiquidoTotal { get; set; }
        public string? BeneficiosOEA { get; set; }
        public string? LocalDespachoRecinto { get; set; }
        public string? LocalDespachoCodigoRFB { get; set; }
        public string? LocalDespachoCoordenadas { get; set; }
        public string? LocalEmbarqueRecinto { get; set; }
        public string? LocalEmbarqueCodigoRFB { get; set; }
        public string? LocalEmbarqueCoordenadas { get; set; }
        public string? NotasFiscaisInstrutivas { get; set; }
        public string? HistoricoEventos { get; set; }
    }

    public class CrtInfo
    {
        public string? NumeroCRT { get; set; }
        public string? NumeroMICDTA { get; set; }
        public string? LocalEmissao { get; set; }
        public string? PaisEmissao { get; set; }
        public string? AlfandegaPartida { get; set; }
        public string? CidadePartida { get; set; }
        public string? PaisPartida { get; set; }
        public string? CidadeDestino { get; set; }
        public string? PaisDestino { get; set; }
        public string? AlfandegaDestino { get; set; }
        public string? DataCarga { get; set; }
        public string? LocalCarga { get; set; }
        public string? PrazoTransporte { get; set; }
        public string? RotaTransporte { get; set; }
        public string? PontosFronteira { get; set; }
        public string? ValorFOB { get; set; }
        public string? ValorFOT { get; set; }
        public string? ValorFreteInternacional { get; set; }
        public string? MoedaFrete { get; set; }
        public string? ValorSeguro { get; set; }
        public string? MoedaSeguro { get; set; }
        public string? MontanteRemetente { get; set; }
        public string? MontanteDestinatario { get; set; }
        public string? ValorMercadoriasCondicaoVenda { get; set; }
        public string? QtdVolumes { get; set; }
        public string? TipoVolumes { get; set; }
        public string? PesoBruto { get; set; }
        public string? PesoLiquido { get; set; }
        public string? VolumeM3 { get; set; }
        public string? LacresCarga { get; set; }
    }

    public class CertificadoOrigemInfo
    {
        public string? NumeroCertificado { get; set; }
        public string? EntidadeEmissora { get; set; }
        public string? CNPJEntidadeEmissora { get; set; }
        public string? ContatoEntidadeEmissora { get; set; }
        public string? AcordoComercial { get; set; }
        public string? NumeroFatura { get; set; }
        public string? DataFatura { get; set; }
        public string? PortoEmbarque { get; set; }
        public string? PaisDestino { get; set; }
        public string? MeioTransporte { get; set; }
        public string? EnquadramentoOrigem { get; set; }
        public string? TaxaCambioObservacao { get; set; }
        public string? ValorMoedaDestino { get; set; }
        public string? DeclaracaoOrigemExportador { get; set; }
        public string? CertificacaoEntidade { get; set; }
        public string? AssinaturaDigitalSignatario { get; set; }
        public string? AssinaturaDigitalDataHora { get; set; }
        public string? HashValidacao { get; set; }
    }

    public class PackingListInfo
    {
        public string? SKU { get; set; }
        public string? NumeroLote { get; set; }
        public string? DataFabricacao { get; set; }
        public string? DataValidade { get; set; }
        public string? NumeroSIF { get; set; }
        public string? CertificadoClassificacao { get; set; }
        public string? Calibre { get; set; }
        public string? TotalCaixasVolumes { get; set; }
        public string? PesoLiquidoTotal { get; set; }
        public string? PesoBrutoTotal { get; set; }
        public string? CertificadoLivreVendaNome { get; set; }
        public string? CertificadoLivreVendaCargo { get; set; }
        public string? CertificadoLivreVendaDeclaracao { get; set; }
        public string? CertificadoLivreVendaPermissao { get; set; }
        public string? CertificadoLivreVendaLocal { get; set; }
        public string? CertificadoLivreVendaData { get; set; }
    }

    public class ItemMercadoria
    {
        public int? NumeroOrdem { get; set; }
        public string? NCM { get; set; }
        public string? NALADI { get; set; }
        public string? DescricaoProduto { get; set; }
        public string? DescricaoAdicional { get; set; }
        public string? CST { get; set; }
        public string? CFOP { get; set; }
        public string? UnidadeMedida { get; set; }
        public string? Quantidade { get; set; }
        public string? QtdVolumes { get; set; }
        public string? PesoLiquido { get; set; }
        public string? PesoBruto { get; set; }
        public string? PrecoUnitario { get; set; }
        public string? PrecoTotal { get; set; }
        public string? BaseCalculoICMS { get; set; }
        public string? AliquotaICMS { get; set; }
        public string? ValorICMS { get; set; }
        public string? BaseCalculoICMSST { get; set; }
        public string? ValorICMSST { get; set; }
        public string? ValorIPI { get; set; }
    }
}
