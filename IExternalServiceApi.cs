using CSharpFunctionalExtensions;
using System.Threading.Tasks;
using TSF.Foundation.SupporterPortal.Models.Dto;

namespace TSF.Feature.SupporterPortalMvc.Services
{
    public interface IExternalServiceApi
    {
        Task<Result<ReCaptchaResponseApiModel>> ValidateReCaptchaApiModel(string reCaptchaToken);
    }
}
