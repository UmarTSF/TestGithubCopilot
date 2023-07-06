namespace TSF.Feature.SupporterPortalMvc.Services
{
    using AutoMapper;
    using CSharpFunctionalExtensions;
    using Sitecore.Data;
    using Sitecore.Security.Accounts;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using System.Threading.Tasks;
    using System.Web;
    using TSF.Feature.SupporterPortalMvc.Models;
    using TSF.Feature.SupporterPortalMvc.Models.Domain;
    using TSF.Feature.SupporterPortalMvc.Models.Dto;
    using TSF.Feature.SupporterPortalMvc.Repositories;
    using TSF.Foundation.Content.Models;
    using TSF.Foundation.Content.Repositories;
    using TSF.Foundation.Content.Services;
    using TSF.Foundation.Core;
    using TSF.Foundation.Core.Helpers;
    using TSF.Foundation.Core.Models;
    using TSF.Foundation.CRM.Types;
    using TSF.Foundation.CRM_D365.Enums;
    using TSF.Foundation.Email.Helpers;
    using TSF.Foundation.Extensions;
    using TSF.Foundation.Helpers;
    using TSF.Foundation.Helpers.Helpers;
    using TSF.Foundation.Logging.Repositories;
    using TSF.Foundation.Portal.Extensions;
    using TSF.Foundation.Portal.Models.Session;
    using TSF.Foundation.Portal.Services;
    using TSF.Foundation.Portal.Services.Azure;
    using TSF.Foundation.SupporterPortal.ApiClient;
    using TSF.Foundation.SupporterPortal.Enums;
    using TSF.Foundation.SupporterPortal.Models.Domain;
    using TSF.Foundation.SupporterPortal.Models.Dto;
    using TSF.Foundation.WebApi.Services;
    using static TSF.Feature.SupporterPortalMvc.Constants;

    public class SupporterPortalMvcServiceApi : ISupporterPortalMvcServiceApi
    {
        private readonly ILogRepository _logRepository;
        private readonly IContentRepository _contentRepository;
        private readonly IGenderService _genderService;
        private readonly ISupporterPortalMvcRepository _supporterPortalMvcRepository;
        private readonly IProcessingOverlayService _processingOverlayService;
        private readonly ISupporterCRMApiClient _supporterCRMApiClient;
        private readonly ISessionService _sessionService;
        private readonly IAzureStorageFileService _azureStorageFileService;
        private readonly IResponseService _responseService;

        private readonly string _db = Sitecore.Configuration.Settings.GetSetting("db");
        public SupporterPortalMvcServiceApi(ILogRepository logRepository,
            IContentRepository contentRepository,
            IGenderService genderService,
            IProcessingOverlayService processingOverlayService,
            ISupporterPortalMvcRepository supporterPortalMvcRepository,
            ISupporterCRMApiClient supporterCRMApiClient,
            ISessionService sessionService,
            IAzureStorageFileService azureStorageFileService,
            IResponseService responseService)
        {
            _logRepository = logRepository;
            _contentRepository = contentRepository;
            _genderService = genderService;
            _processingOverlayService = processingOverlayService;
            _supporterPortalMvcRepository = supporterPortalMvcRepository;
            _supporterCRMApiClient = supporterCRMApiClient;
            _sessionService = sessionService;
            _azureStorageFileService = azureStorageFileService;
            _responseService = responseService;
        }

        #region Public Methods
        public async Task<Result<LatestDonationPledgeActivityApiModel>> CreateLatestDonationPledgeActivityApiModel(Guid dataSourceID)
        {
            ILatestDonationPledgeActivity modelDatasource = _contentRepository.GetItem<ILatestDonationPledgeActivity>(dataSourceID, Sitecore.Context.Database);
            if(modelDatasource == null)
            {
                _logRepository.Warn("Datasource is null SupporterPortalMvcService CreateLastNextDonationDetailsModel");
                return Result.Failure<LatestDonationPledgeActivityApiModel>("CreateLastNextDonationDetailsModel : Datasource is null");
            }

            var loginUserInfo = _sessionService.GetLoginUserInfo();
            if (loginUserInfo == null)
            {
                _logRepository.Warn("loginUserInfo is null SupporterPortalMvcService CreateLastNextDonationDetailsModel");
                return Result.Failure<LatestDonationPledgeActivityApiModel>("CreateLastNextDonationDetailsModel : loginUserInfo is null");
            }

            var latestDonationPledgeActivity = await _supporterCRMApiClient.GetLatestDonationPledgeActivity(loginUserInfo.CustomerType, loginUserInfo.CustomerEntityGuid);
            if(latestDonationPledgeActivity == null)
            {
                _logRepository.Warn("GetLatestDonationPledgeActivity API result is null SupporterPortalMvcServiceApi CreateLatestDonationPledgeActivityApiModel");
                return Result.Failure<LatestDonationPledgeActivityApiModel>("CreateLatestDonationPledgeActivityApiModel : API result is null");
            }
            LatestDonationPledgeActivityApiModel yourGivingSummaryModel = Mapper.Map<ILatestDonationPledgeActivity, LatestDonationPledgeActivityApiModel>(modelDatasource);

            if(latestDonationPledgeActivity.NextDueDate?.Date > DateTime.MinValue)
            {
                yourGivingSummaryModel.PageContent.Top.Data = latestDonationPledgeActivity.NextDueDate?.Date.ToString("dd MMM yyyy");
                if(latestDonationPledgeActivity.PledgeDueNow)
                {
                    yourGivingSummaryModel.PageContent.Top.Title = modelDatasource.TextHeadingSecondary1;
                    yourGivingSummaryModel.IsPaymentOverdue = true;
                }
            }

            if(latestDonationPledgeActivity.LastDonationAmount > 0)
            {
                yourGivingSummaryModel.PageContent.Bottom.Data = latestDonationPledgeActivity.LastDonationAmount.ToString("C");
            }

            return Result.Success(yourGivingSummaryModel);
        }
        
        public async Task<Result<DonationsByFYGraphApiModel>> CreateDonationsByFYGraphApiModel(Guid dataSourceID)
        {
            IDonationsByFYGraph modelDatasource = _contentRepository.GetItem<IDonationsByFYGraph>(dataSourceID, Sitecore.Context.Database);
            if(modelDatasource == null)
            {
                _logRepository.Warn("Datasource is null SupporterPortalMvcService DonationsByFYGraphApiModel");
                return Result.Failure<DonationsByFYGraphApiModel>("DonationsByFYGraphApiModel : Datasource is null");
            }

            int numberOfYears = modelDatasource.NumberOfFYs;
            var loginUserInfo = _sessionService.GetLoginUserInfo();
            if (loginUserInfo == null)
            {
                _logRepository.Warn("loginUserInfo is null SupporterPortalMvcServiceApi DonationsByFYGraphApiModel");
                return Result.Failure<DonationsByFYGraphApiModel>("DonationsByFYGraphApiModel : loginUserInfo is null or empty");
            }

            var donationsByFYGraph = await _supporterCRMApiClient.GetDonationsByFYGraph(loginUserInfo.CustomerType, loginUserInfo.CustomerEntityGuid, numberOfYears);
            if(donationsByFYGraph?.Any() == false)
            {
                _logRepository.Warn("GetDonationsByFYGraph API result is null SupporterPortalMvcServiceApi DonationsByFYGraphApiModel");
                return Result.Failure<DonationsByFYGraphApiModel>("DonationsByFYGraphApiModel : API result is null or empty");
            }
            DonationsByFYGraphApiModel donationsByFYGraphModel = Mapper.Map<IDonationsByFYGraph, DonationsByFYGraphApiModel>(modelDatasource);

            if (donationsByFYGraph != null)
            {
                var chartData = new List<object[]>();
                foreach (var item in donationsByFYGraph)
                {
                    chartData.Add(new object[] { $"FY {item.StartDate?.ToString("yyyy")}/{item.EndDate?.ToString("yy")}", item.TotalAmount });
                }
                donationsByFYGraphModel.PageContent.ChartData = chartData;

                if (chartData.Count == 1)
                    donationsByFYGraphModel.PageContent.ScreenreaderText = $"This year in {chartData[chartData.Count - 1][0]} giving is ${chartData[chartData.Count - 1][1]}";
                else if (chartData.Count > 1)
                    donationsByFYGraphModel.PageContent.ScreenreaderText = $"Last year in {chartData[chartData.Count - 2][0]} giving was ${chartData[chartData.Count - 2][1]} and this year in {chartData[chartData.Count - 1][0]} giving is ${chartData[chartData.Count - 1][1]}";
            }

            return Result.Success(donationsByFYGraphModel);
        }


        public async Task<Result> SubmitQuestionnaire(SubmitQuestionnaire submitQuestionnaire)
        {
            string apiErrorMessage = _responseService.GetValidationMessage(Constants.ValidationMessage.SurveyResponseFailure);

            if(submitQuestionnaire == null)
            {
                _logRepository.Warn("Error in SupporterPortalMvcService SubmitQuestionnaireResponseApi : Input object is null");
                return Result.Failure(apiErrorMessage);
            }

            IQuestionnaire modelDatasource = _contentRepository.GetItem<IQuestionnaire>(submitQuestionnaire.Datasource.ToString(), Sitecore.Context.Database.Name);
            if(modelDatasource == null)
            {
                _logRepository.Warn("Datasource is null SupporterPortalMvcServiceApi SubmitQuestionnaireResponseApi");
                return Result.Failure(apiErrorMessage);
            }

            if((modelDatasource.QuestionType == QuestionType.SingleLineText || modelDatasource.QuestionType == QuestionType.MultiLineText) && submitQuestionnaire.AnswerText.Length > modelDatasource.TextAnswerMaxLength)
            {
                _logRepository.Warn("Error in SupporterPortalMvcService SubmitQuestionnaireResponseApi : Answer text exceeded the maximum characters limit");
                return Result.Failure("Answer text exceeded the maximum characters limit. Please correct and try again.");
            }

            CustomerBase loginUserInfo = _sessionService.GetLoginUserInfo();
            if (loginUserInfo == null)
            {
                _logRepository.Warn("loginUserInfo is null SupporterPortalMvcServiceApi SubmitQuestionnaireResponseApi");
                return Result.Failure(apiErrorMessage);
            }

            QuestionnaireResponse questionnaireResponse = Mapper.Map<QuestionnaireResponse>(modelDatasource);
            questionnaireResponse.CustomerType = loginUserInfo.CustomerType;
            questionnaireResponse.CustomerEntityGuid = loginUserInfo.CustomerEntityGuid;
            questionnaireResponse.AnswerText = submitQuestionnaire.AnswerText;
            questionnaireResponse.AnswerDate = DateTime.Now;

            QuestionnaireResponseReturn submitQuestionnaireResponse = await _supporterCRMApiClient.SubmitQuestionnaireResponse(questionnaireResponse);
            if(submitQuestionnaireResponse == null)
            {
                _logRepository.Warn("SubmitQuestionnaireResponse API result is null SupporterPortalMvcServiceApi SubmitQuestionnaireResponseApi");
                return Result.Failure(apiErrorMessage);
            }

            AnalyticsInputObject analyticsInputObject = new AnalyticsInputObject
            {
                Email = loginUserInfo.Email,
                FirstName = string.IsNullOrEmpty(SessionHelper.Get<string>(CoreConstants.SessionKeys.UserFirstName))
                    ? loginUserInfo.LoggedInName : SessionHelper.Get<string>(CoreConstants.SessionKeys.UserFirstName),
                CrmContactId = _sessionService.GetLoginUserInfo()?.CustomerEntityGuid ?? Guid.Empty
            };
            
            AnalyticsHelper.IdentifyUser(analyticsInputObject);

            TrackMarketingGoal(new Guid(modelDatasource.Goal.ID.ToString()));

            return Result.Success(submitQuestionnaireResponse);
        }

        /// <summary>
        /// Get Sponsored Students details.
        /// </summary>
        /// <param name="dataSourceID"></param>
        /// <param name="participantGuid"></param>
        /// <param name="isPaginationOrSorting"></param>
        /// <param name="pageNumber"></param>
        /// <param name="pageSize"></param>
        /// <param name="primarySortOrderID"></param>
        /// <returns></returns>
        public async Task<Result<SponsoredStudentsApiModel>> CreateSponsoredStudentsApiModel(Guid dataSourceID, bool isPaginationOrSorting = false, int pageNumber = 0, SponsoredStudentsSortOrder sortOrder = SponsoredStudentsSortOrder.DefaultSortOrder, Guid participantGuid = default(Guid), MessageType messageType = MessageType.NotDefined)
        {
            // Get DataSource 
            ISponsoredStudents dataSource = _contentRepository.GetItem<ISponsoredStudents>(dataSourceID, Sitecore.Context.Database);
            ISupporterPortalSettings portalSettings = _contentRepository.GetItem<ISupporterPortalSettings>(CoreConstants.IDs.SupporterPortalSettings.Guid.ToString(), Sitecore.Context.Database.Name);
            
            #region CRM API Call

            // CRM Input Data
            var loginUserInfo = _sessionService.GetLoginUserInfo();
            if(loginUserInfo == null)
            {
                _logRepository.Warn("loginUserInfo is null CreateSponsoredStudentsApiModel method");
                return Result.Failure<SponsoredStudentsApiModel>("SponsoredStudentsApiModel : loginUserInfo is null");
            }
            pageNumber = pageNumber != default(int) ? pageNumber : Constants.SponsoredStudents.DefaultPageNumber; // By Default it is 1.
            // For initial page load, get default sort order from CMS
            // For sorting on my sponsored students page, get user selected sort order.
            if(!isPaginationOrSorting && Int32.TryParse(dataSource?.PrimarySortOrder?.Value, out int sortOrderID))
            {
                sortOrder = Enum.IsDefined(typeof(SponsoredStudentsSortOrder), sortOrderID) ? (SponsoredStudentsSortOrder)sortOrderID : sortOrder;
            }

            int numberOfstudentsPerPage = dataSource != null ? dataSource.NumberOfStudentsPerPage : Constants.SponsoredStudents.DefaultPageNumber;

            // CRM API Call
            List<SponsoredStudent> sponsoredStudents = await _supporterCRMApiClient.GetSponsoredStudents(loginUserInfo.CustomerType, loginUserInfo.CustomerEntityGuid, participantGuid, portalSettings.RenewalNotificationDays,
                                                                                        portalSettings.ReplacementNotificationDays, portalSettings.UpgradeNotificationDays, sortOrder, pageNumber, numberOfstudentsPerPage);
            if(sponsoredStudents == null)
            {
                _logRepository.Warn("GetSponsoredStudents API result is null CreateSponsoredStudentsApiModel method");
                return Result.Failure<SponsoredStudentsApiModel>("SponsoredStudentsApiModel : API result is null");
            }
            #endregion  

            #region  Convert CRM data into Sponsored Students API Model as per business rules

            //  Convert CRM data into Sponsored Students API Model as per business rules
            var list = new List<StudentApiModel>();
            foreach(var item in sponsoredStudents)
            {
                // Prepare token dictionary
                Dictionary<string, string> tokenDictionary = PrepareSponsoredStudentsTokenDictionary(item);

                // Prepare sponsored students Api Model from CRM student dto based on business rules
                var sponsoredStudentsApiModel = ConvertToSponsoredStudentsApiModel(dataSource, item, tokenDictionary, messageType);
                
                if(sponsoredStudentsApiModel.IsSuccess)
                {
                    list.Add(sponsoredStudentsApiModel.Value);
                }
                else //Redirect user back to the corresponding student details page
                {
                    return Result.Failure<SponsoredStudentsApiModel>(sponsoredStudentsApiModel.Error);
                }
            }

            AddUnallocatedTiles(pageNumber, dataSource, sponsoredStudents, list);

            AddUpSellTile(dataSource, list);

            var apiModel = new SponsoredStudentsApiModel() { PageContent = new SponsoredStudentsApiModelPageContent() };
            apiModel.PageContent.Students = list;

            // This data requires only for initial load of sponsored student module.
            if(!isPaginationOrSorting)
            {
                PopulateSponsoredStudentsOtherDetails(dataSource, apiModel.PageContent);
            }
            #endregion

            return Result.Success(apiModel);
        }

        public Result<SponsoredStudentsMessagesApiModel> CreateSponsoredStudentMessageApiModel(Guid dataSourceID)
        {
            // Get DataSource 
            ISponsoredStudentsMessages contentItem = _contentRepository.GetItem<ISponsoredStudentsMessages>(dataSourceID.ToString(), Sitecore.Context.Database.Name);
            if (contentItem == null)
            {
                _logRepository.Warn("SponsoredStudentsMessagesApiModel API result is null CreateSponsoredStudentMessageApiModel method");
                return Result.Failure<SponsoredStudentsMessagesApiModel>("SponsoredStudentsMessagesApiModel : API result is null");
            }
            var apiModel =  Mapper.Map<ISponsoredStudentsMessages, SponsoredStudentsMessagesApiModel>(contentItem);
            apiModel.ProcessingOverlay = _processingOverlayService.MapProcessingOverlay(contentItem);
            var loginUserInfo = _sessionService.GetLoginUserInfo();
            if (loginUserInfo != null)
            {
                apiModel.FromField = string.IsNullOrEmpty(loginUserInfo.CustomerFirstName)?string.Empty: loginUserInfo.CustomerFirstName;
            }
            return Result.Success(apiModel);
        }

        public async Task<Result<bool>> SponsoredStudentMessageResponseApi(SponsoredStudentsMessagesPostApiModel model)
        {
            string apiErrorMessage = _responseService.GetValidationMessage(Constants.ValidationMessage.GeneralError);

            if (model == null)
            {
                _logRepository.Warn("Error in SupporterPortalMvcServiceApi SponsoredStudentMessageResponseApi : Input object is null");
                return Result.Failure<bool>(apiErrorMessage);
            }


            var loginUserInfo = _sessionService.GetLoginUserInfo();
            if (loginUserInfo == null)
            {
                _logRepository.Warn("loginUserInfo is null SupporterPortalMvcServiceApi SponsoredStudentMessageResponseApi");
                return Result.Failure<bool>(apiErrorMessage);
            }

            var requestModel = new SponsorshipCorrespondenceDto();

            requestModel.CustomerType = loginUserInfo.CustomerType;
            requestModel.customerEntityGuid = loginUserInfo.CustomerEntityGuid;
            requestModel.ParticipantGuid = model.ParticipantGuid;
            requestModel.CorrespondenceText = model.CorrespondenceText;
            requestModel.CorrespondenceFromText = model.CorrespondenceFromText;
            requestModel.MessageType = GetMessageTypeTokenCode(model.MessageType);

            await _supporterCRMApiClient.CreateSponsorshipCorrespondence(requestModel);

            //Send Email
            Hashtable tokenReplacements = GetSponsoredStudentMessageTokenReplacments(loginUserInfo, requestModel, model);

            var dataSource = Sitecore.Configuration.Factory.GetDatabase(_db).GetItem(Constants.SponsoredStudentMessageConstants.StudentMessageReviewEmailID);
            var email = dataSource.Fields["To"].Value;
            EmailHelper.SendEmail(dataSource, tokenReplacements, email, true);

            return Result.Success(true);
        }


        public Result<StudentFileGalleryApiModel> CreateStudentFileGalleryApiModel(Guid dataSourceID, Guid participantGuid)
        {
            IFileGallery modelDatasource = _contentRepository.GetItem<IFileGallery>(dataSourceID, Sitecore.Context.Database);
            if (modelDatasource == null)
            {
                _logRepository.Warn("Datasource is null TaxReceiptService CreateTaxReceiptsApiModel");
                return Result.Failure<StudentFileGalleryApiModel>("CreateTaxReceiptsApiModel : Datasource is null");
            }

            var loginUserInfo = _sessionService.GetLoginUserInfo();
            if (loginUserInfo == null)
            {
                _logRepository.Warn("GetTaxReceiptsByFinancialYear loginUserInfo is null TaxReceiptService CreateTaxReceiptsApiModel");
                return Result.Failure<StudentFileGalleryApiModel>("CreateTaxReceiptsApiModel : loginUserInfo is null");
            }

            // get sponsored student data.
            SponsoredStudentDto sponsoredStudentData = SessionHelper.Get<SponsoredStudentDto>(SessionKeys.SponsoredStudentData);

            var files = _azureStorageFileService.GetParticipantDocuments(modelDatasource.FileStorageSourcePrimary?.Value, participantGuid, sponsoredStudentData.ParticipantSmithFamilyId);
            
            StudentFileGalleryApiModel studentFileGalleryApiModel = new StudentFileGalleryApiModel
            {
                Tiles = PrepareImageTiles(modelDatasource.TilesPrimary.ToList(), files)
            };
            if (!string.IsNullOrEmpty(modelDatasource.FileStorageSourceSecondary?.Value))
            {
                files = _azureStorageFileService.GetParticipantDocuments(modelDatasource.FileStorageSourceSecondary.Value, participantGuid, sponsoredStudentData.ParticipantSmithFamilyId);
                studentFileGalleryApiModel.Tiles.AddRange(PrepareImageTiles(modelDatasource.TilesSecondary.ToList(), files));
            }
            studentFileGalleryApiModel.Tiles = studentFileGalleryApiModel.Tiles.OrderByDescending(x => x.Title).ToList();
            return Result.Success(studentFileGalleryApiModel);
        }

        #endregion

        #region Private Methods
        private Hashtable GetSponsoredStudentMessageTokenReplacments(CustomerBase loginUserInfo,
            SponsorshipCorrespondenceDto requestModel,
            SponsoredStudentsMessagesPostApiModel apiModel)
        {
            var sponsoredStudent = SessionHelper.Get<SponsoredStudentDto>(SessionKeys.SponsoredStudentData);


            return new Hashtable
                    {
                        {Constants.Tokens.MessageType,  GetMessageTypeToken(apiModel.MessageType)},
                        {Constants.Tokens.MessageText,  requestModel.CorrespondenceText},
                        {Constants.Tokens.MessageFrom,  requestModel.CorrespondenceFromText},
                        {Constants.Tokens.DateSent,  DateTime.Today.ToTSFDateFormat()},
                        {Constants.Tokens.CustomerGUID,  loginUserInfo.CustomerEntityGuid.ToString()},
                        {Constants.Tokens.CustomerSmithFamilyID,  loginUserInfo.CustomerSmithFamilyId},
                        {Constants.Tokens.CustomerFirstName,  string.IsNullOrEmpty(loginUserInfo.CustomerFirstName)? string.Empty: loginUserInfo.CustomerFirstName},
                        {Constants.Tokens.ParticipantGUID,  requestModel.ParticipantGuid.ToString()},
                        {Constants.Tokens.ParticipantSmithFamilyID,  sponsoredStudent?.ParticipantSmithFamilyId ?? ""},
                        {Constants.Tokens.StudentQuotes.ParticipantFirstName,  sponsoredStudent?.ParticipantFirstName ?? ""},
                    };
        }

        private string GetMessageTypeToken(string messageType)
        {
            string messageTypeToken = SponsoredStudentMessageConstants.MessageTypeTokenWriteAMessage;

            var settings = _contentRepository.GetItem<ISupporterPortalSettings>(CoreConstants.IDs.SupporterPortalSettings.Guid);
            var studentData = SessionHelper.Get<SponsoredStudentDto>(SessionKeys.SponsoredStudentData);

            if (studentData != null && settings != null && !String.IsNullOrEmpty(messageType))
            {
                switch (messageType)
                {
                    case SponsoredStudentMessageConstants.BirthdayMessageType:
                        if (NotificationPeriodHelper.IsBirthdayNotificationPeriod(studentData.ParticipantBirthDate.Value))
                        {
                            messageTypeToken = SponsoredStudentMessageConstants.MessageTypeTokenBirthday;
                        }
                        break;
                    case SponsoredStudentMessageConstants.ChristmasMessageType:
                        if (NotificationPeriodHelper.IsChristmasNotificationPeriod())
                        {
                            messageTypeToken = SponsoredStudentMessageConstants.MessageTypeTokenChristmas;
                        }
                        break;
                    default:
                        break;
                }
            }

            return messageTypeToken;
        }

        private string GetMessageTypeTokenCode(string messageType)
        {
            string messageTypeToken = GetMessageTypeToken(messageType);
            string messageTypeCode = SponsoredStudentMessageConstants.MessageTypeTokenWriteAMessageCode;

            switch (messageTypeToken)
            {
                case SponsoredStudentMessageConstants.MessageTypeTokenBirthday:
                    messageTypeCode = SponsoredStudentMessageConstants.MessageTypeTokenBirthdayCode;
                    break;
                case SponsoredStudentMessageConstants.MessageTypeTokenChristmas:
                    messageTypeCode = SponsoredStudentMessageConstants.MessageTypeTokenChristmasCode;
                    break;
            }

            return messageTypeCode;
        }

        private void TrackMarketingGoal(Guid goalID)
        {
            var result = "";

            try
            {
                if(goalID != Guid.Empty)
                {
                    var goalItem = Sitecore.Context.Database.GetItem(new ID(goalID));

                    if (!AnalyticsHelper.TrackGoal(goalItem))
                    {
                        result = "There's been a problem tracking the current question's Goal. Trying to track via xDB";
                        User sitecoreUser = Sitecore.Context.User;

                        if (!AnalyticsHelper.TrackGoal_In_XConnect(sitecoreUser.Name, goalID))
                            result = "There's been a problem tracking the current question's Goal via xDB.";
                    }
                }
            }
            catch(Exception ex)
            {
                result = "There's been an exception tracking the current question's Goal.";
                _logRepository.Error(result, ex);
            }

            if (!string.IsNullOrEmpty(result))
            {
                _logRepository.Warn(result);
            }
        }

        /// <summary>
        /// Populate Sponsored Students TotalStudents, Sorting List and other  details.
        /// </summary>
        /// <param name="dataSource"></param>
        /// <param name="pageContent"></param>
        private void PopulateSponsoredStudentsOtherDetails(ISponsoredStudents dataSource, SponsoredStudentsApiModelPageContent pageContent)
        {
            pageContent.TotalStudents = _supporterPortalMvcRepository.GetTotalSponsoredChildren();
            pageContent.SortingLabel = !string.IsNullOrWhiteSpace(dataSource.SortLabel) ? dataSource.SortLabel : Constants.SponsoredStudents.DefaultSortingLabel;
            pageContent.ShowViewAllButton = dataSource.ViewAllCTA?.ToLinkModel(Constants.SponsoredStudents.DefaultViewAllStudentsButtonStyle);
            pageContent.Sorting = _supporterPortalMvcRepository.GetSponsoredStudentsSortOrders().Select(f => new ListItemApiModel() { Label = f.Name_Field, Value = f.Value });
            pageContent.ProcessingOverlay =
                new ProcessingOverlay()
                {
                    ProcessingHeading = dataSource.ProcessingHeading,
                    ProcessingLogoImageSrc = dataSource.ProcessingLogoImage?.Src,
                    ProcessingText = dataSource.ProcessingText
                };
        }

        /// <summary>
        /// Prepare sponsored students Api Model from CRM student dto
        /// </summary>
        /// <param name="dataSource"></param>
        /// <param name="item"></param>
        /// <param name="tokenDictionary"></param>
        /// <returns></returns>
        private Result<StudentApiModel> ConvertToSponsoredStudentsApiModel(ISponsoredStudents dataSource, SponsoredStudent item, Dictionary<string, string> tokenDictionary, MessageType messageType)
        {
            string image = GetStudentImage(dataSource,item, messageType);
            IBadge badge = GetStudentBadge(item, dataSource);
            var model = new StudentApiModel()
            {
                Name = item.ParticipantFirstName,
                Intro = dataSource?.ChildIntro.Replace(tokenDictionary, true),
                Image = image,
                Information1 = dataSource?.ChildDetail1.Replace(tokenDictionary, true),
                Information2 = dataSource?.ChildDetail2.Replace(tokenDictionary, true),
                Information3 = dataSource?.ChildDetail3.Replace(tokenDictionary, true),
                Information4 = dataSource?.ChildDetail4.Replace(tokenDictionary, true),
                Information5 = dataSource?.ChildDetail5.Replace(tokenDictionary, true),
                ChildDetailLink = dataSource?.ChildDetailLink?.ToLinkModel(),
                Link = dataSource?.ChildTileCTA?.GetUrl(tokenDictionary),
                IsRenewal = item.ParticipantRenewal,
                IsReplacement = item.ParticipantReplacement,
                IsUpgrade = item.ParticipantUpgrade,
                Badge = MapBadge(badge, tokenDictionary),
                Button = item.ParticipantRenewal ? dataSource?.ChildRenewalCTA?.ToLinkModel(tokenDictionary) : null
            };

            // Send-Message page, Correspondence History moduel anchor ID reference update.
            if (!string.IsNullOrEmpty(dataSource?.ChildDetailLink?.Query) && dataSource.ChildDetailLink.Query.Contains(Constants.SponsoredStudents.AnchorIDSymbole) && model.ChildDetailLink != null)
            {
                model.ChildDetailLink.Url = dataSource.ChildDetailLink.Query;
            }

            // set default style for Renewal button
            if (model.Button != null && string.IsNullOrEmpty(model.Button.Style))
            {
                model.Button.Style = CoreConstants.CssClases.DefaultButtonPrimaryStyle;
            }

            return Result.Success(model);
        }

        private string GetStudentImage(ISponsoredStudents dataSource, SponsoredStudent item, MessageType messageType)
        {
            var gender = (GenderType)Convert.ToInt64(item.ParticipantGender);

            var studentData = SessionHelper.Get<SponsoredStudentDto>(SessionKeys.SponsoredStudentData);

            // If token value is null or empty then make complete string as an empty.
            // Populate Gender image based on CRM student gender
            // Student has renewal then populate renewal button
            // Student has replacement then popualte replacement text
            string image = string.Empty;
            switch(messageType)
            {
                case MessageType.NotDefined:
                    image = _genderService.GetGenderImage(gender)?.Src;
                    break;
                case MessageType.WriteAMessage:
                    image = dataSource.WriteAMessageImage?.Src;
                    break;
                case MessageType.Birthday:
                    if (studentData != null && NotificationPeriodHelper.IsBirthdayNotificationPeriod(studentData.ParticipantBirthDate.Value))
                    {
                        image = dataSource.BirthdayImage?.Src;
                    }
                    break;
                case MessageType.Christmas:
                    if (studentData != null && NotificationPeriodHelper.IsChristmasNotificationPeriod())
                    {
                        image = dataSource.ChristmasImage?.Src;
                    }
                    break;
            }

            // if image is still blank it should be set to default image.
            if (string.IsNullOrWhiteSpace(image))
            {
                if (messageType == MessageType.WriteAMessage)
                {
                    image = dataSource.WriteAMessageImage?.Src;
                }
                else
                {
                    image = _genderService.GetGenderImage(gender)?.Src;
                }
            }

            return image;
        }

        private Badge MapBadge(IBadge badge, Dictionary<string, string> tokenDictionary)
        {
            if (badge == null)
                return null;

            return new Badge
            {
                Label = badge.BadgeLabel?.Replace(tokenDictionary),
                Color = badge.BadgeColor?.Value,
                Icon = badge.BadgeIcon?.Value
            };
        }

        /// <summary>
        /// Prepare Sponsored Students token dictionary.         //
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        private static Dictionary<string, string> PrepareSponsoredStudentsTokenDictionary(SponsoredStudent item)
        {
            var age = string.Empty;
            string startDate = null;

            if(item.ParticipantBirthDate != null && item.ParticipantBirthDate.Value != DateTime.MinValue && DateTime.Now.Age(item.ParticipantBirthDate) != 0)
                age = DateTime.Now.Age(item.ParticipantBirthDate).ToString();
            if(item.ParticipantSponsorshipStartDate != null && item.ParticipantSponsorshipStartDate.Value != DateTime.MinValue)
                startDate = item.ParticipantSponsorshipStartDate.TSFDisplayFormat();

            // Do not display label if token is empty
            var tokenDictionary = new Dictionary<string, string>() {
                {CoreConstants.Tokens.ParticipantSmithFamilyID, item.ParticipantSmithFamilyId},
                {CoreConstants.Tokens.ParticipantState, item.ParticipantState},
                {CoreConstants.Tokens.ParticipantEducationLevel, item.ParticipantEducationLevel},
                {CoreConstants.Tokens.ParticipantYearLevel, item.ParticipantYearLevel},
                {CoreConstants.Tokens.ReplacedParticipantFirstName, item.ParticipantReplacementFirstName },
                {CoreConstants.Tokens.ParticipantGUID, item.ParticipantGuid != Guid.Empty ? item.ParticipantGuid.ToString("N") : string.Empty},
                {CoreConstants.Tokens.PledgeGUID, item.PledgeGuid != Guid.Empty ? item.PledgeGuid.ToString("N") : string.Empty},
                {CoreConstants.Tokens.ParticipantAge, age},
                {CoreConstants.Tokens.ParticipantSponsorshipStartDate, startDate},
                {CoreConstants.Tokens.ParticipantFirstNameWildCard, item.ParticipantFirstName.ToLower().RemoveSpaces()}
            };

            return tokenDictionary;
        }

        /// <summary>
        /// Prepare Student Badge details
        /// </summary>
        /// <param name="student"></param>
        /// <param name="modelDatasource"></param>
        /// <param name="tokenDictionary"></param>
        /// <returns></returns>
        private IBadge GetStudentBadge(SponsoredStudent item, ISponsoredStudents modelDatasource)
        {
            if (item != null && modelDatasource?.ChildBadges != null && modelDatasource.ChildBadges.Any())
            {
                IBadge badge = modelDatasource.ChildBadges.FirstOrDefault(x => CheckBadgeFlag(item, x.CRMAPIAttribute));
                if (badge != null)
                    return badge;
            }
            return null;
        }

        public bool CheckBadgeFlag(SponsoredStudent item, string crmFlag)
        {
            if (item != null && !string.IsNullOrEmpty(crmFlag))
            {
                PropertyInfo prop = item.GetType().GetProperty(crmFlag);
                if (prop != null)
                    return (bool)prop.GetValue(item);
            }
            return false;
        }

        /// <summary>
        /// Add Unallocated Tiles
        /// </summary>
        /// <para>
        /// Add Unallocated Tiles if number of students returned from the CRM API is less than the value in the 'SupporterData' session key 'Total Sponsored Children' parameter
        /// Send the through to the FE API an 'Unallocated Tile' for each missing student. 
        /// </para>
        /// <param name="pageNumber"></param>
        /// <param name="dataSource"></param>
        /// <param name="sponsoredStudents"></param>
        /// <param name="list">List item object which the Tiles to be added to</param>
        private void AddUnallocatedTiles(int pageNumber, ISponsoredStudents dataSource, List<SponsoredStudent> sponsoredStudents, List<StudentApiModel> list)
        {
            var supporterBase = _sessionService.GetSupporterBase();
            if(dataSource.ShowUnallocatedTiles && list.Count < dataSource.NumberOfStudentsPerPage && supporterBase != null) // Fetch from Session
            {
                int allocatedCountUptoThePage = AllocatedStudentsCount(pageNumber, dataSource, sponsoredStudents);
                int unallocatedCountForThePage = UnallocatedStudentsCount(pageNumber, dataSource, supporterBase);
                for(var i = allocatedCountUptoThePage; i < unallocatedCountForThePage; i++)
                {
                    list.Add(new StudentApiModel()
                    {
                        Name = dataSource?.UnallocatedHeading,
                        Intro = dataSource?.UnallocatedIntro,
                        Information1 = dataSource?.UnallocatedText,
                        Button = dataSource?.UnallocatedCTA?.ToLinkModel(CoreConstants.CssClases.DefaultButtonPrimaryStyle),
                        Image = dataSource?.UnallocatedImage?.Src
                    });
                }
            }
        }

        /// <summary>
        /// Get the count of Un-Allocated Students Tiles for the current page
        /// </summary>
        /// <param name="pageNumber"></param>
        /// <param name="dataSource"></param>
        /// <param name="supporterBase"></param>
        private static int UnallocatedStudentsCount(int pageNumber, ISponsoredStudents dataSource, SupporterBase supporterBase)
        {
            return Math.Min(pageNumber * dataSource.NumberOfStudentsPerPage, supporterBase.TotalSponsoredStudents);
        }

        /// <summary>
        /// Get the count of Allocated Students so far
        /// </summary>
        /// <param name="pageNumber"></param>
        /// <param name="dataSource"></param>
        /// <param name="sponsoredStudents"></param>
        private static int AllocatedStudentsCount(int pageNumber, ISponsoredStudents dataSource, List<SponsoredStudent> sponsoredStudents)
        {
            return ((pageNumber - 1) * dataSource.NumberOfStudentsPerPage) + sponsoredStudents.Count;
        }

        /// <summary>
        /// Add UpSell tile if number of students is less than CMS Page Number
        /// </summary>
        /// <param name="dataSource"></param>
        /// <param name="list">List item object which the Tile to be added to</param>
        private static void AddUpSellTile(ISponsoredStudents dataSource, List<StudentApiModel> list)
        {
            if(dataSource.ShowUpsellTile && list.Count < dataSource.NumberOfStudentsPerPage)
            {
                list.Add(new StudentApiModel()
                {
                    Name = dataSource?.UpsellHeading,
                    Intro = dataSource?.UpsellIntro,
                    Information1 = dataSource?.UpsellText,
                    Link = dataSource?.UpsellCTA?.GetUrl(),
                    Image = dataSource?.UpsellImage?.Src
                });
            }
        }

        private static List<ImageTile> PrepareImageTiles(List<IFileTile> Tiles, List<Foundation.CRM_D365.Models.AzureFileEntry> files)
        {
            Dictionary<string, string> tokens = new Dictionary<string, string>()
            {
                { CoreConstants.Tokens.ParticipantFirstNameWildCard, Constants.Paths.StudentGallaryDocumentPageName}
            };

            List<ImageTile> imageTiles = new List<ImageTile>();
            var defaultTile = Tiles.AsEnumerable().FirstOrDefault(x => x.Default);
            
            foreach (var file in files.OrderBy(x => x.Year).ToList())
            {
                IFileTile fileTile = null;
                if (Tiles.Any(x => x.FileMatchingValue == file.Year.ToString()))
                {
                    fileTile = Tiles.FirstOrDefault(x => x.FileMatchingValue == file.Year.ToString());
                }
                else
                {
                    //If no matching tile is found the 'Default' tile is used
                    fileTile = defaultTile;
                }

                // Replace the [Year] token in the 'Tilte' field.
                var tokenDictionary = new Dictionary<string, string>
                        {
                            {Constants.Tokens.FileGallery.Year, file.Year.ToString()}
                        };

                imageTiles.Add(
                    new ImageTile
                    {
                        Title = fileTile?.Title.Replace(tokenDictionary),
                        Image = fileTile?.ContentImage.Src,
                        DestinationURL = file.Url?.Replace(tokens)
                    });
            }

            return imageTiles;
        }
          
        #endregion
    }
}