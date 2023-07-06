namespace TSF.Feature.SupporterPortalMvc.Services
{
    using CSharpFunctionalExtensions;
    using System.Threading.Tasks;
    using TSF.Feature.SupporterPortalMvc.Models.Dto;
    using System;
    using TSF.Foundation.SupporterPortal.Models.Domain;
    using System.Collections.Generic;
    using TSF.Foundation.CRM_D365.Enums;
    using TSF.Feature.SupporterPortalMvc.Models.Domain;

    public interface IPaymentDetailsService
    {
        Result<IPaymentDetails> CreatePaymentDetailsModel();
        Task<Result<PaymentDetailsApiModel>> CreatePaymentDetailsApiModel(Guid dataSourceID);
        Task<List<RegularPaymentDetails>> GetPaymentDetails(SupporterClass supporterClass, Guid CrmId);
        Task<Result> CreateCreditCardPaymentAccount(CreditCardDetailsDto creditCardDetails);
    }
}
