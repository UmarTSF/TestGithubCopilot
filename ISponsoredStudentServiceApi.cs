using CSharpFunctionalExtensions;
using System;
using System.Threading.Tasks;
using TSF.Feature.SupporterPortalMvc.Models.Dto;

namespace TSF.Feature.SupporterPortalMvc.Services
{
    public interface ISponsoredStudentServiceApi
    {
        Task<Result<SponsoredStudentHistoryApiModel>> CreateCorrespondenceHistoryApiModel(Guid dataSourceID);
    }
}
