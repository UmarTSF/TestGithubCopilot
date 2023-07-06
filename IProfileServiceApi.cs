using CSharpFunctionalExtensions;
using System;
using System.Threading.Tasks;
using TSF.Feature.SupporterPortalMvc.Models;
using TSF.Feature.SupporterPortalMvc.Models.Dto;
using TSF.Feature.SupporterPortalMvc.Models.Dto.Profile;
using TSF.Foundation.SupporterPortal.Models.Domain.Profile;
using TSF.Foundation.SupporterPortal.Models.Dto.Profile;

namespace TSF.Feature.SupporterPortalMvc.Services
{
    public interface IProfileServiceApi
    {
        Task<Result<SupporterProfileApiModel>> CreateSupporterProfileApiModel(Guid dataSourceID);
        Result<SupporterProfileApiModel> CreateChangePasswordApiModel();
        Result<UpdateProfileResponse> UpdatePassword(UpdatePasswordRequest request);
        bool LogoutUser();
        string GetErrorMessage(out bool reachMax);
        Task<Result<UpdateProfileResponse>> UpdateSupporterProfile(UpdateProfileRequest request);
        Task<Result<CommunicationPreferencesApiModel>> CreateCommunicationPreferencesApiModel(Guid dataSourceID);

        Task<Result<CommsPreferenceReturn>> UpdateCommsPreferenceBreak();
        Task<Result<UpdateProfileResponse>> UpdateCommsPreference(UpdateCommsPrefRequest request);

        Result<IProfileDetails> CreateProfileDetailsModel();
    }
}
