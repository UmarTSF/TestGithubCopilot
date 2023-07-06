namespace TSF.Feature.SupporterPortalMvc.Services
{
    using CSharpFunctionalExtensions;
    using System.Collections.Specialized;
    using TSF.Feature.SupporterPortalMvc.Models;
    using TSF.Feature.SupporterPortalMvc.Models.Domain;
    using TSF.Feature.SupporterPortalMvc.Models.Dto;
    using TSF.Foundation.Content.Models;
    using ViewModels;

    public interface ISupporterPortalMvcService
    {
        SPortalNavigationViewModel GetPortalNavigation();
        Result<SmithFamilyIDModel> CreateSmithFamilyIDModel();
        Result<IWelcomeMessage> CreateWelcomeMessageModel();
        Result<ILatestDonationPledgeActivity> CreateLatestDonationPledgeActivityModel();
        Result<IDonationsByFYGraph> CreateDonationsByFYGraphModel();
        Result<IYourImpact> CreateYourImpactModel();
        Result<IQuestionnaire> CreateQuestionnaireModel();
        Result<ISponsoredStudents> CreateSponsoredStudentsModel(NameValueCollection queryStringsCollection);
        Result<ISponsoredStudentQuote> CreateSponsoredStudentQuoteModel();
        Result<ISponsoredStudentsMessagesModel> CreateSponsoredStudentMessageModel();
        Result<IStudentInformation> CreateStudentInformationModel();
        Result<SponsoredStudentSelectMessageDto> SponsoredStudentSelectMessageModel();
        Result<IFileGallery> CreateStudentFileGalleryModel();
        Result<ISupporterPortalSessionTimeout> SessionTimeoutSettingsModel(int sessionTimeout);
    }
}
