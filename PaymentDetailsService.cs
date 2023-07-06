using AutoMapper;
using CSharpFunctionalExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TSF.Feature.SupporterPortalMvc.Models.Domain;
using TSF.Feature.SupporterPortalMvc.Models.Dto;
using TSF.Feature.SupporterPortalMvc.Repositories;
using TSF.Foundation.Content.Models;
using TSF.Foundation.Content.Repositories;
using TSF.Foundation.CRM.Types;
using TSF.Foundation.Helpers;
using TSF.Foundation.Logging.Repositories;
using TSF.Foundation.Portal.Services;
using TSF.Foundation.SupporterPortal.ApiClient;
using TSF.Foundation.Extensions;
using TSF.Foundation.SupporterPortal.Models.Domain;
using TSF.Foundation.Content.Services;
using TSF.Foundation.CRM_D365.Enums;
using Glass.Mapper.Sc.Fields;
using TSF.Foundation.SupporterPortal.Models.Dto;
using TSF.Foundation.WebApi.Services;
using TSF.Foundation.Payment.Services;

namespace TSF.Feature.SupporterPortalMvc.Services
{
    public class PaymentDetailsService : IPaymentDetailsService
    {
        private readonly IRenderingRepository _renderingRepository;
        private readonly ILogRepository _logRepository;
        private readonly IContentRepository _contentRepository;
        private readonly ISupporterPortalMvcRepository _supporterPortalMvcRepository;
        private readonly ISupporterCRMApiClient _supporterCRMApiClient;
        private readonly IContentService _contentService;
        private readonly IGenderService _genderService;
        private readonly ISessionService _sessionService;
        private readonly IResponseService _responseService;
        private readonly IProcessingOverlayService _processingOverlayService;
        private readonly IPaymentService _paymentService;

        public PaymentDetailsService(IRenderingRepository renderingRepository,
            ILogRepository logRepository,
            IContentRepository contentRepository,
            ISupporterPortalMvcRepository supporterPortalMvcRepository,
            ISupporterCRMApiClient supporterCRMApiClient,
            IContentService contentService,
            IGenderService genderService,
            ISessionService sessionService,
            IResponseService responseService,
            IProcessingOverlayService processingOverlayService,
            IPaymentService paymentService)
        {
            _renderingRepository = renderingRepository;
            _logRepository = logRepository;
            _contentRepository = contentRepository;
            _supporterPortalMvcRepository = supporterPortalMvcRepository;
            _supporterCRMApiClient = supporterCRMApiClient;
            _contentService = contentService;
            _genderService = genderService;
            _sessionService = sessionService;
            _responseService = responseService;
            _processingOverlayService = processingOverlayService;
            _paymentService = paymentService;
        }

        #region Public Methods
        public Result<IPaymentDetails> CreatePaymentDetailsModel()
        {
            IPaymentDetails modelDatasource = _renderingRepository.GetDataSourceItem<IPaymentDetails>();
            if(modelDatasource == null)
            {
                _logRepository.Warn("Datasource is null PaymentDetailsService.CreatePaymentDetailsModel");
                return Result.Failure<IPaymentDetails>("CreatePaymentDetailsModel : Datasource is null");
            }

            return Result.Success(modelDatasource);
        }

