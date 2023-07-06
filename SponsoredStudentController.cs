namespace TSF.Feature.SupporterPortalMvc.Controllers
{
    using CSharpFunctionalExtensions;
    using Services;
    using System.Web.Mvc;
    using TSF.Feature.SupporterPortalMvc.Models.Domain;
    using TSF.Foundation.Content.Base;

    public class SponsoredStudentController : MvcController
    {
        #region Private Fields
        private readonly ISponsoredStudentService _supporterStudentMvcService;
        #endregion

        #region Constructor
        public SponsoredStudentController(ISponsoredStudentService supporterStudentMvcService)
        {
            _supporterStudentMvcService = supporterStudentMvcService;
        }
        #endregion

        #region Public Methods
        public ActionResult CorrespondenceHistory()
        {
            Result<ICorrespondenceHistory> modelResult = _supporterStudentMvcService.CorrespondenceHistoryModel();
            if (modelResult.IsFailure)
                return CreateErrorView("SponsoredStudentController.CorrespondenceHisotry", modelResult.Error);

            return View(modelResult.Value);
        }
        #endregion
    }
}