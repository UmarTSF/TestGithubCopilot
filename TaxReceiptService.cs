namespace TSF.Feature.SupporterPortalMvc.Services
{
    using AutoMapper;
    using CSharpFunctionalExtensions;
    using Sitecore.Security.Accounts;
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
    using TSF.Foundation.Portal.Extensions;
    using TSF.Foundation.Portal.Services;
    using TSF.Foundation.SupporterPortal.ApiClient;
    using TSF.Foundation.SupporterPortal.Models.Domain;
    using System.Web;
    using GemBox.Document;
    using TSF.Foundation.Core;
    using TSF.Foundation.Helpers.Helpers;
    using System.Text;
    using System.IO;
    using TSF.Foundation.Extensions;
    using TaxReceiptApi = Foundation.SupporterPortal.Models.Domain.TaxReceipt;
    using System.Collections;
    using TSF.Foundation.Email.Helpers;
    using Sitecore.Data.Items;
    using TSF.Foundation.CRM_D365.Enums;

    public class TaxReceiptService : ITaxReceiptService
    {
        private readonly IRenderingRepository _renderingRepository;
        private readonly ILogRepository _logRepository;
        private readonly IContentRepository _contentRepository;
        private readonly ISupporterPortalMvcRepository _supporterPortalMvcRepository;
        private readonly ISupporterCRMApiClient _supporterCRMApiClient;
        private readonly IUserService _userService;
        private readonly ISessionService _sessionService;

        public TaxReceiptService(IRenderingRepository renderingRepository,
            ILogRepository logRepository,
            IContentRepository contentRepository,
            ISupporterPortalMvcRepository supporterPortalMvcRepository,
            ISupporterCRMApiClient supporterCRMApiClient,
            IUserService userService,
            ISessionService sessionService)
        {
            _renderingRepository = renderingRepository;
            _logRepository = logRepository;
            _contentRepository = contentRepository;
            _supporterPortalMvcRepository = supporterPortalMvcRepository;
            _supporterCRMApiClient = supporterCRMApiClient;
            _userService = userService;
            _sessionService = sessionService;
        }

        #region Public Methods
        public Result<ITaxReceipts> CreateTaxReceiptsModel()
        {
            ITaxReceipts modelDatasource = _renderingRepository.GetDataSourceItem<ITaxReceipts>();
            if(modelDatasource == null)
            {
                _logRepository.Warn("Datasource is null TaxReceiptService CreateTaxReceiptsModel");
                return Result.Failure<ITaxReceipts>("CreateTaxReceiptsModel : Datasource is null");
            }

            return Result.Success(modelDatasource);
        }

        public async Task<Result<TaxReceiptsApiModel>> CreateTaxReceiptsApiModel(Guid dataSourceID)
        {
            ITaxReceipts modelDatasource = _contentRepository.GetItem<ITaxReceipts>(dataSourceID, Sitecore.Context.Database);
            if(modelDatasource == null)
            {
                _logRepository.Warn("Datasource is null TaxReceiptService CreateTaxReceiptsApiModel");
                return Result.Failure<TaxReceiptsApiModel>("CreateTaxReceiptsApiModel : Datasource is null");
            }

            var loginUserInfo = _sessionService.GetLoginUserInfo();
            if (loginUserInfo == null)
            {
                _logRepository.Warn("GetTaxReceiptsByFinancialYear loginUserInfo is null TaxReceiptService CreateTaxReceiptsApiModel");
                return Result.Failure<TaxReceiptsApiModel>("CreateTaxReceiptsApiModel : loginUserInfo is null");
            }

            var DonationCategories = _supporterPortalMvcRepository.GetListItems(modelDatasource.PrimaryDonationCategoryFilter.SitecoreItem.ParentID.Guid);

            List<TaxReceiptsByFY> taxReceiptsByFY = await GetTaxReceipts(loginUserInfo.CustomerEntityGuid, loginUserInfo.CustomerType, modelDatasource.NumberofFYs);
            if(taxReceiptsByFY == null)
            {
                _logRepository.Warn("GetTaxReceiptsByFinancialYear API result is null TaxReceiptService CreateTaxReceiptsApiModel");
                return Result.Failure<TaxReceiptsApiModel>("CreateTaxReceiptsApiModel : API result is null");
            }
            var taxReceipts = new List<Models.Dto.TaxReceipt>();
            bool NoReceipts = true;
            foreach(var itemFY in taxReceiptsByFY)
            {
                foreach(var receipt in itemFY.DonationTaxReceipts)
                {
                    NoReceipts = false; //Because there's at least one receipt

                    Models.Dto.TaxReceipt taxReceipt = Mapper.Map<Foundation.SupporterPortal.Models.Domain.TaxReceipt, Models.Dto.TaxReceipt>(receipt);
                    taxReceipt.DonationDetails = GetDonationDetails(modelDatasource, receipt);
                    taxReceipt.Category = GetDonationCategory(DonationCategories, receipt.DonationCategory);
                    taxReceipt.FinancialYear = string.Format("FY {0}/{1}", itemFY.FinancialYearStartDate.Value.ToString("yyyy"), itemFY.FinancialYearEndDate.Value.ToString("yy"));
                    taxReceipt.ReceiptMessage = GetReceiptMessage(modelDatasource, receipt);
                    if(string.IsNullOrEmpty(taxReceipt.ReceiptMessage))
                    {
                        var tokenDictionary = new Dictionary<string, string>
                        {
                            {Constants.Tokens.ReceiptNumber, receipt.DonationReceiptNumber}
                        };
                        taxReceipt.ReceiptLink = modelDatasource.PdfCta.ToLinkModel(tokenDictionary);
                    }

                    taxReceipts.Add(taxReceipt);
                }
            }
            TaxReceiptsApiModel taxReceiptsModel = new TaxReceiptsApiModel();
            if(NoReceipts)
            {
                taxReceiptsModel.NoReceipts = Mapper.Map<ITaxReceipts, NoTaxReceipt>(modelDatasource);
            }
            else
            {
                taxReceiptsModel = Mapper.Map<ITaxReceipts, TaxReceiptsApiModel>(modelDatasource);
                taxReceiptsModel.Receipts = taxReceipts.OrderByDescending(x => DateTime.Parse(x.Date)).ToList();
            }
            return Result.Success(taxReceiptsModel);
        }

        public async Task<List<TaxReceiptsByFY>> GetTaxReceipts(Guid customerEntityGuid, SupporterClass supporterClass, int numberOfYears, bool forceRefresh = false)
        {
            List<TaxReceiptsByFY> taxReceiptsByFY;
            if(HttpContext.Current.Session?[Constants.SessionKeys.TaxReceipts] != null && !forceRefresh)
            {
                taxReceiptsByFY = HttpContext.Current.Session?[Constants.SessionKeys.TaxReceipts] as List<TaxReceiptsByFY>;
            }
            else
            {
                taxReceiptsByFY = await _supporterCRMApiClient.GetTaxReceiptsByFY(supporterClass, customerEntityGuid, numberOfYears);
                if(taxReceiptsByFY != null)
                    HttpContext.Current.Session.Add(Constants.SessionKeys.TaxReceipts, taxReceiptsByFY);//Add taxReceipts to Session for receipt download page
            }

            return taxReceiptsByFY;
        }

        /// <summary>
        /// Generate Tax Receipt PDF
        /// </summary>
        /// <param name="taxReceiptNumber"></param>
        /// <returns></returns>
        public Result<DocumentModel> CreateTaxReceiptPDFDocument(string receiptNumber)
        {
            // Get Tax Receipt from Session
            var taxReceipt = GetTaxReceiptByNumber(receiptNumber);
            if (taxReceipt == null)
            {
                _logRepository.Warn(string.Format("TaxReceipt is null TaxReceiptService CreateTaxReceiptPDFDocument. taxReceiptNumber={0}", receiptNumber));
                return Result.Failure<DocumentModel>(Constants.TaxReceipts.TaxReceiptNotExistInSession);
            }

            // Generate Tax Receipt PDF
            var pdfDocument = GenerateTaxReceiptPDFDocumentModel(taxReceipt);
            if (pdfDocument == null)
            {
                _logRepository.Warn(string.Format("pdfDocument is null TaxReceiptService CreateTaxReceiptPDFDocument. taxReceiptNumber={0}", receiptNumber));
                return Result.Failure<DocumentModel>("CreateTaxReceiptPDFDocument : pdfDocument is null");
            }

            return Result.Success(pdfDocument);
        }

        /// <summary>
        /// Send Tax Receipt Email with a PDF attachment.
        /// </summary>
        /// <param name="taxReceiptNumber"></param>
        /// <returns></returns>
        public Result<DocumentModel> SendTaxReceiptEmail(string taxReceiptNumber)
        {
            // Get Tax Receipt from Session
            var taxReceipt = GetTaxReceiptByNumber(taxReceiptNumber);
            if (taxReceipt == null)
            {
                _logRepository.Warn("taxReceipt is null TaxReceiptService SendTaxReceiptEmail");
                return Result.Failure<DocumentModel>("SendTaxReceiptEmail : taxReceipt is null");
            }

            string templateId = string.Empty;
            switch(taxReceipt.DonationCategory)
            {
                case DonationCategory.Sponsorship:
                    templateId = CoreConstants.EmailTemplateIDs.TaxReceiptEmailSponsorship.Guid.ToString();
                    break;
                case DonationCategory.RegularDonation:
                    templateId = CoreConstants.EmailTemplateIDs.TaxReceiptEmailRegularDonation.Guid.ToString();
                    break;
                case DonationCategory.SponsorshipAndRegular:
                    templateId = CoreConstants.EmailTemplateIDs.TaxReceiptEmailSponsorshipAndRegular.Guid.ToString();
                    break;
                case DonationCategory.OneOffDonation:
                    templateId = CoreConstants.EmailTemplateIDs.TaxReceiptEmailOneOffDonation.Guid.ToString();
                    break;
            }
            if (string.IsNullOrEmpty(templateId))
            {
                _logRepository.Warn("TaxReceipt.DonationCategory is null TaxReceiptService.SendTaxReceiptEmail method.");
                return Result.Failure<DocumentModel>("SendTaxReceiptEmail : TaxReceipt.DonationCategory is null");
            }

            var emailTemplate = _contentRepository.GetItem<Item>(templateId, Sitecore.Context.Database.Name);
            if (emailTemplate != null)
            {
                // Generate Tax Receipt PDF
                var pdfDocument = GenerateTaxReceiptPDFDocumentModel(taxReceipt);

                if (pdfDocument != null)
                {
                    using (var ms = new MemoryStream())
                    {
                        pdfDocument.Save(ms, GemBox.Document.SaveOptions.PdfDefault);

                        // Send an email with an attachment
                        if (SendReceiptEmail(ms, emailTemplate, taxReceipt))
                        {
                            return Result.Success(pdfDocument);
                        }
                    }
                }
            }

            return Result.Failure<DocumentModel>("SendTaxReceiptEmail : TaxReceipt.DonationCategory is null");
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Get the Receipt message
        /// </summary>
        /// <param name="modelDatasource"></param>
        /// <param name="receipt"></param>
        /// <returns></returns>
        private string GetReceiptMessage(ITaxReceipts modelDatasource, Foundation.SupporterPortal.Models.Domain.TaxReceipt receipt)
        {
            string NoReceiptMessage = string.Empty;
            if(!receipt.DonationIsTaxDeductible)
            {
                NoReceiptMessage = modelDatasource.ReceiptMessageNonTaxDeductible;
            }
            else if(string.IsNullOrEmpty(receipt.DonationReceiptNumber))
            {
                NoReceiptMessage = modelDatasource.ReceiptMessageProcessing;
            }
            else if(receipt.DonationDate > DateTime.Today && 
                    receipt.DonationFrequency != PledgeFrequency.Yearly && 
                    receipt.DonationCategory.IsIn(DonationCategory.Sponsorship,DonationCategory.RegularDonation,DonationCategory.SponsorshipAndRegular) && 
                    !receipt.DonationPledgeEnded)
            {
                NoReceiptMessage = modelDatasource.ReceiptMessageAnnual;
            }
            return NoReceiptMessage;
        }

        /// <summary>
        /// Get the donation details text
        /// </summary>
        /// <param name="modelDatasource"></param>
        /// <param name="receipt"></param>
        /// <returns></returns>
        private string GetDonationDetails(ITaxReceipts modelDatasource, Foundation.SupporterPortal.Models.Domain.TaxReceipt receipt)
        {
            string donationDetails = string.Empty;
            var tokenDictionary = new Dictionary<string, string>();
            switch(receipt.DonationCategory)
            {
                case DonationCategory.Sponsorship:
                    tokenDictionary = new Dictionary<string, string>
                        {
                            {Constants.Tokens.PledgeFrequency, receipt.DonationFrequency.StringValue()}
                        };
                    donationDetails = modelDatasource.DetailsSponsorship.Replace(tokenDictionary);
                    break;
                case DonationCategory.RegularDonation:
                    tokenDictionary = new Dictionary<string, string>
                        {
                            {Constants.Tokens.PledgeFrequency, receipt.DonationFrequency.StringValue()}
                        };
                    donationDetails = modelDatasource.DetailsRegularDonation.Replace(tokenDictionary);
                    break;
                case DonationCategory.SponsorshipAndRegular:
                    tokenDictionary = new Dictionary<string, string>
                        {
                            {Constants.Tokens.PledgeFrequency, receipt.DonationFrequency.StringValue()}
                        };
                    donationDetails = modelDatasource.DetailsSponsorshipAndRegularDonation.Replace(tokenDictionary);
                    break;
                case DonationCategory.OneOffDonation:
                    if(receipt.DonationIsTaxDeductible)
                    {
                        donationDetails = modelDatasource.DetailsOneOffDonationTaxDeductible;
                    }
                    else
                    {
                        donationDetails = receipt.GlAccountName;
                    }
                    break;
                default://if the 'Donation Category' value is 'null'
                    donationDetails = receipt.GlAccountName;
                    break;
            }
            return donationDetails;
        }

        /// <summary>
        /// Get the donation category text
        /// </summary>
        /// <param name="donationCategories"></param>
        /// <param name="donationCategory"></param>
        /// <returns></returns>
        private string GetDonationCategory(List<IListItem> donationCategories, DonationCategory donationCategory)
        {
            return donationCategories
                .Find(x => x.Value == ((int)donationCategory).ToString())?
                .Name_Field;
        }

        /// <summary>
        /// Generate Tax Receipt PDF
        /// </summary>
        /// <param name="taxReceipt"></param>
        /// <returns></returns>
        private DocumentModel GenerateTaxReceiptPDFDocumentModel(TaxReceiptApi taxReceipt)
        {
            string pdfTemplateId = string.Empty;
            switch (taxReceipt.DonationCategory)
            {
                case DonationCategory.Sponsorship:
                    pdfTemplateId = CoreConstants.HtmlTemplateIDs.TaxReceiptSponsorship.Guid.ToString();
                    break;
                case DonationCategory.RegularDonation:
                    pdfTemplateId = CoreConstants.HtmlTemplateIDs.TaxReceiptRegularDonation.Guid.ToString();
                    break;
                case DonationCategory.SponsorshipAndRegular:
                    pdfTemplateId = CoreConstants.HtmlTemplateIDs.TaxReceiptSponsorshipAndRegular.Guid.ToString();
                    break;
                case DonationCategory.OneOffDonation:
                    pdfTemplateId = CoreConstants.HtmlTemplateIDs.TaxReceiptOneOffDonation.Guid.ToString();
                    break;
            }

            // Get HTML Template
            var pdfTemplate = _contentRepository.GetItem<IHTMLTemplate>(pdfTemplateId, Sitecore.Context.Database.Name);

            // Prepare token replace dictionary 
            var tokenDictionary = GetTaxReceiptPDFTokenDictionary(taxReceipt);

            // Replace tokens in the HTML Body
            var sbContent = pdfTemplate?.HTMLBody?.Replace(tokenDictionary);

            // Create PDF Document with HTML Body
            using (var htmlStream = new MemoryStream(Encoding.UTF8.GetBytes(sbContent)))
            {
                ComponentInfo.SetLicense(CoreConstants.SerialKeys.GemBox);
                var document = DocumentModel.Load(htmlStream, LoadOptions.HtmlDefault);

                return document;
            }
        }

        /// <summary>
        /// Get Tax Receipt from Session
        /// </summary>
        /// <param name="taxReceiptNumber"></param>
        /// <returns></returns>
        private TaxReceiptApi GetTaxReceiptByNumber(string taxReceiptNumber)
        {
            TaxReceiptApi taxReceipt = null;

            var taxReceipts = HttpContext.Current?.Session?[Constants.SessionKeys.TaxReceipts] as List<TaxReceiptsByFY>;

            if (taxReceipts != null)
            {
                taxReceipt = taxReceipts?.SelectMany(f => f.DonationTaxReceipts).FirstOrDefault(f => !string.IsNullOrEmpty(f.DonationReceiptNumber) && f.DonationReceiptNumber.Equals(taxReceiptNumber));
            }

            if (taxReceipt == null)
                _logRepository.Warn(string.Format("TaxReceipt is not exist in the session. TaxReceiptService GetTaxReceiptByNumber. taxReceiptNumber={0}", taxReceiptNumber));

            return taxReceipt;
        }

        /// <summary>
        /// Create a token replace dictionary 
        /// </summary>
        /// <param name="taxReceipt"></param>
        /// <returns></returns>
        private Dictionary<string, string> GetTaxReceiptPDFTokenDictionary(TaxReceiptApi taxReceipt)
        {
            var smithFamilyID = HttpContext.Current.Session?[CoreConstants.SessionKeys.TsfID]?.ToString();
            var name = _userService.GetUserFirstName();

            var donationDate = string.Empty;
            if (taxReceipt.DonationCategory.IsIn(DonationCategory.Sponsorship,DonationCategory.RegularDonation,DonationCategory.SponsorshipAndRegular) &&
                taxReceipt.DonationFrequency != PledgeFrequency.Yearly)
            {
                donationDate = string.Format("{0:dd/MM/yyyy} - {1:dd/MM/yyyy}", taxReceipt.DonationDate.ToFirstDayOfFinancialYear(), taxReceipt.DonationDate.ToLastDayOfFinancialYear());
            }
            else
            {
                donationDate = taxReceipt.DonationDate.ToString("dd/MM/yyyy");
            }

            var tokenDictionary = new Dictionary<string, string>() {
                {Constants.Tokens.SmithFamilyId, smithFamilyID},
                {Constants.Tokens.Name, name},
                {Constants.Tokens.NameOnReceipt, taxReceipt.DonationReceiptName},
                {Constants.Tokens.ReceiptNumber, taxReceipt.DonationReceiptNumber },
                {Constants.Tokens.DonationDate, donationDate},
                {Constants.Tokens.DonationDetail, taxReceipt.GlAccountName},
                {Constants.Tokens.SponsorshipAmount, taxReceipt.DonationAmountSponsorship.ToString("0.00")},
                {Constants.Tokens.RegularDonationAmount, taxReceipt.DonationAmountRegularDonation.ToString("0.00")},
                {Constants.Tokens.DonationAmount, taxReceipt.DonationAmount.ToString("0.00")}
            };

            return tokenDictionary;
        }

        /// <summary>
        /// Send an Email
        /// </summary>
        /// <param name="receipt"></param>
        /// <param name="emailTemplate"></param>
        /// <param name="taxReceipt"></param>
        /// <returns></returns>
        private bool SendReceiptEmail(MemoryStream receipt, Item emailTemplate, TaxReceiptApi taxReceipt)
        {
            var name = _userService.GetUserFirstName();
            var email = GetTaxReceiptToEmail();

            if (email == null)
            {
                _logRepository.Warn(string.Format("TaxReceipt is not exist in the session. TaxReceiptService GetTaxReceiptToEmail. taxReceiptNumber={0};", taxReceipt.DonationReceiptNumber));
                return false;
            }

            _logRepository.Info(string.Format("SendReceiptEmail - username = {0}; email id = {1}", name, email));

            var bodyReplaces = new Hashtable
                {
                    {Constants.Tokens.Name, name},
                    {Constants.Tokens.Email, email}
                };

            var taxReceiptPDFName = string.Format(Constants.TaxReceipts.TaxReceiptPDFNameFormat, taxReceipt.DonationReceiptNumber);
            var attachments = new Dictionary<string, Stream> { { taxReceiptPDFName, receipt } };

            return EmailHelper.SendEmail(emailTemplate, bodyReplaces, email, true, attachments);

        }

        /// <summary>
        /// Get To address email id
        /// </summary>
        /// <returns></returns>
        private string GetTaxReceiptToEmail()
        {
            string email = string.Empty;

            var taxReceipts = HttpContext.Current?.Session?[Constants.SessionKeys.TaxReceipts] as List<TaxReceiptsByFY>;
            if (taxReceipts != null)
            {
                email = taxReceipts?.FirstOrDefault()?.SupporterEmailAddress;
            }

            return email;
        }
        #endregion
    }
}