        public async Task<Result<PaymentDetailsApiModel>> CreatePaymentDetailsApiModel(Guid dataSourceID)
        {
            IPaymentDetails modelDatasource = _contentRepository.GetItem<IPaymentDetails>(dataSourceID, Sitecore.Context.Database);
            if(modelDatasource == null)
            {
                _logRepository.Warn("Datasource is null PaymentDetailsService CreatePaymentDetailsApiModel");
                return Result.Failure<PaymentDetailsApiModel>("CreatePaymentDetailsApiModel : Datasource is null");
            }

            // CRM Input Data
            var loginUserInfo = _sessionService.GetLoginUserInfo();

            List<RegularPaymentDetails> paymentDetails = await GetPaymentDetails(loginUserInfo.CustomerType, loginUserInfo.CustomerEntityGuid);
            if(paymentDetails == null)
            {
                _logRepository.Warn("GetPaymentDetails API result is null PaymentDetailsService CreatePaymentDetailsApiModel");
                return Result.Failure<PaymentDetailsApiModel>("CreatePaymentDetailsApiModel : API result is null");
            }

            PaymentDetailsApiModel paymentDetailsModel = Mapper.Map<IPaymentDetails, PaymentDetailsApiModel>(modelDatasource);
            paymentDetailsModel.Payframe.ErrorValidationMessage = _responseService.GetValidationMessage(modelDatasource.ErrorValidationMessage.ID);
            paymentDetailsModel.Payframe.FailureValidationMessage = _responseService.GetValidationMessage(modelDatasource.FailureValidationMessage.ID);
            paymentDetailsModel.Payframe.SuccessValidationMessage = _responseService.GetValidationMessage(modelDatasource.SuccessValidationMessage.ID);
            FillMWReCaptchaCredentials(paymentDetailsModel);

            foreach(var regularPaymentDetailsItem in paymentDetails)
            {
                Payment payment = Mapper.Map<RegularPaymentDetails, Payment>(regularPaymentDetailsItem);
                FillPaymentDetails(payment, regularPaymentDetailsItem, modelDatasource);
                AddPaymentDetails(paymentDetailsModel, payment);
            }
            paymentDetailsModel.ProcessingOverlay = _processingOverlayService.MapProcessingOverlay(modelDatasource);
            return Result.Success(paymentDetailsModel);
        }

        public async Task<List<RegularPaymentDetails>> GetPaymentDetails(SupporterClass supporterClass, Guid CrmId)
        {
            List<RegularPaymentDetails> paymentDetails;

            paymentDetails = await _supporterCRMApiClient.GetRegularPaymentDetails(supporterClass, CrmId);

            return paymentDetails;
        }

