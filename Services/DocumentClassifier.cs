using System;
using System.IO;
using System.Text.RegularExpressions;
using ComexTool.Models;

namespace ComexTool.Services;

public static class DocumentClassifier
{
    public static DocumentType Classify(string fileName, string text)
    {
        string nameUpper = fileName.ToUpperInvariant();
        string textUpper = (text ?? string.Empty).ToUpperInvariant();

        // 1. NF-e / DANFE
        if (nameUpper.EndsWith("NF.PDF") || nameUpper.Contains("NFE") || nameUpper.Contains("DANFE") ||
            textUpper.Contains("DANFE") || textUpper.Contains("DOCUMENTO AUXILIAR DA NOTA FISCAL") || textUpper.Contains("CHAVE DE ACESSO"))
        {
            return DocumentType.NotaFiscal;
        }

        // 2. DU-E
        if (nameUpper.Contains("DUE") || nameUpper.Contains("DU-E") ||
            textUpper.Contains("DECLARAÇÃO ÚNICA DE EXPORTAÇÃO") || textUpper.Contains("DECLARACAO UNICA DE EXPORTACAO") || textUpper.Contains("EXTRATO SIMPLIFICADO DU-E") || textUpper.Contains("EXTRATO-DUE"))
        {
            return DocumentType.DUE;
        }

        // 3. Certificado de Origem
        if (nameUpper.StartsWith("CO ") || nameUpper.Contains("CERTIFICADO") && nameUpper.Contains("ORIGEM") ||
            textUpper.Contains("CERTIFICADO DE ORIGEM") || textUpper.Contains("CERTIFICACION DE ORIGEN") || textUpper.Contains("ACE Nº 35") || textUpper.Contains("ACE 35") || textUpper.Contains("NORMAS DE ORIGEM"))
        {
            return DocumentType.CertificadoOrigem;
        }

        // 4. CRT / MIC-DTA
        if (nameUpper.Contains("CRT") || textUpper.Contains("CARTA DE PORTE") || textUpper.Contains("CONHECIMENTO DE TRANSPORTE INTERNACIONAL"))
        {
            return DocumentType.CRT;
        }
        if (nameUpper.Contains("MIC") || textUpper.Contains("MANIFESTO INTERNACIONAL DE CARGA") || textUpper.Contains("DECLARAÇÃO DE TRÂNSITO ADUANEIRO"))
        {
            return DocumentType.MIC_DTA;
        }

        // 5. Certificado Sanitário Internacional (CSI)
        if (nameUpper.Contains("CSI") || textUpper.Contains("CERTIFICADO SANITÁRIO INTERNACIONAL") || textUpper.Contains("CERTIFICADO SANITARIO INTERNACIONAL"))
        {
            return DocumentType.CSI;
        }

        // 6. Certificado de Livre Venda
        if (nameUpper.Contains("LIVRE") || textUpper.Contains("CERTIFICADO DE LIVRE VENDA") || textUpper.Contains("CERTIFICADO DE LIBRE VENTA"))
        {
            return DocumentType.CertificadoLivreVenda;
        }

        // 7. Packing List / Romaneio / Rastreabilidade
        if (nameUpper.Contains("P_LIST") || nameUpper.Contains("PACKING") || nameUpper.Contains("ROMANEIO") || nameUpper.Contains("TRACEABILI") ||
            textUpper.Contains("PACKING LIST") || textUpper.Contains("TRACEABILITY CERTIFICATE") || textUpper.Contains("ROMANEIO DE CARGA"))
        {
            return DocumentType.PackingList;
        }

        // 8. Fatura Comercial
        if (nameUpper.Contains("FATURA") || nameUpper.Contains("FACTURA") || nameUpper.Contains("INVOICE") || nameUpper.StartsWith("FAT ") ||
            textUpper.Contains("FACTURA COMERCIAL") || textUpper.Contains("COMMERCIAL INVOICE") || textUpper.Contains("FATURA COMERCIAL"))
        {
            return DocumentType.Fatura;
        }

        // 9. Bill of Lading (B/L) / AWB
        if (nameUpper.Contains("BL") || nameUpper.Contains("BILL") || textUpper.Contains("BILL OF LADING"))
        {
            return DocumentType.BillOfLading;
        }
        if (nameUpper.Contains("AWB") || textUpper.Contains("AIR WAYBILL"))
        {
            return DocumentType.AirWayBill;
        }

        return DocumentType.Outro;
    }
}
