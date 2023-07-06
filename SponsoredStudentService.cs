namespace TSF.Feature.SupporterPortalMvc.Services
{
    using CSharpFunctionalExtensions;
    using TSF.Feature.SupporterPortalMvc.Models.Domain;
    using TSF.Foundation.Content.Repositories;
    using TSF.Foundation.Logging.Repositories;

    public class SponsoredStudentService : ISponsoredStudentService
    {
        #region Private Fields
        private readonly ILogRepository _logRepository;
        private readonly IRenderingRepository _renderingRepository;
        #endregion

        #region Constructor
        public SponsoredStudentService(ILogRepository logRepository,
            IRenderingRepository renderingRepository)
        {
            _logRepository = logRepository;
            _renderingRepository = renderingRepository;
        }
        #endregion

        #region Public Methods
        public Result<ICorrespondenceHistory> CorrespondenceHistoryModel()
        {
            // Get data source from CMS
            ICorrespondenceHistory modelDatasource = _renderingRepository.GetDataSourceItem<ICorrespondenceHistory>();
            if (modelDatasource == null)
            {
                _logRepository.Warn("Datasource is null SponsoredStudentService CorrespondenceHisotryModel");
                return Result.Failure<ICorrespondenceHistory>("CorrespondenceHisotryModel : Datasource is null");
            }

            return Result.Success(modelDatasource);
        }
        #endregion
    }
}