namespace TSF.Feature.SupporterPortalMvc.Services
{
    using CSharpFunctionalExtensions;
    using TSF.Feature.SupporterPortalMvc.Models.Domain;

    public interface ISponsoredStudentService
    {
        Result<ICorrespondenceHistory> CorrespondenceHistoryModel();
    }
}
