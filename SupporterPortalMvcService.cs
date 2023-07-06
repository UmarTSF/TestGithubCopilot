namespace TSF.Feature.SupporterPortalMvc.Services
{
    using System;
    using System.Web;
    using TSF.Feature.SupporterPortalMvc.ViewModels;
    using TSF.Feature.SupporterPortalMvc.Models;
    using TSF.Foundation.Content.Repositories;
    using TSF.Foundation.Logging.Repositories;
    using TSF.Foundation.Portal;
    using TSF.Foundation.Portal.Extensions;
    using TSF.Foundation.Portal.Services;
    using System.Linq;
    using System.Collections.Generic;
    using TSF.Foundation.Core;
    using TSF.Foundation.Content.Models;
    using Sitecore.Security.Accounts;
    using TSF.Foundation.Extensions;
    using CSharpFunctionalExtensions;
    using AutoMapper;
    using TSF.Feature.SupporterPortalMvc.Models.Domain;
    using TSF.Foundation.Dictionary.Repositories;
    using TSF.Foundation.CRM.Types;
    using TSF.Foundation.Portal.Models.Profile;
    using TSF.Foundation.Helpers.Helpers;
    using TSF.Feature.SupporterPortalMvc.Models.Dto;
    using static TSF.Feature.SupporterPortalMvc.Constants;
    using TSF.Foundation.Extensions.Extensions;
    using Glass.Mapper.Sc.Fields;
    using System.Collections.Specialized;
    using TSF.Foundation.ORM.Models;
    using TSF.Feature.SupporterPortalMvc.Repositories;
    using System.Globalization;
    using TSF.Foundation.Portal.Services.Azure;
    using TSF.Foundation.Core.Helpers;

    public class SupporterPortalMvcService : ISupporterPortalMvcService
    {
        private readonly string db = Sitecore.Configuration.Settings.GetSetting("db");
        private readonly ILogRepository _logRepository;
        private readonly IContentRepository _contentRepository;
        private readonly IRenderingRepository _renderingRepository;
        private readonly ICombinedUserService _combinedUserService;
        private readonly ISupporterService _supporterService;
        private readonly IUserService _userService;
        private readonly ISessionService _sessionService;
        private readonly ISupporterPortalMvcRepository _portalMvcRepository;


        private readonly IAzureStorageFileService _azureStorageFileService;
        public SupporterPortalMvcService(ILogRepository logRepository,
            IContentRepository contentRepository,
            IRenderingRepository renderingRepository,
            ICombinedUserService combinedUserService,
            ISupporterService supporterService,
            IUserService userService,
            ISessionService sessionService,
            IAzureStorageFileService azureStorageFileService,
            ISupporterPortalMvcRepository portalMvcRepository)
        {
            _logRepository = logRepository;
            _contentRepository = contentRepository;
            _renderingRepository = renderingRepository;
            _combinedUserService = combinedUserService;
            _supporterService = supporterService;
            _userService = userService;
            _sessionService = sessionService;
            _azureStorageFileService = azureStorageFileService;
            _portalMvcRepository = portalMvcRepository;
        }

        public SPortalNavigationViewModel GetPortalNavigation()
        {
            try
            {
                _combinedUserService.SetCombinedUserContextIdinSession(CoreConstants.IDs.PortalDashboardPage);

                var dataSource = _contentRepository.GetItem<ISPortalNavigationModel>(CoreConstants.IDs.PortalDashboardPage.ToString(), Sitecore.Context.Database.Name);
                if (dataSource == null)
                {
                    return null;
                }

                SPortalNavigationViewModel viewModel = ConvertToViewModel(dataSource);

                if (Sitecore.Context.User.IsFamily() && Sitecore.Context.User.IsSupporter())
                {
                    viewModel.AccountsLinked = true;
                    var familyPortalHomepage = _contentRepository.GetItem<ISPortalNavigationModel>(PortalConstants.FamilyPortalHomePage.ID.ToString(), Sitecore.Context.Database.Name);

                    var switchLinkItem = ConvertToNavItem(familyPortalHomepage);
                    switchLinkItem.Title = !string.IsNullOrEmpty(familyPortalHomepage.SwitchLinkText) ? familyPortalHomepage.SwitchLinkText : switchLinkItem.Title;

                    viewModel.LinkedAccount = switchLinkItem;
                }
                return viewModel;
            }
            catch (Exception ex)
            {
                _logRepository.Error("TSF.Feature.SupporterPortalMvc.Services.GetPortalNavigation", ex);
                return null;
            }
        }

        public Result<SmithFamilyIDModel> CreateSmithFamilyIDModel()
        {
            SmithFamilyIDModel model = new SmithFamilyIDModel();
            string _tsfID = _supporterService.GetCurrentSupporterTsfId();
            if (string.IsNullOrEmpty(_tsfID))
            {
                _logRepository.Warn("Smith Family ID is null or empty in SupporterPortalMvcService.CreateSmithFamilyIDModel");
                return Result.Failure<SmithFamilyIDModel>("Smith Family ID is null or empty");
            }
            model.SmithFamilyIDLabel = DictionaryPhraseRepository.Current.Get(Constants.DictionaryItems.SmithFamilyID, Constants.DictionaryItems.SmithFamilyID.Substring(Constants.DictionaryItems.SmithFamilyID.LastIndexOf("/") + 1));
            model.TsfID = _tsfID;
            return Result.Success(model);
        }

        public Result<IWelcomeMessage> CreateWelcomeMessageModel()
        {
            IWelcomeMessage modelDatasource = _renderingRepository.GetDataSourceItem<IWelcomeMessage>();

            if (modelDatasource == null)
            {
                _logRepository.Warn("Datasource is null SupporterPortalMvcService CreateWelcomeMessageModel");
                return Result.Failure<IWelcomeMessage>("WelcomeMessage : Datasource is null");
            }

            var tokenDictionary = new Dictionary<string, string>
                {
                    {Constants.Tokens.Name, GetFirstName()}
                };

            modelDatasource.ModuleHeading_TokenReplaced = modelDatasource.ModuleHeading.Replace(tokenDictionary);
            return Result.Success(modelDatasource);
        }

        public Result<IYourImpact> CreateYourImpactModel()
        {
            IYourImpact modelDatasource = _renderingRepository.GetDataSourceItem<IYourImpact>();
            if (modelDatasource == null)
            {
                _logRepository.Warn("Datasource is null SupporterPortalMvcService CreateYourImpactModel");
                return Result.Failure<IYourImpact>("YourImpact : Datasource is null");
            }

            var tokenDictionary = new Dictionary<string, string>
                {
                    {CoreConstants.Tokens.SupportPeriod, GetSupportPeriodText()},
                    {CoreConstants.Tokens.TotalSponsoredChildren, GetTotalSponsoredChildren()}
                };

            modelDatasource.Text_TokenReplaced = modelDatasource.Text.Replace(tokenDictionary);

            var renderingParams = _renderingRepository.GetRenderingParameters<IHideModuleHeadingParam>();
            if (renderingParams == null)
            {
                _logRepository.Warn($"Rendering Parameters are null SupporterPortalMvcService CreateYourImpactModel");
            }
            else
            {
                modelDatasource.HideModuleHeading = renderingParams.HideModuleHeading;
            }
            return Result.Success(modelDatasource);
        }

        public Result<ILatestDonationPledgeActivity> CreateLatestDonationPledgeActivityModel()
        {
            ILatestDonationPledgeActivity modelDatasource = _renderingRepository.GetDataSourceItem<ILatestDonationPledgeActivity>();
            if (modelDatasource == null)
            {
                _logRepository.Warn("Datasource is null SupporterPortalMvcService CreateLatestDonationPledgeActivityModel");
                return Result.Failure<ILatestDonationPledgeActivity>("CreateLatestDonationPledgeActivityModel : Datasource is null");
            }

            var renderingParams = _renderingRepository.GetRenderingParameters<IHideModuleHeadingParam>();
            if (renderingParams == null)
            {
                _logRepository.Warn($"Rendering Parameters are null SupporterPortalMvcService CreateLatestDonationPledgeActivityModel");
            }
            else
            {
                modelDatasource.HideModuleHeading = renderingParams.HideModuleHeading;
            }
            return Result.Success(modelDatasource);
        }

        public Result<IDonationsByFYGraph> CreateDonationsByFYGraphModel()
        {
            IDonationsByFYGraph modelDatasource = _renderingRepository.GetDataSourceItem<IDonationsByFYGraph>();
            if (modelDatasource == null)
            {
                _logRepository.Warn("Datasource is null SupporterPortalMvcService CreateDonationsByFYGraphModel");
                return Result.Failure<IDonationsByFYGraph>("CreateDonationsByFYGraphModel : Datasource is null");
            }

            var renderingParams = _renderingRepository.GetRenderingParameters<IHideModuleHeadingParam>();
            if (renderingParams == null)
            {
                _logRepository.Warn($"Rendering Parameters are null SupporterPortalMvcService CreateDonationsByFYGraphModel");
            }
            else
            {
                modelDatasource.HideModuleHeading = renderingParams.HideModuleHeading;
            }
            return Result.Success(modelDatasource);
        }

        public Result<IQuestionnaire> CreateQuestionnaireModel()
        {
            var loginUserInfo = _sessionService.GetLoginUserInfo();
            if (loginUserInfo != null)
            {
                AnalyticsHelper.IdentifyUser(new Foundation.Core.Models.AnalyticsInputObject { CrmContactId = loginUserInfo.CustomerEntityGuid });
            }

            IQuestionnaire modelDatasource = _renderingRepository.GetDataSourceItem<IQuestionnaire>();
            if (modelDatasource == null)
            {
                _logRepository.Warn("Datasource is null SupporterPortalMvcService CreateQuestionnaireModel");
                return Result.Failure<IQuestionnaire>("CreateQuestionnaireModel : Datasource is null");
            }

            var renderingParams = _renderingRepository.GetRenderingParameters<IHideModuleHeadingParam>();
            if (renderingParams == null)
            {
                _logRepository.Warn($"Rendering Parameters are null SupporterPortalMvcService CreateQuestionnaireModel");
            }
            else
            {
                modelDatasource.HideModuleHeading = renderingParams.HideModuleHeading;
            }

            if (modelDatasource.QuestionType == QuestionType.SingleOptionList || modelDatasource.QuestionType == QuestionType.MultiOptionList)
            {
                modelDatasource.QuestionOptions = modelDatasource.ListQuestionOptions.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            }

            if (string.IsNullOrEmpty(modelDatasource.SubmitButtonStyle))
            {
                modelDatasource.SubmitButtonStyle = CoreConstants.CssClases.DefaultButtonPrimaryStyle;
            }

            var tokenDictionary = new Dictionary<string, string>
                {
                    {Constants.Tokens.Name, GetFirstName()}
                };

            modelDatasource.ThankYouHeading_TokenReplaced = modelDatasource.ThankYouHeading.Replace(tokenDictionary);

            return Result.Success(modelDatasource);
        }

        /// <summary>
        /// Create a sponsored students initial model for experience editor and front-end Razor input parameters.
        /// </summary>
        /// <returns></returns>
        public Result<ISponsoredStudents> CreateSponsoredStudentsModel(NameValueCollection queryStringsCollection)
        {
            ISponsoredStudents modelDatasource = _renderingRepository.GetDataSourceItem<ISponsoredStudents>();
            if (modelDatasource == null)
            {
                _logRepository.Warn("Datasource is null SupporterPortalMvcService CreateSponsoredStudentsModel");
                return Result.Failure<ISponsoredStudents>("CreateSponsoredStudentsModel : Datasource is null");
            }

            var renderingParams = _renderingRepository.GetRenderingParameters<ISponsoredStudentsParameters>();
            if (renderingParams == null)
            {
                _logRepository.Warn($"Rendering Parameters are null SupporterPortalMvcService CreateSponsoredStudentsModel");
            }
            else
            {
                modelDatasource.HideModuleHeading = renderingParams.HideModuleHeading;
                modelDatasource.ShowViewAllCTA = renderingParams.ShowViewAllCTA;
                modelDatasource.ShowPagination = renderingParams.ShowPagination;
                modelDatasource.ShowSorting = renderingParams.ShowSorting;
                modelDatasource.ShowBadges = renderingParams.ShowBadges;
            }
            modelDatasource.SingleStudentSummary = IsSingleStudentTile(queryStringsCollection);
            return Result.Success(modelDatasource);
        }

        /// <summary>
        /// Create a sponsored students initial model for experience editor and front-end Razor input parameters.
        /// </summary>
        /// <returns></returns>
        public Result<ISponsoredStudentQuote> CreateSponsoredStudentQuoteModel()
        {
            ISponsoredStudentQuote modelDatasource = _renderingRepository.GetDataSourceItem<ISponsoredStudentQuote>();
            if (modelDatasource == null)
            {
                _logRepository.Warn("Datasource is null SupporterPortalMvcService CreateSponsoredStudentQuoteModel");
                return Result.Failure<ISponsoredStudentQuote>("CreateSponsoredStudentQuoteModel : Datasource is null");
            }

            var renderingParams = _renderingRepository.GetRenderingParameters<IHideModuleHeadingParam>();
            if (renderingParams == null)
            {
                _logRepository.Warn($"Rendering Parameters are null SupporterPortalMvcService CreateSponsoredStudentQuoteModel");
            }
            else
            {
                modelDatasource.HideModuleHeading = renderingParams.HideModuleHeading;
            }
            if (Sitecore.Context.PageMode.IsNormal)
            {
                PopulateSponsoredStudentQuote(modelDatasource);
            }

            return Result.Success(modelDatasource);
        }

        /// <summary>
        /// Get Profile details from content datasource
        /// </summary>
        /// <returns></returns>
        public Result<IProfileDetails> CreateProfileDetailsModel()
        {
            // Get Form details
            IProfileDetails modelDatasource = _renderingRepository.GetDataSourceItem<IProfileDetails>();
            if (modelDatasource == null)
            {
                _logRepository.Warn("Datasource is null SupporterPortalMvcService CreateProfileDetailsModel");
                return Result.Failure<IProfileDetails>("CreateProfileDetailsModel : Datasource is null");
            }

            // Get Form Sections details
            var tab1IndividualFormSection = _contentRepository.GetItem<IProfileFormSection>(modelDatasource?.IndividualSupporter?.Guid.ToString(), db);
            var tab1CoporateFormSection = _contentRepository.GetItem<IProfileFormSection>(modelDatasource?.CorporateSupporter?.Guid.ToString(), db);
            var tab2ProfileFormSection = _contentRepository.GetItem<IProfileFormSection>(Constants.ProfileDetails.Item.Tab2ChangePassword.Guid.ToString(), db);
            var tsfID = _supporterService.GetCurrentSupporterTsfId();

            if (tab1IndividualFormSection != null)
            {
                modelDatasource.Tab1IndividualFormSection = tab1IndividualFormSection;
            }
            if (tab1CoporateFormSection != null)
            {
                modelDatasource.Tab1CorporateFormSection = tab1CoporateFormSection;
            }
            if (tab2ProfileFormSection != null)
            {
                modelDatasource.Tab2ChangePasswordFormSection = tab2ProfileFormSection;
            }
            if (!string.IsNullOrEmpty(tsfID))
            {
                modelDatasource.TsfID = tsfID;
            }

            // Set current CustomerType
            var loginUserInfo = _sessionService.GetLoginUserInfo();
            modelDatasource.SupporterClass = loginUserInfo.CustomerType;


            return Result.Success(modelDatasource);
        }

        /// <summary>
        /// Create Student Information model for experience editor and front-end Razor input parameters.
        /// </summary>
        /// <returns></returns>
        public Result<IStudentInformation> CreateStudentInformationModel()
        {
            IStudentInformation modelDatasource = null;
            var studentDto = GetSponsoredStudentDto();
            if (studentDto != null
                && (studentDto.ParticipantReplacement || studentDto.ParticipantUpgrade))
            {
                modelDatasource = _renderingRepository.GetDataSourceItem<IStudentInformation>();
                if (modelDatasource == null)
                {
                    _logRepository.Warn("Datasource is null SupporterPortalMvcService CreateStudentInformationModel");
                    return Result.Failure<IStudentInformation>("CreateStudentInformationModel : Datasource is null");
                }

                var renderingParams = _renderingRepository.GetRenderingParameters<IHideModuleHeadingParam>();
                if (renderingParams == null)
                {
                    _logRepository.Warn($"Rendering Parameters are null SupporterPortalMvcService CreateStudentInformationModel");
                }
                else
                {
                    modelDatasource.HideModuleHeading = renderingParams.HideModuleHeading;
                }

                if (studentDto.ParticipantUpgrade)
                {
                    var tokenDictionary = new Dictionary<string, string>() {
                        {CoreConstants.Tokens.ParticipantFirstName, studentDto.ParticipantFirstName }
                    };
                    modelDatasource.ModuleTextHeading = modelDatasource.ParticipantUpgradeHeading.Replace(tokenDictionary);
                    modelDatasource.ModuleText = modelDatasource.ParticipantUpgradeText;
                }
                if (studentDto.ParticipantReplacement)
                {
                    var tokenDictionary = new Dictionary<string, string>() {
                        {CoreConstants.Tokens.ReplacedParticipantFirstName, studentDto.ParticipantReplacementFirstName }
                    };
                    modelDatasource.ModuleTextHeading = modelDatasource.ParticipantReplacementHeading.Replace(tokenDictionary);

                    tokenDictionary = new Dictionary<string, string>() {
                        {CoreConstants.Tokens.ReplacedParticipantFirstName, studentDto.ParticipantReplacementFirstName },
                        {CoreConstants.Tokens.ParticipantReplacementReasonText, studentDto.ParticipantReplacementReasonText }
                    };
                    modelDatasource.ModuleText = modelDatasource.ParticipantReplacementText.Replace(tokenDictionary);
                }
            }

            return Result.Success(modelDatasource);
        }

        /// <summary>
        /// Create Sponsored Student Message Model
        /// </summary>
        /// <returns></returns>
        public Result<ISponsoredStudentsMessagesModel> CreateSponsoredStudentMessageModel()
        {
            ISponsoredStudentsMessagesModel modelDatasource = _renderingRepository.GetDataSourceItem<ISponsoredStudentsMessagesModel>();
            if (modelDatasource == null)
            {
                _logRepository.Warn("Datasource is null SupporterPortalMvcService CreateSponsoredStudentMessageModel");
                return Result.Failure<ISponsoredStudentsMessagesModel>("CreateSponsoredStudentMessageModel : Datasource is null");
            }

            var renderingParams = _renderingRepository.GetRenderingParameters<IHideModuleHeadingParam>();
            if (renderingParams == null)
            {
                _logRepository.Warn($"Rendering Parameters are null SupporterPortalMvcService CreateSponsoredStudentMessageModel");
            }
            else
            {
                modelDatasource.HideModuleHeading = renderingParams.HideModuleHeading;
            }
            Guid participantId;
            Guid.TryParse(HttpContext.Current.Request.QueryString[Constants.QueryStrings.ParticipantId], out participantId);
            modelDatasource.ParticipantGuid = participantId;
            return Result.Success(modelDatasource);
        }

        /// <summary>
        /// Get sponsored student select message tiles 
        /// </summary>
        /// <returns></returns>
        public Result<SponsoredStudentSelectMessageDto> SponsoredStudentSelectMessageModel()
        {
            var sponsoredStudentSelectMessages = new SponsoredStudentSelectMessageDto();

            // Get data source from CMS
            ISponsoredStudentSelectMessage modelDatasource = _renderingRepository.GetDataSourceItem<ISponsoredStudentSelectMessage>();
            if (modelDatasource == null)
            {
                _logRepository.Warn("Datasource is null. SupporterPortalMvcService SponsoredStudentSelectMessageModel");
                return Result.Failure<SponsoredStudentSelectMessageDto>("SponsoredStudentSelectMessageModel : Datasource is null");
            }
            sponsoredStudentSelectMessages.ModuleHeading = modelDatasource.ModuleHeading;
            sponsoredStudentSelectMessages.SelectMessageDatasource = modelDatasource;

            // Get all select message tiles from CMS
            var selectMessages = _portalMvcRepository.GetSponsoredStudentsSelectMessages();
            if (selectMessages == null)
            {
                _logRepository.Warn("GetSponsoredStudentsSelectMessages are null. SupporterPortalMvcService SponsoredStudentSelectMessageModel");
                return Result.Failure<SponsoredStudentSelectMessageDto>("SponsoredStudentSelectMessageModel : selectMessages is null");
            }

            // get portal settings from CMS
            ISupporterPortalSettings portalSettings = _contentRepository.GetItem<ISupporterPortalSettings>(CoreConstants.IDs.SupporterPortalSettings.Guid);
            if (portalSettings == null)
            {
                _logRepository.Warn("supporter portal settings is null. SupporterPortalMvcService SponsoredStudentSelectMessageModel");
                return Result.Failure<SponsoredStudentSelectMessageDto>("SponsoredStudentSelectMessageModel : portalSettings is null");
            }

            // prepare select message tiles as per business rules.
            sponsoredStudentSelectMessages.Tiles = PrepareSponsoredStudentSelectMessages(selectMessages, modelDatasource);

            // Get rendering parameters 
            var renderingParams = _renderingRepository.GetRenderingParameters<IHideModuleHeadingParam>();
            if (renderingParams == null)
            {
                _logRepository.Warn($"Rendering Parameters are null. SupporterPortalMvcService SponsoredStudentSelectMessageModel");
            }
            else
            {
                sponsoredStudentSelectMessages.HideModuleHeading = renderingParams.HideModuleHeading;
            }

            return Result.Success(sponsoredStudentSelectMessages);
        }

        /// <summary>
        /// Get File Gallery Module from content datasource
        /// </summary>
        /// <returns></returns>
        public Result<IFileGallery> CreateStudentFileGalleryModel()
        {
            // Get File Gallery details
            IFileGallery modelDatasource = _renderingRepository.GetDataSourceItem<IFileGallery>();
            if (modelDatasource == null)
            {
                _logRepository.Warn("Datasource is null SupporterPortalMvcService CreateStudentFileGalleryModel");
                return Result.Failure<IFileGallery>("CreateStudentFileGalleryModel : Datasource is null");
            }
            var renderingParams = _renderingRepository.GetRenderingParameters<IHideModuleHeadingParam>();
            if (renderingParams == null)
            {
                _logRepository.Warn($"Rendering Parameters are null SupporterPortalMvcService CreateStudentFileGalleryModel");
            }
            else
            {
                modelDatasource.HideModuleHeading = renderingParams.HideModuleHeading;
            }
            var studentDto = GetSponsoredStudentDto();


            if (modelDatasource?.FileStorageSourcePrimary?.Value != null)
                modelDatasource.DocumentsAvailble = GetDocumentsFlag(modelDatasource.FileStorageSourcePrimary.Value, studentDto.ParticipantSmithFamilyId);

            return Result.Success(modelDatasource);
        }
        /// <summary>
        /// Get session timeout settings from CMS
        /// </summary>
        /// <param name="sessionTimeout"></param>
        /// <returns></returns>
        public Result<ISupporterPortalSessionTimeout> SessionTimeoutSettingsModel(int sessionTimeout)
        {
            // get portal settings from CMS
            ISupporterPortalSessionTimeout portalSettings = _contentRepository.GetItem<ISupporterPortalSessionTimeout>(CoreConstants.IDs.SupporterPortalSettings.Guid);
            if (portalSettings == null)
            {
                _logRepository.Warn("supporter portal settings is null. SupporterPortalMvcService SessionTimeoutSettings");
                return Result.Failure<ISupporterPortalSessionTimeout>("SupporterPortalSessionTimeout : portalSettings is null");
            }

            ReplaceSessionTimeoutTokens(portalSettings, sessionTimeout);

            return Result.Success(portalSettings);
        }

        #region Private methods

        internal static SponsoredStudentDto GetSponsoredStudentDto()
        {
            return SessionHelper.Get<SponsoredStudentDto>(SessionKeys.SponsoredStudentData);
        }

        private string ReplaceQuoteText(string quoteText, SponsoredStudentDto studentDto, string participantInterestsIntroText)
        {
            string participantInterestsFinal = String.Empty;
            if (studentDto.ParticipantInterests.Count > 0)
            {
                participantInterestsFinal = String.Format(participantInterestsIntroText + " " + studentDto.ParticipantInterests.ListToString());
            }
            var tokenDictionary = new Dictionary<string, string>
                {
                    {Constants.Tokens.StudentQuotes.ParticipantAge, studentDto.ParticipantAge.Value.ToString()},
                    {Constants.Tokens.StudentQuotes.ParticipantBirthMonth, studentDto.ParticipantBirthMonth},
                    {Constants.Tokens.StudentQuotes.ParticipantState, studentDto.ParticipantState},
                    {Constants.Tokens.StudentQuotes.ParticipantEducationLevel, studentDto.ParticipantEducationLevel},
                    {Constants.Tokens.StudentQuotes.ParticipantYearLevel, studentDto.ParticipantYearLevel},
                    {Constants.Tokens.StudentQuotes.ParticipantSponsorshipStartDate,
                    studentDto.ParticipantSponsorshipStartDate?.Date.ToString(ExtensionConstants.TSFDateTimeDisplayFormatLongMonth)},
                    {Constants.Tokens.StudentQuotes.ParticipantInterests, participantInterestsFinal},
                };
            return quoteText.Replace(tokenDictionary);
        }

        private string ReplaceQuoteTitle(string quoteText, SponsoredStudentDto studentDto)
        {
            var tokenDictionary = new Dictionary<string, string>
                {
                    {Constants.Tokens.StudentQuotes.ParticipantFirstName, studentDto.ParticipantFirstName},
                };
            return quoteText.Replace(tokenDictionary);
        }

        private SPortalNavigationViewModel ConvertToViewModel(ISPortalNavigationModel paramInput)
        {
            SPortalNavigationViewModel viewModel = new SPortalNavigationViewModel();
            var navItem = ConvertToNavItem(paramInput);
            viewModel.NavList.Add(navItem);
            foreach (var child in paramInput.Children.Where(x => x.NavigationShow))
            {
                navItem = ConvertToNavItem(child);
                viewModel.NavList.Add(navItem);
            }
            return viewModel;
        }

        private SPortalNavigationItemViewModel ConvertToNavItem(ISPortalNavigationModel paramInput)
        {
            var retOutput = new SPortalNavigationItemViewModel
            {
                Title = string.IsNullOrEmpty(paramInput.NavigationTitle) ? paramInput.Name : paramInput.NavigationTitle,
                Link = paramInput.Url,
                Class = paramInput.NavigationClass,
                ItemId = paramInput.ID
            };
            return retOutput;
        }

        private string GetFirstName()
        {
            var firstName = _userService.GetUserFirstName();
            firstName = (firstName?.Length > 1) ? firstName : string.Empty; //Return empty string if first name is null or a single letter
            return firstName;
        }

        private string GetSupportPeriodText()
        {
            var customPropertyFirstDonationDate = string.Empty;
            // Read session variable
            var supporterBase = _sessionService.GetSupporterBase();
            if (supporterBase != null && supporterBase.FirstDonationDate != null) // Fetch from Session
            {
                customPropertyFirstDonationDate = supporterBase.FirstDonationDate.ToString();

            }
            else // Fetch from Sitecore user profile
            {
                customPropertyFirstDonationDate = User.Current.Profile.GetCustomProperty(CoreConstants.Membership.CustomProperties.FirstDonationDate);
            }

            bool parseFlag = DateTime.TryParse(customPropertyFirstDonationDate, out DateTime firstDonationDate);
            if (string.IsNullOrEmpty(customPropertyFirstDonationDate) && !parseFlag)
            {
                return string.Empty;
            }
            string supporterPeriodText = "1 month";
            if (firstDonationDate == DateTime.MinValue)
            {
                return supporterPeriodText;//if the facet contains the default date value i.e. 1900 - 01 - 01 it is displayed as '1 month'
            }

            var supportMonthPeriod = DateTime.Now.MonthDifference(firstDonationDate);

            switch (supportMonthPeriod)
            {
                case int period when (period < 2):
                    //supporterPeriodText = "1 month"; //if the period is less than 2 months it is displayed as '1 month'
                    break;
                case int period when (period > 1 && period < 24):
                    supporterPeriodText = $"{supportMonthPeriod} months";//if the period is greater than 1 month and less than 24 months it is displayed as 'x months' rounding down
                    break;
                case int period when (period >= 24):
                    supporterPeriodText = $"{supportMonthPeriod / 12} years";//if the period is 24 months or greater it is displayed as 'x years' rounding down i.e. 2 years 11 months would display as 2 years
                    break;
                case int period when (period < 2):
                    supporterPeriodText = "1 month";
                    break;
            }

            return supporterPeriodText;
        }

        private string GetTotalSponsoredChildren()
        {
            int totalSponsoredChildren = 0;
            var supporterBase = _sessionService.GetSupporterBase();
            if (supporterBase != null && supporterBase.TotalSponsoredStudents > 0) // Fetch from session 
            {
                totalSponsoredChildren = supporterBase.TotalSponsoredStudents;
            }
            else // Fetch from User profile
            {
                var customPropertyTotalSponsoredChildren = User.Current.Profile.GetCustomProperty(CoreConstants.Membership.CustomProperties.TotalSponsoredChildren);
                int.TryParse(customPropertyTotalSponsoredChildren, out totalSponsoredChildren);
            }

            return totalSponsoredChildren.ToString();
        }

        private bool IsSingleStudentTile(NameValueCollection queryStringsCollection)
        {
            if (GetQueryStringValue(queryStringsCollection, CoreConstants.QueryStrings.ParticipantId) != null)
            {
                // Check Template
                var currentItem = _contentRepository.GetCurrentItem<ISitecoreItem>();
                if (currentItem.TemplateId == Constants.TemplateIDs.Item.SupporterPortalStudentPageID)
                {
                    return true;
                }

            }
            return false;
        }

        private string GetQueryStringValue(NameValueCollection queryStringsCollection, string paramName)
        {
            return queryStringsCollection[paramName];
        }

        /// <summary>
        /// Prepare sponsored students select messages.
        /// </summary>
        /// <param name="datasourceSelectMessages"></param>
        /// <param name="modelDatasource"></param>
        /// <param name="portalSettings"></param>
        /// <returns></returns>
        private List<ITile> PrepareSponsoredStudentSelectMessages(List<ITile> datasourceSelectMessages, ISponsoredStudentSelectMessage modelDatasource)
        {
            var finalSelectMessages = new List<ITile>();

            int tileCount = 0;
            foreach (var selectMessage in datasourceSelectMessages)
            {
                ITile tile = null;
                // Only display tiles <= MaximumNumberOfTiles.
                if (tileCount >= modelDatasource.MaximumNumberOfTiles)
                {
                    break;
                }

                // Write A Message Tile
                if (selectMessage.ID == modelDatasource.WriteAMessageTile?.ID)
                {
                    tile = selectMessage;
                }
                // Brithday Tile
                else if (selectMessage.ID == modelDatasource.BirthdayTile?.ID)
                {
                    var sponsoredStudentData = SessionHelper.Get<SponsoredStudentDto>(SessionKeys.SponsoredStudentData);
                    if (sponsoredStudentData == null)
                    {
                        _logRepository.Warn("sponsoredStudentData Datasource is null SupporterPortalMvcService PrepareSponsoredStudentSelectMessages");
                    }
                    else if (NotificationPeriodHelper.IsBirthdayNotificationPeriod(sponsoredStudentData.ParticipantBirthDate.Value))
                    {
                        tile = selectMessage;
                    }
                }
                // Christmas Tile

                else if (selectMessage.ID == modelDatasource.ChristmasTile?.ID && NotificationPeriodHelper.IsChristmasNotificationPeriod())
                {
                    tile = selectMessage;
                }
                // Information Tile
                else if (selectMessage.ID == modelDatasource.InfoTile?.ID)
                {
                    tile = selectMessage;
                }

                if (tile != null)
                {
                    // Replace token in the Tile
                    ReplaceTokensInTile(tile);

                    finalSelectMessages.Add(tile);
                    tileCount++;
                }

            }

            return finalSelectMessages;
        }

        /// <summary>
        /// Replace Token in the Tile properties.
        /// </summary>
        /// <param name="tile"></param>
        /// <param name="queryStringsCollection"></param>
        private void ReplaceTokensInTile(ITile tile)
        {
            // get sponsored student data.
            SponsoredStudentDto sponsoredStudentData = SessionHelper.Get<SponsoredStudentDto>(SessionKeys.SponsoredStudentData);

            if (sponsoredStudentData != null)
            {

                var tokenDictionary = new Dictionary<string, string>
                {
                    {Constants.Tokens.ParticipantGUID, sponsoredStudentData.ParticipantGuid.ToString()},
                    {Constants.Tokens.StudentQuotes.ParticipantFirstName, sponsoredStudentData.ParticipantFirstName},
                    {CoreConstants.Tokens.ParticipantFirstNameWildCard, sponsoredStudentData.ParticipantFirstName.ToLower().RemoveSpaces()}

                };

                // Replace token with value.
                if (tile.TileLink != null && !string.IsNullOrEmpty(tile.TileLink.Query) && !string.IsNullOrEmpty(tile.TileLink.Url))
                {
                    tile.TileLink.Query = tile.TileLink.Query.Replace(tokenDictionary);
                    tile.TileLink.Url = tile.TileLink.Url.Replace(tokenDictionary);
                }
                tile.TileHeading = tile.TileHeading.Replace(tokenDictionary);
                tile.TileText = tile.TileText.Replace(tokenDictionary);
            }
        }

        public bool? GetDocumentsFlag(string mainFolder, string tsfId)
        {
            if (mainFolder == null)
                return false;
            string sessionKey = string.Format("{0}_{1}", mainFolder, tsfId);

            var documentsFlag = SessionHelper.Get<bool?>(sessionKey);
            if (documentsFlag == null ||
               tsfId != GetSponsoredStudentDto()?.ParticipantSmithFamilyId)
            {
                documentsFlag = _azureStorageFileService.HasFileEntries(mainFolder, tsfId);
                SessionHelper.Set(sessionKey, documentsFlag);
            }
            return documentsFlag;
        }

        /// <summary>
        /// Replace tokens
        /// </summary>
        /// <param name="portalSettings"></param>
        /// <param name="sessionTimeout"></param>
        private void ReplaceSessionTimeoutTokens(ISupporterPortalSessionTimeout portalSettings, int sessionTimeout)
        {
            var tokenDictionary = new Dictionary<string, string>
            {
                {Constants.Tokens.SessionLength, sessionTimeout.ToString()},
                {Constants.Tokens.Minutes, SessionTimeout.MinutesTokenValue},
                {Constants.Tokens.Seconds, SessionTimeout.SecondsTokenValue}
            };
            portalSettings.PrimaryButtonText = portalSettings.PrimaryButtonText.Replace(tokenDictionary);
            portalSettings.SuccessText = portalSettings.SuccessText.Replace(tokenDictionary);
            portalSettings.SessionTimeout = sessionTimeout;
            portalSettings.Text = portalSettings.Text.Replace(tokenDictionary);
            portalSettings.PrimaryButtonStyle = portalSettings.PrimaryButtonStyle.SetDefaultStyle(CoreConstants.CssClases.DefaultButtonPrimaryStyle);
            portalSettings.SecondaryButtonStyle = portalSettings.SecondaryButtonStyle.SetDefaultStyle(CoreConstants.CssClases.DefaultButtonSecondaryStyle);
        }

        private void PopulateSponsoredStudentQuote(ISponsoredStudentQuote modelDatasource)
        {
            var studentDto = GetSponsoredStudentDto();
            modelDatasource.QuoteTitle_TokenReplaced = ReplaceQuoteTitle(modelDatasource.QuoteTitle, studentDto);
            modelDatasource.QuoteText_TokenReplaced = ReplaceQuoteText(modelDatasource.QuoteText, studentDto, modelDatasource.ParticipantInterestsIntroText);
            modelDatasource.DisplayPrimaryCTA = false;
            IFileGallery primaryCTAfileGalleryDatasource = null;
            if (modelDatasource.QuotePrimaryCTARenderingCheck != null)
            {
                primaryCTAfileGalleryDatasource = _contentRepository.GetItem<IFileGallery>(modelDatasource.QuotePrimaryCTARenderingCheck.ID.ToGuid(), Sitecore.Context.Database);

                if (primaryCTAfileGalleryDatasource?.FileStorageSourcePrimary?.Value != null)
                    modelDatasource.DisplayPrimaryCTA = GetDocumentsFlag(primaryCTAfileGalleryDatasource.FileStorageSourcePrimary.Value, studentDto.ParticipantSmithFamilyId);
            }

            if (modelDatasource.DisplayPrimaryCTA == true)
            {
                modelDatasource.QuotePrimaryCTA_Url = $"#{primaryCTAfileGalleryDatasource?.ModuleAnchorID}";
            }

            modelDatasource.DisplaySecondaryCTA = false;
            IFileGallery secondaryCTAfileGalleryDatasource = null;
            if (modelDatasource.QuotePrimaryCTARenderingCheck != null)
            {
                secondaryCTAfileGalleryDatasource = _contentRepository.GetItem<IFileGallery>(modelDatasource.QuoteSecondaryCTARenderingCheck.ID.ToGuid(), Sitecore.Context.Database);

                if (secondaryCTAfileGalleryDatasource?.FileStorageSourcePrimary?.Value != null)
                    modelDatasource.DisplaySecondaryCTA = GetDocumentsFlag(secondaryCTAfileGalleryDatasource.FileStorageSourcePrimary.Value, studentDto.ParticipantSmithFamilyId);
            }

            if (modelDatasource.DisplaySecondaryCTA == true)
            {
                modelDatasource.QuoteSecondaryCTA_Url = $"#{secondaryCTAfileGalleryDatasource?.ModuleAnchorID}";
            }
        }

        #endregion Private methods
    }
}