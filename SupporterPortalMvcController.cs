namespace TSF.Feature.SupporterPortalMvc.Controllers
{
    using CSharpFunctionalExtensions;
    using Services;
    using System;
    using System.Web.Mvc;
    using TSF.Feature.SupporterPortalMvc.Models;
    using TSF.Feature.SupporterPortalMvc.Models.Domain;
    using TSF.Feature.SupporterPortalMvc.Models.Dto;
    using TSF.Feature.SupporterPortalMvc.ViewModels;
    using TSF.Foundation.Content.Base;
    using TSF.Foundation.Content.Models;
    using TSF.Foundation.Content.ViewModels;
    using TSF.Foundation.Core;

    public class SupporterPortalMvcController : MvcController
    {
        private readonly ISupporterPortalMvcService _supporterPortalMvcService;
        private readonly INotificationService _notificationService;
        private readonly ITaxReceiptService _taxReceiptService;
        private readonly IPaymentDetailsService _paymentDetailsServiceService;
        private readonly IProfileServiceApi _profileServiceApi;

        public SupporterPortalMvcController(ISupporterPortalMvcService supporterPortalMvcService,
            INotificationService notificationService,
            ITaxReceiptService taxReceiptService,
            IPaymentDetailsService paymentDetailsServiceService,
            IProfileServiceApi profileServiceApi)
        {
            _supporterPortalMvcService = supporterPortalMvcService;
            _notificationService = notificationService;
            _taxReceiptService = taxReceiptService;
            _paymentDetailsServiceService = paymentDetailsServiceService;
            _profileServiceApi = profileServiceApi;
        }


        public ActionResult SmithFamilyID()
        {
            Result<SmithFamilyIDModel> modelResult = _supporterPortalMvcService.CreateSmithFamilyIDModel();
            if (modelResult.IsFailure)
                return CreateErrorView("SupporterPortalMvcController.SmithFamilyID", modelResult.Error);

            return View(modelResult.Value);
        }

        public ActionResult SmithFamilyIDMobile()
        {
            Result<SmithFamilyIDModel> modelResult = _supporterPortalMvcService.CreateSmithFamilyIDModel();
            if (modelResult.IsFailure)
                return CreateErrorView("SupporterPortalMvcController.SmithFamilyIDMobile", modelResult.Error);

            return View(modelResult.Value);
        }

        public ActionResult WelcomeMessage()
        {
            Result<IWelcomeMessage> modelResult = _supporterPortalMvcService.CreateWelcomeMessageModel();
            if (modelResult.IsFailure)
                return CreateErrorView("SupporterPortalMvcController.WelcomeMessage", modelResult.Error);

            return View(modelResult.Value);
        }

        public ActionResult LatestDonationPledgeActivity()
        {
            Result<ILatestDonationPledgeActivity> modelResult = _supporterPortalMvcService.CreateLatestDonationPledgeActivityModel();
            if (modelResult.IsFailure)
                return CreateErrorView("SupporterPortalMvcController.LatestDonationPledgeActivity", modelResult.Error);

            return View(modelResult.Value);
        }

        public ActionResult DonationsByFYGraph()
        {
            Result<IDonationsByFYGraph> modelResult = _supporterPortalMvcService.CreateDonationsByFYGraphModel();
            if (modelResult.IsFailure)
                return CreateErrorView("SupporterPortalMvcController.DonationsByFYGraph", modelResult.Error);

            return View(modelResult.Value);
        }

        public ActionResult YourImpact()
        {
            Result<IYourImpact> modelResult = _supporterPortalMvcService.CreateYourImpactModel();
            if (modelResult.IsFailure)
                return CreateErrorView("SupporterPortalMvcController.YourImpact", modelResult.Error);

            return View(modelResult.Value);
        }

        public ActionResult Questionnaire()
        {
            Result<IQuestionnaire> modelResult = _supporterPortalMvcService.CreateQuestionnaireModel();
            if (modelResult.IsFailure)
                return CreateErrorView("SupporterPortalMvcController.Questionnaire ", modelResult.Error);

            return View(modelResult.Value);
        }

        public ActionResult Notifications()
        {
            var participantIdQueryString = HttpContext.Request.QueryString["participantId"];

            Guid.TryParse(participantIdQueryString, out Guid participantId);

            Result<IPortalNotification> modelResult = _notificationService.CreatePortalNotificationModel(participantId);
            if (modelResult.IsFailure)
                return CreateErrorView("SupporterPortalMvcController.Notifications", modelResult.Error);

            return View(modelResult.Value);
        }

        public ActionResult TaxReceipts()
        {
            Result<ITaxReceipts> modelResult = _taxReceiptService.CreateTaxReceiptsModel();
            if (modelResult.IsFailure)
                return CreateErrorView("SupporterPortalMvcController.TaxReceipts ", modelResult.Error);

            return View(modelResult.Value);
        }

        /// <summary>
        /// Initial load of Sponsored Students Intro module with Experience editor content
        /// </summary>
        /// <returns></returns>
        public ActionResult SponsoredStudents()
        {
            Result<ISponsoredStudents> modelResult = _supporterPortalMvcService.CreateSponsoredStudentsModel(Request.QueryString);
            if (modelResult.IsFailure)
                return CreateErrorView("SupporterPortalMvcController.SponsoredStudents", modelResult.Error);

            return View(modelResult.Value);
        }

        /// <summary>
        /// Initial load of Payment Details module with Sitecore content
        /// </summary>
        /// <returns></returns>
        public ActionResult PaymentDetails()
        {
            var modelResult = _paymentDetailsServiceService.CreatePaymentDetailsModel();
            if (modelResult.IsFailure)
                return CreateErrorView("SupporterPortalMvcController.PaymentDetails ", modelResult.Error);

            return View(modelResult.Value);
        }

        /// <summary>
        /// Sponsored Student Information module with Experience editor content
        /// </summary>
        /// <returns></returns>
        public ActionResult StudentInformation()
        {
            Result<IStudentInformation> modelResult = _supporterPortalMvcService.CreateStudentInformationModel();
            if (modelResult.IsFailure)
                return CreateErrorView("SupporterPortalMvcController.StudentInformation", modelResult.Error);

            return View(modelResult.Value);
        }

        /// <summary>
        /// My Profile Details Page Razor loading
        /// </summary>
        /// <returns></returns>
        public ActionResult ProfileDetails()
        {
            Result<IProfileDetails> modelResult = _profileServiceApi.CreateProfileDetailsModel();
            if (modelResult.IsFailure)
                return CreateErrorView("SupporterPortalMvcController.ProfileDetails", modelResult.Error);
            return View(modelResult.Value);
        }

        /// <summary>
        /// Initial load of SponsoredStudentQuote module with Experience editor content
        /// </summary>
        /// <returns></returns>
        public ActionResult SponsoredStudentQuote()
        {
            Result<ISponsoredStudentQuote> modelResult = _supporterPortalMvcService.CreateSponsoredStudentQuoteModel();
            if (modelResult.IsFailure)
                return CreateErrorView("SupporterPortalMvcController.SponsoredStudentQuote", modelResult.Error);

            return View(modelResult.Value);
        }

        /// <summary>
        /// Initial load of Sponsored Student Message Model module with Experience editor content
        /// </summary>
        /// <returns></returns>
        public ActionResult SponsoredStudentMessage()
        {
            var modelResult = _supporterPortalMvcService.CreateSponsoredStudentMessageModel();
            if (modelResult.IsFailure)
                return CreateErrorView("SupporterPortalMvcController.SponsoredStudentMessage", modelResult.Error);

            return View(modelResult.Value);
        }

        public ActionResult SponsoredStudentSelectMessage()
        {
            Result<SponsoredStudentSelectMessageDto> modelResult = _supporterPortalMvcService.SponsoredStudentSelectMessageModel();
            if (modelResult.IsFailure)
                return CreateErrorView("SupporterPortalMvcController.SponsoredStudentSelectMessage", modelResult.Error);

            return View(modelResult.Value);
        }

        /// <summary>
        /// File Gallery Module
        /// </summary>
        /// <returns></returns>
        public ActionResult StudentFileGallery()
        {
            var modelResult = _supporterPortalMvcService.CreateStudentFileGalleryModel();
            if (modelResult.IsFailure)
                return CreateErrorView("SupporterPortalMvcController.StudentFileGallery", modelResult.Error);
            return View(modelResult.Value);
        }
        /// <summary>
        /// Supporter portal session timeout alert message.
        /// </summary>
        /// <returns></returns>
        public ActionResult SupporterPortalSessionTimeout()
        {
            if (Session?.Timeout != null)
            {
                Result<ISupporterPortalSessionTimeout> modelResult = _supporterPortalMvcService.SessionTimeoutSettingsModel(Session.Timeout);

                if (modelResult.IsSuccess)
                    return View(modelResult.Value);
                else
                    return CreateErrorView("SupporterPortalMvcController.SupporterPortalSessionTimeout", modelResult.Error);
            }

            return CreateErrorView("SupporterPortalMvcController.SupporterPortalSessionTimeout", "Session timeout is null");
        }
    }
}