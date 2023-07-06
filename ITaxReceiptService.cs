namespace TSF.Feature.SupporterPortalMvc.Services
{
    using CSharpFunctionalExtensions;
    using System.Threading.Tasks;
    using TSF.Feature.SupporterPortalMvc.Models.Dto;
    using System;
    using TSF.Foundation.SupporterPortal.Models.Domain;
    using System.Collections.Generic;
    using TSF.Feature.SupporterPortalMvc.Models.Domain;
    using GemBox.Document;
    using TSF.Foundation.CRM_D365.Enums;

    public interface ITaxReceiptService
    {
        Result<ITaxReceipts> CreateTaxReceiptsModel();
        Task<Result<TaxReceiptsApiModel>> CreateTaxReceiptsApiModel(Guid dataSourceID);
        Task<List<TaxReceiptsByFY>> GetTaxReceipts(Guid customerEntityGuid, SupporterClass supporterClass, int numberOfYears, bool forceRefresh = false);
        Result<DocumentModel> CreateTaxReceiptPDFDocument(string receiptNumber);
        Result<DocumentModel> SendTaxReceiptEmail(string taxReceiptNumber);
    }
}
