namespace TSF.Feature.SupporterPortalMvc.Services
{
    using AutoMapper;
    using CSharpFunctionalExtensions;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using TSF.Feature.SupporterPortalMvc.Models.Domain;
    using TSF.Feature.SupporterPortalMvc.Models.Dto;
    using TSF.Foundation.Content.Repositories;
    using TSF.Foundation.Core;
    using TSF.Foundation.CRM_D365.Enums;
    using TSF.Foundation.Extensions;
    using TSF.Foundation.Logging.Repositories;
    using TSF.Foundation.Portal.Models.Session;
    using TSF.Foundation.Portal.Services;
    using TSF.Foundation.SupporterPortal.ApiClient;
    using TSF.Foundation.SupporterPortal.Models.Domain;
    using static TSF.Feature.SupporterPortalMvc.Constants;

    public class SponsoredStudentServiceApi : ISponsoredStudentServiceApi
    {
        #region Private Fields
        private readonly ILogRepository _logRepository;
        private readonly IGenderService _genderService;
        private readonly ISessionService _sessionService;        
        private readonly IContentRepository _contentRepository;
        private readonly ISupporterCRMApiClient _supporterCRMApiClient;
        #endregion

        #region Constructor
        public SponsoredStudentServiceApi(ILogRepository logRepository, IGenderService genderService,
            ISessionService sessionService, IContentRepository contentRepository, ISupporterCRMApiClient supporterCRMApiClient)
        {
            _logRepository = logRepository;
            _genderService = genderService;
            _sessionService = sessionService;
            _contentRepository = contentRepository;
            _supporterCRMApiClient = supporterCRMApiClient;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Get Student Correspondence History
        /// </summary>
        /// <param name="dataSourceID"></param>
        /// <returns></returns>
        public async Task<Result<SponsoredStudentHistoryApiModel>> CreateCorrespondenceHistoryApiModel(Guid dataSourceID)
        {
            // Get Datasource
            ICorrespondenceHistory modelDatasource = _contentRepository.GetItem<ICorrespondenceHistory>(dataSourceID);
            if (modelDatasource == null)
            {
                _logRepository.Warn("Datasource is null CreateCorrespondenceHistoryApiModel");
                return Result.Failure<SponsoredStudentHistoryApiModel>("CreateCorrespondenceHistoryApiModel : Datasource is null");
            }

            // Get session information.
            CustomerBase loginUserInfo = _sessionService.GetLoginUserInfo();
            SponsoredStudentDto sponsoredStudentDto = _sessionService.GetSession<SponsoredStudentDto>(SessionKeys.SponsoredStudentData);
            if (loginUserInfo == null || sponsoredStudentDto == null)
            {
                _logRepository.Warn("CustomerBase and SponsoredStudentDto sessions are null SponsoredStudentServiceApi CreateCorrespondenceHistoryApiModel");
                return Result.Failure<SponsoredStudentHistoryApiModel>("CreateCorrespondenceHistoryApiModel : Sessions are null (CustomerBase and SponsoredStudentDto)");
            }

            // CRM API Call
            var studentHistory = await _supporterCRMApiClient.GetSponsoredStudentHistory(loginUserInfo.CustomerType, loginUserInfo.CustomerEntityGuid, sponsoredStudentDto.ParticipantGuid);
            if (studentHistory == null)
            {
                _logRepository.Warn("GetSponsoredStudentHistory API result is null CreateCorrespondenceHistoryApiModel method");
                return Result.Failure<SponsoredStudentHistoryApiModel>("CreateCorrespondenceHistoryApiModel : API result is null");
            }

            SponsoredStudentHistoryApiModel response = PrepareCorrespondenceHistory(modelDatasource, studentHistory, sponsoredStudentDto);

            return Result.Success(response);
        }
        #endregion

        #region Private Methods
    
        /// <summary>
        /// Prepare correspondence history.
        /// </summary>
        /// <param name="datasource"></param>
        /// <param name="studentHistory"></param>
        /// <param name="studentSession"></param>
        /// <returns></returns>
        private SponsoredStudentHistoryApiModel PrepareCorrespondenceHistory(ICorrespondenceHistory datasource, SponsoredStudentHistory studentHistory, SponsoredStudentDto studentSession)
        {
            // Get Student Icon
            var gender = (GenderType)Convert.ToInt64(studentSession.ParticipantGender);
            var genderImageUrl = _genderService.GetGenderNewIcon(gender)?.Src;

            // Dictory to replace tokens
            var tokenDictionary = new Dictionary<string, string>() {
                {CoreConstants.Tokens.ParticipantFirstName, studentSession.ParticipantFirstName}
            };

            // Set Default Styles
            datasource.NoResultsPrimaryCTA = datasource.NoResultsPrimaryCTA?.SetDefaultStyling(CoreConstants.CssClases.DefaultButtonPrimaryStyle, true);
            datasource.NoResultsSecondaryCTA = datasource.NoResultsSecondaryCTA?.SetDefaultStyling(CoreConstants.CssClases.DefaultButtonSecondaryStyle, true);

            // Create response
            SponsoredStudentHistoryApiModel response = Mapper.Map<ICorrespondenceHistory, SponsoredStudentHistoryApiModel>(datasource);
            response.NoResultsText = response.NoResultsText?.Replace(tokenDictionary);
            response.NoResultsTextHeading = response.NoResultsTextHeading?.Replace(tokenDictionary);
            response.DirectionFilterReceivedLabel = datasource.DirectionFilterReceivedLabel?.FilterText;
            response.DirectionFilterSentLabel = datasource.DirectionFilterSentLabel?.FilterText;
            response.CorrespondenceHistory = new List<CorrespondenceHistoryDto>();

            // Correspondence Categories
            var categories = datasource.CorrespondenceCategorySource?.CorrespondenceCategories?.ToList();

            // Sort by Date
            var orderedHistory = studentHistory.CorrespondenceHistory?.OrderByDescending(x => x.CorrespondenceDate);

            if (orderedHistory != null)
            {
                foreach (var item in orderedHistory)
                {
                    ICorrespondenceCategory category = categories.FirstOrDefault(x => x.CRMValue == ((int)item.CorrespondenceCategory).ToString() &&
                                                                x.CorrespondenceDirection?.CRMValue == ((int)item.CorrespondenceDirection).ToString());
                    var dto = new CorrespondenceHistoryDto()
                    {
                        StudentImage = genderImageUrl,
                        CorrespondenceDate = ((DateTime?)item.CorrespondenceDate).TSFDisplayFormat(),
                        CorrespondenceDescription = category?.Text?.Replace(tokenDictionary),
                        CorrespondenceDirection = category?.CorrespondenceDirection?.Text?.Replace(tokenDictionary),
                        CorrespondenceDirectionFilter = category?.CorrespondenceDirection?.FilterText
                    };

                    response.CorrespondenceHistory.Add(dto);
                }
            }

            // FE expectes nul if no correspondence history
            if (response.CorrespondenceHistory.Count == 0)
            {
                response.CorrespondenceHistory = null;
            }


            return response;
        }
        #endregion
    }
}