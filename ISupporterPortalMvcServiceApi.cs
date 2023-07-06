namespace TSF.Feature.SupporterPortalMvc.Services
{
    using CSharpFunctionalExtensions;
    using System.Threading.Tasks;
    using TSF.Feature.SupporterPortalMvc.Models.Dto;
    using System;
    using TSF.Foundation.SupporterPortal.Models.Domain;
    using System.Collections.Generic;
    using TSF.Foundation.CRM.Types;
    using TSF.Foundation.SupporterPortal.Enums;
    using TSF.Feature.SupporterPortalMvc.Models.Domain;

    public interface ISupporterPortalMvcServiceApi
    {
        Task<Result<LatestDonationPledgeActivityApiModel>> CreateLatestDonationPledgeActivityApiModel(Guid dataSourceID);
        Task<Result<DonationsByFYGraphApiModel>> CreateDonationsByFYGraphApiModel(Guid dataSourceID);
        Task<Result> SubmitQuestionnaire(SubmitQuestionnaire submitQuestionnaire);
        Task<Result<SponsoredStudentsApiModel>> CreateSponsoredStudentsApiModel(Guid dataSourceID, bool isPaginationOrSorting = false, int pageNumber = 0, SponsoredStudentsSortOrder sortOrder = SponsoredStudentsSortOrder.DefaultSortOrder, Guid participantGuid = default(Guid), MessageType messageType = MessageType.NotDefined);
        Result<SponsoredStudentsMessagesApiModel> CreateSponsoredStudentMessageApiModel(Guid dataSourceID);
        Task<Result<bool>> SponsoredStudentMessageResponseApi(SponsoredStudentsMessagesPostApiModel model);
        Result<StudentFileGalleryApiModel> CreateStudentFileGalleryApiModel(Guid dataSourceID, Guid participantGuid);
    }
}
