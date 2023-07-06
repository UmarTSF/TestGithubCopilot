using AutoMapper;
using CSharpFunctionalExtensions;
using System.Threading.Tasks;
using TSF.Foundation.Logging.Repositories;
using TSF.Foundation.SupporterPortal.ApiClient;
using TSF.Foundation.SupporterPortal.Models.Domain;
using TSF.Foundation.SupporterPortal.Models.Dto;

namespace TSF.Feature.SupporterPortalMvc.Services
{
    public class ExternalServiceApi : IExternalServiceApi
    {
        private readonly ILogRepository _logRepository;
        private readonly IExternalApiClient _externalApiClient;
        private readonly IMapper _mapper;

        public ExternalServiceApi(ILogRepository logRepository, IExternalApiClient externalApiClient, IMapper mapper)
        {
            _logRepository = logRepository;
            _externalApiClient = externalApiClient;
            _mapper = mapper;
        }

        /// <summary>
        /// Validate ReCaptcha Token
        /// </summary>
        /// <param name="reCaptchaToken"></param>
        /// <returns></returns>
        public async Task<Result<ReCaptchaResponseApiModel>> ValidateReCaptchaApiModel(string reCaptchaToken)
        {
            if (string.IsNullOrEmpty(reCaptchaToken))
            {
                _logRepository.Warn("'reCaptchaToken is null' in ExternalServiceApi.ValidateReCaptchaApiModel method.");

                return Result.Failure<ReCaptchaResponseApiModel>("ValidateReCaptchaApiModel : reCaptchaToken is null");
            }

            var result = await _externalApiClient.ValidateReCaptcha(reCaptchaToken);

            if (result == null)
            {
                _logRepository.Warn("ValidateReCaptcha API result is null ExternalServiceApi.ValidateReCaptchaApiModel method.");
                return Result.Failure<ReCaptchaResponseApiModel>("ValidateReCaptcha : API result is null");
            }

            var response = _mapper.Map<ReCaptchaResponseToken, ReCaptchaResponseApiModel>(result);

            return Result.Success(response);
        }
    }
}