        public async Task<Result> CreateCreditCardPaymentAccount(CreditCardDetailsDto creditCardDetails)
        {
            string apiErrorMessage = _responseService.GetValidationMessage(Constants.ValidationMessage.SurveyResponseFailure);

            if(creditCardDetails == null)
            {
                _logRepository.Warn("Error in PaymentDetailsService CreateCreditCardPaymentAccount : Input object is null");
                return Result.Failure(apiErrorMessage);
            }

            // CRM Input Data
            var loginUserInfo = _sessionService.GetLoginUserInfo();
            if(loginUserInfo == null)
            {
                _logRepository.Warn("Error in PaymentDetailsService CreateCreditCardPaymentAccount : loginUserInfo is null");
                return Result.Failure(apiErrorMessage);
            }

            PaymentAccountRequestDto paymentAccountRequestDto = Mapper.Map<PaymentAccountRequestDto>(creditCardDetails);
            paymentAccountRequestDto.CustomerType = loginUserInfo.CustomerType; //supporterClass
            paymentAccountRequestDto.CustomerEntityGuid = loginUserInfo.CustomerEntityGuid; 

            PaymentAccountRequestReturn paymentAccountRequestReturn = await _supporterCRMApiClient.CreateCreditCardPaymentAccount(paymentAccountRequestDto);
            if(paymentAccountRequestReturn == null)
            {
                _logRepository.Warn("CreateCreditCardPaymentAccount API result is null PaymentDetailsService CreateCreditCardPaymentAccount");
                return Result.Failure(apiErrorMessage);
            }

            return Result.Success(paymentAccountRequestReturn);
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Gets the Payment Method Item
        /// </summary>
        /// <param name="paymentMethod"></param>
        /// <returns></returns>
        private IPaymentMethod GetPaymentMethod(IListItem paymentType, CreditCardType? creditCardType)
        {
            List<IPaymentMethod> PaymentMethods = _supporterPortalMvcRepository.GetPaymentMethods(Constants.ItemIds.PaymentMethodsID.Guid);

            if(creditCardType != null)
            {
                return PaymentMethods?.Find(x => (x.PaymentType.ID == paymentType.ID) && (x.CreditCardCRMValue == creditCardType));
            }

            return PaymentMethods?.Find(x => (x.PaymentType.ID == paymentType.ID));
        }

        private IListItem GetPaymentType(PaymentMethod donationCategory)
        {
            List<IListItem> paymentTypeDatasource = _supporterPortalMvcRepository.GetListItems(Constants.ItemIds.PaymentTypeListID.Guid);
            return paymentTypeDatasource?.Find(x => x.Value == ((int)donationCategory).ToString());
        }

        private void FillPaymentDetails(Payment payment, RegularPaymentDetails regularPaymentDetails, IPaymentDetails modelDatasource)
        {
            IListItem itemPaymentType = GetPaymentType(regularPaymentDetails.PaymentMethod);
            IPaymentMethod itemPaymentMethod = GetPaymentMethod(itemPaymentType, regularPaymentDetails.PaymentAccountDetails?.CreditCardType);
            Mapper.Map<IPaymentMethod, Payment>(itemPaymentMethod, payment);
            payment.PaymentType = itemPaymentType.Name_Field;
            if(regularPaymentDetails.PaymentMethod == PaymentMethod.PaymentGateway)
            {
                payment.PaymentText = $"{regularPaymentDetails.PaymentAccountDetails?.CreditCardNumberFirst4Digits}{Constants.GlobalSettings.CrediCardMasking}{regularPaymentDetails.PaymentAccountDetails?.CreditCardNumberLast4Digits}";
                payment.PaymentName = regularPaymentDetails.PaymentAccountDetails?.CreditCardName;
                payment.PaymentCardID = regularPaymentDetails.PaymentAccountDetails?.PaymentCardId;
                payment.CreditCardTypeText = itemPaymentMethod?.CreditCardTypeText;
                payment.CreditCardExpiryLabel = itemPaymentMethod?.CreditCardExpiryLabel;
                payment.CreditCardExpiryMonth = regularPaymentDetails.PaymentAccountDetails?.CreditCardExpiry.Split('/')[0];
                payment.CreditCardExpiryYear = regularPaymentDetails.PaymentAccountDetails?.CreditCardExpiry.Split('/')[1];
                payment.CreditCardExpired = regularPaymentDetails.PaymentAccountDetails?.CreditCardExpired;
            }
            else if(regularPaymentDetails.PaymentMethod == PaymentMethod.BankAccount)
            {
                payment.PaymentText = regularPaymentDetails.PaymentAccountDetails?.BankAccountNumber;
                payment.PaymentName = regularPaymentDetails.PaymentAccountDetails?.BankAccountName;
            }
            else
            {
                payment.PaymentText = itemPaymentMethod?.PaymentLabel;
            }
            if(regularPaymentDetails.PledgeDueNow)
            {
                payment.isAnyPaymentOverdue = regularPaymentDetails.PledgeDueNow;
                payment.BadgeText = itemPaymentMethod?.PaymentOverdueBadgeLabel;
            }

            RegularPayment regularPayment = new RegularPayment();
            if(regularPaymentDetails.PledgeCategory == PledgeCategory.RegularDonation)
            {
                regularPayment.SupportTypeImage = GetSpporterTypeImage(regularPaymentDetails.PledgeCategory, modelDatasource.RegularDonationImage);
                regularPayment.SupportTypeText = GetSpporterTypeText(regularPaymentDetails.PledgeCategory, modelDatasource.RegularDonationText, regularPaymentDetails.PledgeFrequency);
                regularPayment.AmountValue = GetAmountValue(regularPaymentDetails.PledgeCategory, regularPaymentDetails.PledgeAmount);
                regularPayment.AmountFrequency = regularPaymentDetails.PledgeFrequency.StringValue();
                regularPayment.DonatedSince = regularPaymentDetails.PledgeStartDate.GetDateString(ExtensionConstants.TSFDateTimeDisplayFormat, true);
                regularPayment.NextDue = regularPaymentDetails.PledgeNextDueDate.GetDateString(ExtensionConstants.TSFDateTimeDisplayFormat, true);
                regularPayment.isPaymentOverdue = regularPaymentDetails.PledgeDueNow;
                payment.RegularPayments.Add(regularPayment);
            }
            else //PledgeCategory.Sponsorship
            {
                foreach(var participantSummary in regularPaymentDetails.ParticipantsSummary)
                {
                    regularPayment = new RegularPayment();
                    regularPayment.SupportTypeImage = GetSpporterTypeImage(regularPaymentDetails.PledgeCategory, modelDatasource.RegularDonationImage, participantSummary.ParticipantGender);
                    regularPayment.SupportTypeText = GetSpporterTypeText(regularPaymentDetails.PledgeCategory, modelDatasource.RegularDonationText, regularPaymentDetails.PledgeFrequency, participantSummary.ParticipantFirstName);
                    regularPayment.AmountValue = GetAmountValue(regularPaymentDetails.PledgeCategory, regularPaymentDetails.PledgeAmount, participantSummary.ParticipantPledgeFrequencyAmount);
                    regularPayment.AmountFrequency = regularPaymentDetails.PledgeFrequency.StringValue();
                    regularPayment.DonatedSince = regularPaymentDetails.PledgeStartDate.GetDateString(ExtensionConstants.TSFDateTimeDisplayFormat, true);
                    regularPayment.NextDue = regularPaymentDetails.PledgeNextDueDate.GetDateString(ExtensionConstants.TSFDateTimeDisplayFormat, true);
                    regularPayment.isPaymentOverdue = regularPaymentDetails.PledgeDueNow;
                    payment.RegularPayments.Add(regularPayment);
                }
            }

        }

        private static void AddPaymentDetails(PaymentDetailsApiModel paymentDetailsModel, Payment payment)
        {
            if (paymentDetailsModel.Payments?.Count(x => ((x.PaymentAccountGUID == payment.PaymentAccountGUID || x.PaymentCardID == payment.PaymentCardID) && x.PaymentType == payment.PaymentType)) > 0)
            {
                Payment existingPayment = paymentDetailsModel.Payments?.First(x => (x.PaymentAccountGUID == payment.PaymentAccountGUID || x.PaymentCardID == payment.PaymentCardID) && x.PaymentType == payment.PaymentType);

                if (existingPayment != null)
                {
                    existingPayment?.RegularPayments.AddRange(payment.RegularPayments);
                    if (payment.isAnyPaymentOverdue)
                    {
                        existingPayment.isAnyPaymentOverdue = payment.isAnyPaymentOverdue;
                        existingPayment.BadgeText = payment.BadgeText;
                    }
                }
            }
            else
            {
                paymentDetailsModel.Payments.Add(payment);
            }
        }

        private void FillMWReCaptchaCredentials(PaymentDetailsApiModel paymentDetailsModel)
        {
            var mwReCaptchaCredentials = _paymentService.GetMWAndReCaptchaCredentials();

            paymentDetailsModel.Payframe.RecaptchaSiteKey = mwReCaptchaCredentials.RecaptchaSiteKey;
            paymentDetailsModel.Payframe.UUID = mwReCaptchaCredentials.UUID;
            paymentDetailsModel.Payframe.ApiKey = mwReCaptchaCredentials.ApiKey;
            paymentDetailsModel.Payframe.PayframeUrl = mwReCaptchaCredentials.PayframeUrl;
            paymentDetailsModel.Payframe.PayframeSubmitUrl = mwReCaptchaCredentials.PayframeSubmitUrl;
        }

        private string GetSpporterTypeImage(PledgeCategory pledgeCategory, Image regularDonationImage, GenderType ParticipantGender = GenderType.NotDefined)
        {
            string imageUrl = string.Empty;
            if(pledgeCategory == PledgeCategory.RegularDonation)
            {
                imageUrl = regularDonationImage?.Src;
            }
            else if(pledgeCategory == PledgeCategory.Sponsorship)
            {
                imageUrl = _genderService.GetGenderImage(ParticipantGender)?.Src;
            }
            return imageUrl;
        }

        private string GetSpporterTypeText(PledgeCategory pledgeCategory, string regularDonationText, PledgeFrequency pledgeFrequency, string participantFirstName = "")
        {
            string text = string.Empty;
            if(pledgeCategory == PledgeCategory.RegularDonation)
            {
                var tokenDictionary = new Dictionary<string, string>
                        {
                            {Constants.Tokens.PledgeFrequency, pledgeFrequency.StringValue()}
                        };
                text = regularDonationText.Replace(tokenDictionary);
            }
            else if(pledgeCategory == PledgeCategory.Sponsorship)
            {
                text = participantFirstName;
            }
            return text;
        }

        private string GetAmountValue(PledgeCategory pledgeCategory, string pledgeAmount, string participantPledgeFrequencyAmount = "")
        {
            string amount = string.Empty;
            if(pledgeCategory == PledgeCategory.RegularDonation)
            {
                amount = pledgeAmount;
            }
            else if(pledgeCategory == PledgeCategory.Sponsorship)
            {
                amount = participantPledgeFrequencyAmount;
            }
            return amount;
        }
        #endregion
    }
}