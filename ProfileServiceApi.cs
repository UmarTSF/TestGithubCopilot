namespace TSF.Feature.SupporterPortalMvc.Services
{
    using AutoMapper;
    using CSharpFunctionalExtensions;
    using Sitecore.Security.Accounts;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using TSF.Feature.SupporterPortalMvc.Models;
    using TSF.Feature.SupporterPortalMvc.Models.Dto;
    using TSF.Feature.SupporterPortalMvc.Models.Dto.Profile;
    using TSF.Foundation.Content.Models;
    using TSF.Foundation.Content.Repositories;
    using TSF.Foundation.Core;
    using TSF.Foundation.CRM_D365.Enums;
    using TSF.Foundation.Extensions;
    using TSF.Foundation.Logging.Repositories;
    using TSF.Foundation.Portal.Extensions;
    using TSF.Foundation.Portal.Models.Profile;
    using TSF.Foundation.Portal.Models.Session;
    using TSF.Foundation.Portal.Services;
    using TSF.Foundation.SupporterPortal.ApiClient;
    using TSF.Foundation.SupporterPortal.Enums;
    using TSF.Foundation.SupporterPortal.Models.Domain;
    using TSF.Foundation.SupporterPortal.Models.Domain.Profile;
    using TSF.Foundation.SupporterPortal.Models.Dto;
    using TSF.Foundation.SupporterPortal.Models.Dto.Profile;
    using static TSF.Feature.SupporterPortalMvc.Constants;
    using static TSF.Foundation.Content.ContentConstants;
    using static TSF.Foundation.Portal.PortalConstants;

    public class ProfileServiceApi : IProfileServiceApi
    {
        #region Private Fields
        private readonly ILogRepository _logRepository;
        private readonly IContentRepository _contentRepository;
        private readonly ISupporterCRMApiClient _supporterCRMApiClient;
        private readonly ISupporterService _supporterService;
        private readonly ISessionService _sessionService;
        private readonly IMembershipService _membershipService;
        private readonly IRenderingRepository _renderingRepository;
        #endregion

        #region Constructor
        public ProfileServiceApi(ILogRepository logRepository,
            IContentRepository contentRepository,
            ISupporterCRMApiClient supporterCRMApiClient,
            ISupporterService supporterService,
            ISessionService sessionService,
            IMembershipService membershipService,
            IRenderingRepository renderingRepository)
        {
            _logRepository = logRepository;
            _contentRepository = contentRepository;
            _supporterCRMApiClient = supporterCRMApiClient;
            _supporterService = supporterService;
            _sessionService = sessionService;
            _membershipService = membershipService;
            _renderingRepository = renderingRepository;
        }
        #endregion

        #region Public Methods

        /// <summary>
        /// Get Profile Tab1 details (IndividualSupporter/BusinessSupporter)
        /// </summary>
        /// <param name="dataSourceID"></param>
        /// <returns></returns>
        public async Task<Result<SupporterProfileApiModel>> CreateSupporterProfileApiModel(Guid dataSourceID)
        {
            #region CRM API Call
            // CRM Input Data
            var loginUserInfo = _sessionService.GetLoginUserInfo();

            // CRM API Call
            var supporterDetails = await _supporterCRMApiClient.GetSupporterDetails(loginUserInfo.CustomerType, loginUserInfo.CustomerEntityGuid);
            if (supporterDetails == null)
            {
                _logRepository.Warn("SupporterProfile API result is null CreateSupporterProfileApiModel method");
                return Result.Failure<SupporterProfileApiModel>("CreateSupporterProfileApiModel : API result is null");
            }
            #endregion

            #region Get Fields Details from CMS 
            // Get DataSource 
            IProfileDetails dataSource = _contentRepository.GetItem<IProfileDetails>(dataSourceID, Sitecore.Context.Database);
            IProfileFormSection formSection = null;
            if (loginUserInfo.CustomerType == SupporterClass.IndividualSupporter)
            {
                formSection = _contentRepository.GetItem<IProfileFormSection>(dataSource.IndividualSupporter.Guid, Sitecore.Context.Database);
            }
            else if (loginUserInfo.CustomerType == SupporterClass.BusinessSupporter)
            {
                formSection = _contentRepository.GetItem<IProfileFormSection>(dataSource.CorporateSupporter.Guid, Sitecore.Context.Database);
            }
            #endregion

            #region Response object construction
            // create response
            var response = new SupporterProfileApiModel()
            {
                SmithFamilyId = _supporterService.GetCurrentSupporterTsfId(),
                DonorType = loginUserInfo.CustomerType.ToString(),
                ProcessingOverlay = new ProcessingOverlay()
                {
                    ProcessingHeading = formSection?.ProcessingHeading,
                    ProcessingLogoImageSrc = formSection?.ProcessingLogoImage?.Src,
                    ProcessingText = formSection?.ProcessingText
                }

            };

            // Fields details
            foreach (var field in formSection?.FormPages)
            {
                // Field type as Text
                if (field.TemplateId == FormFields.TextType)
                {
                    response.Fields.Add(PrepareFieldFormTypeTextModel(field, supporterDetails));
                }
                // Field Type as Dropdown
                else if (field.TemplateId == FormFields.DropdowneType)
                {
                    response.Fields.Add(PrepareFieldFormTypeDropdownModel(field, supporterDetails));
                }
                // Field Type as Date
                else if (field.TemplateId == FormFields.DateType)
                {
                    response.Fields.Add(PrepareFieldFormTypeDateModel(field, supporterDetails));
                }
                // Field Type as Address
                else if (field.TemplateId == FormFields.AddressType)
                {
                    response.Fields.Add(PrepareFieldFormTypeAddressModel(field, supporterDetails));
                }
                // Field Type as SubmitButton
                else if (field.TemplateId == FormFields.SubmitButtonType)
                {
                    response.Fields.Add(PrepareFieldFormTypeSubmitButtonModel(field));
                }
            }

            #endregion

            return Result.Success(response);
        }

        /// <summary>
        /// Get Profile Tab2 details (ChangePassword)
        /// </summary>
        /// <returns></returns>
        public Result<SupporterProfileApiModel> CreateChangePasswordApiModel()
        {

            var formSection = _contentRepository.GetItem<IProfileFormSection>(Constants.ProfileDetails.Item.Tab2ChangePassword.Guid, Sitecore.Context.Database);
            var loginUserInfo = _sessionService.GetLoginUserInfo();

            // create response
            var response = new SupporterProfileApiModel()
            {
                SmithFamilyId = _supporterService.GetCurrentSupporterTsfId(),
                DonorType = loginUserInfo?.CustomerType.ToString(),
                ProcessingOverlay = new ProcessingOverlay()
                {
                    ProcessingHeading = formSection?.ProcessingHeading,
                    ProcessingLogoImageSrc = formSection?.ProcessingLogoImage?.Src,
                    ProcessingText = formSection?.ProcessingText
                }
            };

            // Fields details
            foreach (var field in formSection?.FormPages)
            {
                // Field type as Text
                if (field.TemplateId == FormFields.TextType)
                {
                    response.Fields.Add(PrepareFieldFormTypeTextModel(field, null));
                }
                // Field Type as SubmitButton
                else if (field.TemplateId == FormFields.SubmitButtonType)
                {
                    response.Fields.Add(PrepareFieldFormTypeSubmitButtonModel(field));
                }
            }

            return Result.Success(response);
        }

        /// <summary>
        /// Update Supporter profile details in CRM. It has two steps.
        /// 1. Validations
        /// 2. Update Profile in CRM.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<Result<UpdateProfileResponse>> UpdateSupporterProfile(UpdateProfileRequest request)
        {
            var response = new UpdateProfileResponse();
            
            // Get fields information from CMS
            var fieldsInfo = GetFieldsDetails(request);

            // Request Data Validation as per CMS validation configuration.
            var validationMessages = ProfileValidation(fieldsInfo, request);
            if (validationMessages != null && validationMessages.Count > 0)
            {
                response.ValidationMessages = validationMessages;
                return Result.Success(response);
            }

            // Convert request to Dto
            var supporterDetailsDto = ConvertToSupporterDetailsDto(request, fieldsInfo);

            // CRM API Call to update profile in CRM
            var result = await _supporterCRMApiClient.SetSupporterDetails(supporterDetailsDto);
            if (result == null || result?.ErrorCodes?.Count > 0) // Failure Message
            {
                _logRepository.Warn("ProfileServiceApi API result is null UpdateSupporterProfile method");
                var validationMessage = GetValidationMessageByApiErrorCode(result?.ErrorCodes);
                if (!validationMessage.Any())
                {
                    validationMessage.Add(GetSaveValidationMessage(fieldsInfo, false));
                }

                response.ValidationMessages = validationMessage;
            }
            else // Success Message 
            {
                response.SuccessValidationMessage = GetSaveValidationMessage(fieldsInfo, true);
                
                // Update CustomerBase session variable if companyname / firstname / email updates
                UpdateSessionVariables(supporterDetailsDto, result);
            }

            return Result.Success(response);
        }

        /// <summary>
        /// Update the password
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public Result<UpdateProfileResponse> UpdatePassword(UpdatePasswordRequest request)
        {
            var response = new UpdateProfileResponse();

            // Get fields information from CMS
            var fieldsInfo = GetFieldsDetails(request);
            // Convert request to Dto
            var updatePasswordDto = ConvertToUpdatePasswordDto(request, fieldsInfo);
            if (fieldsInfo == null || fieldsInfo.Count == 0 || updatePasswordDto == null)
            {
                return Result.Failure<UpdateProfileResponse>("Update Password failed.");
            }

            var user = Sitecore.Security.Accounts.User.Current;
            var isSuccessful = _membershipService.ChangePassword(user.GetDomainAndUsername(), updatePasswordDto.OldPassword, updatePasswordDto.NewPassword);
            if (!isSuccessful) // Failure Message
            {
                // Check MaximumLimit or not. And get message as per that.
                bool reachMaxLimit = false;
                var errorMessage = this.GetErrorMessage(out reachMaxLimit);
                if (reachMaxLimit)
                {
                    this.LogoutUser();
                }

                // Send failure password attempt message.
                if (!string.IsNullOrEmpty(errorMessage))
                {
                    response.ValidationMessages = new List<string>() { errorMessage };
                }
                // Get configured default error message for other failures.
                else
                {
                    _logRepository.Warn("ProfileServiceApi API result is null UpdateSupporterProfile method");
                    return Result.Failure<UpdateProfileResponse>(GetSaveValidationMessage(fieldsInfo, false));
                }
            }
            else // Success Message
            {
                response.SuccessValidationMessage = GetSaveValidationMessage(fieldsInfo, true);
            }

            return Result.Success(response);
        }

        /// <summary>
        /// Logout the user
        /// </summary>
        /// <returns></returns>
        public bool LogoutUser()
        {
            TSF.Foundation.Helpers.Helpers.SessionHelper.Set<int?>(Constants.SessionKeys.UpdatePasswordInvalidIdAttempts, 0);

            var user = Sitecore.Security.Accounts.User.Current;

            return _membershipService.LogoutUser(user.GetLocalName());
        }

        /// <summary>
        /// Get update password failure attempts error messages
        /// </summary>
        /// <param name="reachMax"></param>
        /// <returns></returns>
        public string GetErrorMessage(out bool reachMax)
        {
            int maxAttempts = Sitecore.Configuration.Settings.GetIntSetting("MaxSupporterIdVerificationAttempt", 3);
            var attemptObject = TSF.Foundation.Helpers.Helpers.SessionHelper.Get<int?>(Constants.SessionKeys.UpdatePasswordInvalidIdAttempts);
            int attemptsInt;
            string message = String.Empty;
            if (attemptObject == null)
            {
                attemptsInt = 0;
            }
            else
            {
                attemptsInt = (int)attemptObject;
            }
            attemptsInt++;
            TSF.Foundation.Helpers.Helpers.SessionHelper.Set<int?>(Constants.SessionKeys.UpdatePasswordInvalidIdAttempts, attemptsInt);
            if (attemptsInt >= maxAttempts)
            {
                var messageItem = _contentRepository.GetItem<TSF.Foundation.Content.Models.List_Items.IText>(
                     Constants.ItemIds.ValidationMessages.InvalidPasswordLockout.ToString(), Sitecore.Context.Database.Name);
                message = messageItem?.Value;
                reachMax = true;
            }
            else
            {
                reachMax = false;
                switch (attemptsInt)
                {
                    case 1:
                        var messageItem = _contentRepository.GetItem<TSF.Foundation.Content.Models.List_Items.IText>(
                                Constants.ItemIds.ValidationMessages.InvalidPasswordAttemptOne.ToString(), Sitecore.Context.Database.Name);
                        message = messageItem?.Value;
                        break;
                    case 2:
                        var messageItem2 = _contentRepository.GetItem<TSF.Foundation.Content.Models.List_Items.IText>(
                                Constants.ItemIds.ValidationMessages.InvalidPasswordAttemptTwo.ToString(), Sitecore.Context.Database.Name);
                        message = messageItem2?.Value;
                        break;
                    default:
                        break;
                }
            }

            return message;
        }

        public async Task<Result<CommunicationPreferencesApiModel>> CreateCommunicationPreferencesApiModel(Guid dataSourceID)
        {
            #region Get Tab3 form section from CMS and CRM API Call
            //  Get Tab3 form section guid from CMS 
            //Guid? tab3FormSectionGuid = GetTab3IdFromCms(dataSourceID);
            //if (tab3FormSectionGuid == null)
            //{
            //    return Result.Failure<CommunicationPreferencesApiModel>("CreateCommunicationPreferencesApiModel :CMS Tab3 Value is null (OR) Sitecore User profile SupporterType is null (OR) loginUserInfo session value is null");
            //}
            IProfileFormSection formSection = _contentRepository.GetItem<IProfileFormSection>(dataSourceID);

            // Get CRM API Call to get CRM Input Data
            var loginUserInfo = _sessionService.GetLoginUserInfo();
            var commsPrfersCrmData = await _supporterCRMApiClient.GetCommsPreferences(loginUserInfo.CustomerType, loginUserInfo.CustomerEntityGuid);
            if (commsPrfersCrmData == null)
            {
                return Result.Failure<CommunicationPreferencesApiModel>("CreateCommunicationPreferencesApiModel : Get Communication Preference API response is null");
            }
            #endregion

            #region Response object constructiona
            // create response
            var response = new CommunicationPreferencesApiModel()
            {
                ProcessingOverlay = new ProcessingOverlay()
                {
                    ProcessingHeading = formSection?.ProcessingHeading,
                    ProcessingLogoImageSrc = formSection?.ProcessingLogoImage?.Src,
                    ProcessingText = formSection?.ProcessingText
                }
            };

            // Fields details
            foreach (var field in formSection?.FormPages)
            {
                // Form Sub Section
                if (field.TemplateId == FormFields.FormSubSectionType)
                {
                    response.Fields.Add(PrepareProfileFormSubSectionModel(field, commsPrfersCrmData));
                }
                // Field Type as Communication Break
                else if (field.TemplateId == FormFields.CommsBreakType)
                {
                    response.Fields.Add(PrepareFieldFormTypeCommsBreakModel(field, commsPrfersCrmData));
                }
                // Field Type as Message Type
                else if (field.TemplateId == FormFields.MessageType)
                {
                    response.Fields.Add(PrepareFieldFormTypeMessageModel(field));
                }
                // Field Type as SubmitButton
                else if (field.TemplateId == FormFields.SubmitButtonType)
                {
                    response.Fields.Add(PrepareFieldFormTypeSubmitButtonModel(field));
                }
            }
            #endregion

            // Update Comms Preference ShowNotification to false.
            await UpdteCommsPreferenceBreak(loginUserInfo, commsPrfersCrmData);

            return Result.Success(response);
        }

        public async Task<Result<CommsPreferenceReturn>> UpdateCommsPreferenceBreak()
        {
            // Get CRM API Call to get CRM Input Data
            var loginUserInfo = _sessionService.GetLoginUserInfo();

            // Update Comms Preference ShowNotification to false.
            CommsPreferenceBreakDto commsPreferenceBreak = new CommsPreferenceBreakDto()
            {
                CustomerType = (int)loginUserInfo.CustomerType,
                CustomerEntityGuid = loginUserInfo.CustomerEntityGuid,
                RemoveCommsBreakExpiryDate = true
            };
            var updatedCommsBreak = await _supporterCRMApiClient.UpdateCommsPreferenceBreak(commsPreferenceBreak);

            return updatedCommsBreak;
        }

        public async Task<Result<UpdateProfileResponse>> UpdateCommsPreference(UpdateCommsPrefRequest request)
        {
            // Get CRM API Call to get CRM Input Data
            var loginUserInfo = _sessionService.GetLoginUserInfo();

            CommunicationPreferences crmApiRequest = new CommunicationPreferences()
            {
                CustomerType = (int)loginUserInfo.CustomerType,
                CustomerEntityGuid = loginUserInfo.CustomerEntityGuid.ToString(),
                CommsBreakShowNotification = false,
                CommsPreferences = new List<CommsPreference>()
            };

            if (request.CommunicationBreakLength.Any())
            {
                var commsBreakLength =  request.CommunicationBreakLength.First();
                int.TryParse(commsBreakLength, out int breakLength);
                crmApiRequest.CommsBreakExpiryDate = DateTime.Today.AddMonths(breakLength);
                crmApiRequest.CommsBreakShowNotification = true;
                crmApiRequest.CommsPreferences = new List<CommsPreference>();
            }

            foreach (var field in request.Fields)
            {
                Guid.TryParse(field.CRMValue, out Guid crmValue);

                if (crmValue.Equals(Guid.Empty)) { continue; }

                CommsPreference preference = new CommsPreference();
                preference.CommsPreferenceGuid = crmValue;
                preference.CommsPreferenceItems = new CommsPreferenceItem();

                foreach (var property in preference.CommsPreferenceItems.GetType().GetProperties())
                {
                    var fieldOption = field.FieldOptions?.Where(x => !string.IsNullOrEmpty(x.CRMValue) && x.CRMValue.ToLower().Equals(property.Name.ToLower())).FirstOrDefault();
                    if (fieldOption != null)
                    {
                        property.SetValue(preference.CommsPreferenceItems, fieldOption.Optionvalue);
                    }
                    else
                    {
                        property.SetValue(preference.CommsPreferenceItems, null);
                    }
                }

                crmApiRequest.CommsPreferences.Add(preference);
            }

            // Update Comms Preferences.
            var commsPreferenceResult = await _supporterCRMApiClient.UpdateCommsPreferences(crmApiRequest);

            // Prepare UpdateProfile response
            var response = PrepareUpdateProfileResponse(crmApiRequest);

            return response;
        }



        /// <summary>
        /// Get Profile details from content datasource
        /// </summary>
        /// <returns></returns>
        public Result<IProfileDetails> CreateProfileDetailsModel()
        {
            // Get Form details
            IProfileDetails modelDatasource = _renderingRepository.GetDataSourceItem<IProfileDetails>();
            if (modelDatasource == null)
            {
                _logRepository.Warn("Datasource is null ProfileServiceApi CreateProfileDetailsModel");
                return Result.Failure<IProfileDetails>("CreateProfileDetailsModel : Datasource is null");
            }

            // Get Form Sections details
            var tab1IndividualFormSection = _contentRepository.GetItem<IProfileFormSection>(modelDatasource.IndividualSupporter.Guid);
            var tab1CoporateFormSection = _contentRepository.GetItem<IProfileFormSection>(modelDatasource.CorporateSupporter.Guid);
            var tab2ProfileFormSection = _contentRepository.GetItem<IProfileFormSection>(Constants.ProfileDetails.Item.Tab2ChangePassword.Guid);
            var tsfID = _supporterService.GetCurrentSupporterTsfId();

            Guid? tab3FormSectionId = GetTab3IdFromCms(modelDatasource.Id);
            if (tab3FormSectionId == null)
            {
                _logRepository.Warn("CreateCommunicationPreferencesApiModel :CMS Tab3 Value is null (OR) Sitecore User profile SupporterType is null (OR) loginUserInfo session value is null");
                tab3FormSectionId = Guid.Empty;
            }
            var tab3FormSection = _contentRepository.GetItem<IProfileFormSection>(tab3FormSectionId.Value);

            if (tab1IndividualFormSection != null)
            {
                modelDatasource.Tab1IndividualFormSection = tab1IndividualFormSection;
            }
            if (tab1CoporateFormSection != null)
            {
                modelDatasource.Tab1CorporateFormSection = tab1CoporateFormSection;
            }
            if (tab2ProfileFormSection != null)
            {
                modelDatasource.Tab2ChangePasswordFormSection = tab2ProfileFormSection;
            }
            if (tab3FormSection != null)
            {
                modelDatasource.Tab3CommsPrefFormSection = tab3FormSection;
            }
            if (!string.IsNullOrEmpty(tsfID))
            {
                modelDatasource.TsfID = tsfID;
            }

            // Set current CustomerType
            var loginUserInfo = _sessionService.GetLoginUserInfo();
            modelDatasource.SupporterClass = loginUserInfo.CustomerType; // sitecoreUser.IsBusinessSupporter() ? SupporterClass.BusinessSupporter : SupporterClass.IndividualSupporter;


            return Result.Success(modelDatasource);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Prepare Filed Type Text Model
        /// </summary>
        /// <param name="field"></param>
        /// <param name="supporterDetails"></param>
        /// <returns></returns>
        private FieldFormTypeTextModel PrepareFieldFormTypeTextModel(IFieldForm field, SupporterDetails supporterDetails)
        {
            // Get field details from CMS
            var textField = _contentRepository.GetItem<IFormFieldTypeText>(field.ID.Guid, Sitecore.Context.Database);

            // Field model
            var fieldModel = Mapper.Map<FieldFormTypeTextModel>(textField);
            fieldModel.Validations = GetValidationMessages(textField);
            fieldModel.FieldValue = GetValueByPropertyName(textField.CRMValue, supporterDetails);

            return fieldModel;
        }

        /// <summary>
        /// Prepare Filed Type Dropdown Model
        /// </summary>
        /// <param name="field"></param>
        /// <param name="supporterDetails"></param>
        /// <returns></returns>
        private FieldFormTypeDropdownModel PrepareFieldFormTypeDropdownModel(IFieldForm field, SupporterDetails supporterDetails)
        {
            // Get field details from CMS
            var dropdownField = _contentRepository.GetItem<IFormFieldTypeDropdown>(field.ID.Guid, Sitecore.Context.Database);
            // Convert to OptionSet Model
            List<FieldFormTypeDropdownOptionSetModel> optionset = Mapper.Map<List<FieldFormTypeDropdownOptionSetModel>>(dropdownField?.ListSource?.ListItems?.Where(f => f.ShowInDroplist));

            // Field model
            var fieldModel = Mapper.Map<FieldFormTypeDropdownModel>(dropdownField);
            fieldModel.OptionSet = optionset;
            fieldModel.Validations = GetValidationMessages(dropdownField);
            fieldModel.FieldValue = GetValueByPropertyName(dropdownField?.CRMValue, supporterDetails);

            return fieldModel;
        }

        /// <summary>
        /// Prepare Filed Type Address Model
        /// </summary>
        /// <param name="field"></param>
        /// <param name="supporterDetails"></param>
        /// <returns></returns>
        private FieldFormTypeAddressModel PrepareFieldFormTypeAddressModel(IFieldForm field, SupporterDetails supporterDetails)
        {
            // Get field details from CMS
            var addressField = _contentRepository.GetItem<IFormFieldTypeAddress>(field.ID.Guid, Sitecore.Context.Database);
            // Convert to Countries Model        
            var countryOptionset = Mapper.Map<List<FieldFormTypeDropdownOptionSetModel>>(addressField?.CountryListSource?.Countries);
            // Convert to State Model        
            var stateOptionset = Mapper.Map<List<FieldFormTypeDropdownOptionSetModel>>(addressField?.AUStateListSource?.States);
            // Conver to address Model
            var addressModel = Mapper.Map<FieldFormTypeAddressValueModel>(supporterDetails);
            // Convert from Country Alpha2 code to Country Value
            addressModel.Country = TSF.Foundation.Helpers.CountryHelper.ToAlpha2Code(addressModel.Country);

            // Field model
            var fieldModel = Mapper.Map<FieldFormTypeAddressModel>(addressField);
            fieldModel.Validations = GetValidationMessages(addressField);
            fieldModel.FieldValue = addressModel;
            fieldModel.CountryOptionSet = countryOptionset;
            fieldModel.StateOptionSet = stateOptionset;

            return fieldModel;
        }

        /// <summary>
        /// Prepare Filed Type SubmitButton Model
        /// </summary>
        /// <param name="field"></param>
        /// <param name="supporterDetails"></param>
        /// <returns></returns>
        private FieldFormTypeSubmitButtonModel PrepareFieldFormTypeSubmitButtonModel(IFieldForm field)
        {
            // Get field details from CMS
            var buttonField = _contentRepository.GetItem<IFormFieldTypeSubmitButton>(field.ID.Guid, Sitecore.Context.Database);

            // Field model
            var fieldModel = Mapper.Map<FieldFormTypeSubmitButtonModel>(buttonField);

            return fieldModel;
        }

        private FieldFormTypeCommsBreakModel PrepareFieldFormTypeCommsBreakModel(IFieldForm field, CommunicationPreferences commsPrefersCrmData)
        {
            // Get field details from CMS
            var commsBreakField = _contentRepository.GetItem<IFormFieldTypeCommsBreak>(field.ID.Guid);

            // Field model
            var fieldModel = Mapper.Map<FieldFormTypeCommsBreakModel>(commsBreakField);

            // Set BreakCurrent and ShowBreakExpiredNotification values
            if (commsPrefersCrmData.CommsBreakExpiryDate != null)
            {
                if (commsPrefersCrmData.CommsBreakExpiryDate > DateTime.Today)
                {
                    fieldModel.BreakCurrent = true;
                }
                else if (commsPrefersCrmData.CommsBreakExpiryDate <= DateTime.Today && 
                    commsPrefersCrmData.CommsBreakShowNotification.HasValue  && 
                    commsPrefersCrmData.CommsBreakShowNotification ==  true)
                {
                    fieldModel.ShowBreakExpiredNotification = true;
                }

                #region Replace Notification tokens
                // Prepare tokens to replace tokes in the Notification header text and Title.
                var tokens = new Dictionary<string, string>() {
                    {CoreConstants.Tokens.CommsBreakExpiryDate, commsPrefersCrmData.CommsBreakExpiryDate.HasValue ? commsPrefersCrmData.CommsBreakExpiryDate.TSFDisplayFormat() : String.Empty},
                };

                // Token replace in the Break Current Notification and Break Expired Notification.
                if (fieldModel.BreakCurrentNotification != null)
                {
                    fieldModel.BreakCurrentNotification.Title = fieldModel.BreakCurrentNotification.Title.Replace(tokens);
                    fieldModel.BreakCurrentNotification.Text = fieldModel.BreakCurrentNotification.Text.Replace(tokens);

                    // Set IsAPI to true : FE requires this flag to call API for any buttion action
                    if (fieldModel.BreakCurrentNotification.ButtonLink != null)
                    {
                        fieldModel.BreakCurrentNotification.ButtonLink.IsApi = true;
                    }
                }
                if (fieldModel.BreakExpiredNotification != null)
                {
                    fieldModel.BreakExpiredNotification.Title = fieldModel.BreakExpiredNotification.Title.Replace(tokens);
                    fieldModel.BreakExpiredNotification.Text = fieldModel.BreakExpiredNotification.Text.Replace(tokens);
                }
                #endregion
            }

            return fieldModel;
        }

        private FieldFormTypeMessageModel PrepareFieldFormTypeMessageModel(IFieldForm field)
        {
            // Get field details from CMS
            var messageField = _contentRepository.GetItem<IFormFieldTypeMessage>(field.ID.Guid);

            // Field model
            var fieldModel = Mapper.Map<FieldFormTypeMessageModel>(messageField);

            return fieldModel;
        }

        private ProfileFormSubSectionModel PrepareProfileFormSubSectionModel(IFieldForm field, CommunicationPreferences commsPreferencesCrmData)
        {
            // Get field details from CMS
            var formSubSection = _contentRepository.GetItem<IProfileFormSubSection>(field.ID.Guid);
            // Field model
            var cmsFormSubSectionModel = Mapper.Map<ProfileFormSubSectionModel>(formSubSection);

            // Set OptionalValue and CRMValue of each field with CRMData
            foreach (var cmsSubSectionField in cmsFormSubSectionModel.Fields)
            {
                if (!Guid.TryParse(cmsSubSectionField.CRMValue, out Guid crmVlueGuid)){ continue; }

                // Each SubSection 'CRM Value' is matched with the CRM API 'Comms Preference Control GUID' field
                CommsPreference crmData = commsPreferencesCrmData?.CommsPreferences?.Where(x => x.CommsPreferenceControlGuid == crmVlueGuid).SingleOrDefault();
                if (crmData == null || crmData.CommsPreferenceItems == null) { continue; }

                // Set the CRM API 'Comms Preference GUID' is sent to the FE API 'CRMValue' field.
                cmsSubSectionField.CRMValue = crmData.CommsPreferenceGuid.ToString();
                foreach (var property in crmData.CommsPreferenceItems.GetType().GetProperties())
                {
                    var propertyValue = property.GetValue(crmData.CommsPreferenceItems, null);
                    var cmsSubSectionFieldOption = cmsSubSectionField.FieldOptions?.Where(x => x.CRMValue.ToLower().Equals(property.Name.ToLower())).SingleOrDefault();
                    if (cmsSubSectionFieldOption != null && propertyValue != null)
                    {
                        cmsSubSectionFieldOption.Optionvalue = Convert.ToBoolean(propertyValue);
                    }
                }
                
            }

            return cmsFormSubSectionModel;
        }

        /// <summary>
        /// Get CRM Value by property name
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="supporterDetails"></param>
        /// <returns></returns>
        private string GetValueByPropertyName(string propertyName, SupporterDetails supporterDetails)
        {
            string finalValue = string.Empty;

            if (string.IsNullOrEmpty(propertyName) || supporterDetails == null)
            {
                return string.Empty;
            }

            var value =  supporterDetails.GetType().GetProperty(propertyName).GetValue(supporterDetails, null);

            if (value != null)
            {
                if (value.GetType().Equals(Guid.Empty.GetType()))
                {
                    Guid.TryParse(value.ToString(), out Guid result);
                    finalValue = result != Guid.Empty ? result.ToString() : null;
                }
                else
                {
                    finalValue = value.ToString();
                }
            }

            return finalValue;            
        }

        /// <summary>
        /// Prepare Filed Type Address Model
        /// </summary>
        /// <param name="field"></param>
        /// <param name="supporterDetails"></param>
        /// <returns></returns>
        private FieldFormTypeDateModel PrepareFieldFormTypeDateModel(IFieldForm field, SupporterDetails supporterDetails)
        {
            // Get field details from CMS
            var dateField = _contentRepository.GetItem<IFormFieldTypeDate>(field.ID.Guid, Sitecore.Context.Database);

            var fieldValue = GetValueByPropertyName(dateField.CRMValue, supporterDetails);

            DateTime.TryParse(fieldValue, out DateTime dob);

            // Field model
            var fieldModel = Mapper.Map<FieldFormTypeDateModel>(dateField);
            fieldModel.Validations = GetValidationMessages(dateField);
            fieldModel.FieldValue = dob != default(DateTime) ? dob.ToString(ExtensionConstants.FamilyPortalDateTimeGeneralDisplayFormat) : string.Empty;

            return fieldModel;
        }

        /// <summary>
        /// Prepare Validation Messages (Required/Regex/TextMap types)
        /// </summary>
        /// <param name="fieldForm"></param>
        /// <returns></returns>
        private List<ValidationMessageModel> GetValidationMessages(IFieldFormType fieldForm)
        {
            var validationMessages = new List<ValidationMessageModel>();

            ValidationMessageModel model = null;
            foreach (var message in fieldForm?.FieldValidations)
            {
                // Required Validation Message Type
                if(message.TemplateId == GlobalValidationMessages.Templates.RequiredValidation)
                {
                    var requiredValidation = _contentRepository.GetItem<IRequiredValidation>(message.ID.Guid, Sitecore.Context.Database);

                    model = Mapper.Map<RequiredValidationModel>(requiredValidation);
                }
                // Regex Validation Message Type
                else if (message.TemplateId == GlobalValidationMessages.Templates.RegexValidation)
                {
                    var regexValidation = _contentRepository.GetItem<IRegexValidation>(message.ID.Guid, Sitecore.Context.Database);
                    model = Mapper.Map<RegexdValidationModel>(regexValidation);
                }
                // TextMap Validation Message Type
                else if (message.TemplateId == GlobalValidationMessages.Templates.TextMapValidation)
                {
                    var textMapValidation = _contentRepository.GetItem<ITextMapValidation>(message.ID.Guid, Sitecore.Context.Database);
                    model = Mapper.Map<TextMapValidationModel>(textMapValidation);
                }

                validationMessages.Add(model);
            }

            return validationMessages;
        }

        /// <summary>
        /// Input Request validation as per CMS configured validations.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        private List<string> ProfileValidation(List<IProfileFieldForm> fieldForms, UpdateProfileRequest request)
        {
            var failedValidations = new List<string>();
            var maximumValidationString = string.Empty;

            // Get Maximum Validation String
            Sitecore.Data.ID parentID = fieldForms.FirstOrDefault()?.SitecoreItem?.ParentID;
            if (!Sitecore.Data.ID.IsNullOrEmpty(parentID))
            {
                var parentItem = _contentRepository.GetItem<IProfileFormSection>(parentID.Guid);
                maximumValidationString = parentItem?.MaximumFieldLengthValidation?.Value;
            }

            foreach (var field in fieldForms)
            {
                var message = string.Empty;

                // Check field is mandatory or not
                var isRequiredFiled = IsFieldRequired(field);
                // Get field value from input request
                var fieldValue = GetValueByCmsId(request, field.ID.Guid);

                if (field.TemplateId == FormFields.TextType)
                {
                    message = FieldValidation(field, fieldValue, maximumValidationString, isRequiredFiled);
                }
                else if (field.TemplateId == FormFields.DateType)
                {
                    // Input DateTimeFormatAs ddMMYYYY
                    DateTime.TryParseExact(fieldValue.ToString(), ExtensionConstants.FamilyPortalDateTimeGeneralDisplayFormat, new CultureInfo("en-AU"), DateTimeStyles.None, out DateTime date);

                    if (date == default(DateTime) && isRequiredFiled)
                    {
                        message = field.FieldValidations?.FirstOrDefault(f => f.TemplateId == GlobalValidationMessages.Templates.RequiredValidation)?.Value;
                    }
                }
                else if (field.TemplateId == FormFields.AddressType)
                {
                    fieldValue = GetValueByCmsId(request, field.ID.Guid, true);

                    message = AddressValidation(field, fieldValue, isRequiredFiled);
                }

                if (!string.IsNullOrEmpty(message))
                {
                    failedValidations.Add(string.Format(Constants.ProfileDetails.ValidationMessageFormat, field.FieldLabel, message));
                }
            }

            return failedValidations;
        }

        /// <summary>
        /// Check field is required or not
        /// </summary>
        /// <param name="field"></param>
        /// <returns></returns>
        private bool IsFieldRequired(IProfileFieldForm field)
        {
            return field?.FieldValidations?.FirstOrDefault(f => f.TemplateId == GlobalValidationMessages.Templates.RequiredValidation) != null;
        }

        /// <summary>
        /// Validate as per CMS configuration (Required and Regex) and MaximumFieldLength.
        /// </summary>
        /// <param name="field"></param>
        /// <param name="fieldValue"></param>
        /// <param name="maximumValidationString"></param>
        /// <returns></returns>
        private string FieldValidation(IProfileFieldForm field, object fieldValue, string maximumValidationString, bool isRequiredField)
        {
            string message = string.Empty;

            // Check Maximum fild length validation (if MaximumFieldLength has a value then fieldvalue should less than or equal to MaximumFieldLength)
            if (!string.IsNullOrEmpty(field.MaximumFieldLength) && fieldValue.ToString().Trim().Length > Convert.ToInt32(field.MaximumFieldLength))
            {
                message = maximumValidationString;
            }

            // CMS Configured Validations
            foreach (var validation in field.FieldValidations)
            {
                // Required field Validation (field is mandatory + required field validation exists, then field should has some value)
                if (validation.TemplateId == GlobalValidationMessages.Templates.RequiredValidation && isRequiredField && string.IsNullOrEmpty(fieldValue.ToString().Trim()))
                {
                    message = validation.Value;
                    break;
                }
                // Regex Validations
                else if (validation.TemplateId == GlobalValidationMessages.Templates.RegexValidation && fieldValue.ToString().Trim().Length > 0 && !string.IsNullOrEmpty(validation.RegexPattern.Trim()))
                {
                    var regex = new Regex(validation.RegexPattern.Trim());
                    if (!regex.IsMatch(fieldValue.ToString()))
                    {
                        message = validation.Value;
                        break;
                    }
                }
            }

            return message;
        }

        /// <summary>
        /// Convert input request to Dto
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        private SupporterDetailsDto ConvertToSupporterDetailsDto(UpdateProfileRequest request, List<IProfileFieldForm> fieldForms)
        {
            var supporterDetailsDto = new SupporterDetailsDto();

            foreach (var reqField in request.Fields)
            {
                var cmsField = fieldForms.FirstOrDefault(f => f.ID.Guid == reqField.FieldId);
                // Field Type as Address
                if (cmsField != null && cmsField.TemplateId == FormFields.AddressType)
                {
                    MapAddressToSupporterDetailsDto(reqField.AddressFieldValue, supporterDetailsDto);
                }
                // Other Field Types
                else
                {
                    SetValueByPropertyName(cmsField?.CRMValue, reqField.FieldValue, supporterDetailsDto);
                }
            }

            // Get logged-in user context.
            var loginUserInfo = _sessionService.GetLoginUserInfo();


            supporterDetailsDto.CustomerEntityGuid = loginUserInfo.CustomerEntityGuid;
            supporterDetailsDto.CustomerEntityType = (int)loginUserInfo.CustomerType;
            supporterDetailsDto.CustomerType = loginUserInfo.CustomerType;

            return supporterDetailsDto;
        }

        /// <summary>
        /// Map address object to Dto
        /// </summary>
        /// <param name="addressModel"></param>
        /// <param name="supporterDetailsDto"></param>
        private void MapAddressToSupporterDetailsDto(FieldFormTypeAddressValueModel addressModel, SupporterDetailsDto supporterDetailsDto)
        {
            // Convert from Country value to Alpha2 code.
            supporterDetailsDto.Country = TSF.Foundation.Helpers.CountryHelper.ToConnectValue(addressModel.Country);

            supporterDetailsDto.PostalAddressLine1 = addressModel.Address1;
            supporterDetailsDto.PostalAddressLine2 = addressModel.Address2;
            supporterDetailsDto.PostalAddressLine3 = addressModel.Address3;
            supporterDetailsDto.Suburb = addressModel.Suburb;
            supporterDetailsDto.State = addressModel.State;
            supporterDetailsDto.PostCode = addressModel.Postcode;
        }

        /// <summary>
        /// Set Value by Property name 
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="propertyValue"></param>
        /// <param name="supporterDetails"></param>
        private void SetValueByPropertyName(string propertyName, string propertyValue, SupporterDetailsDto supporterDetails)
        {
            if (string.IsNullOrEmpty(propertyName) || supporterDetails == null)
            {
                return;
            }

            // Find property
            var property = supporterDetails.GetType().GetProperty(propertyName);

            // Convert value as per type of property.
            if (property.PropertyType == typeof(Guid)) // Type is Guid
            {
                Guid.TryParse(propertyValue, out Guid result);
                supporterDetails.GetType().GetProperty(propertyName).SetValue(supporterDetails, result);
            }
            else if (property.PropertyType == typeof(Nullable<DateTime>)) // Type is DateTime
            {
                // Input DateTimeFormatAs ddMMYYYY
                DateTime.TryParseExact(propertyValue, ExtensionConstants.FamilyPortalDateTimeGeneralDisplayFormat, new CultureInfo("en-AU"), DateTimeStyles.None, out DateTime result);

                if (result != default(DateTime))
                {
                    supporterDetails.GetType().GetProperty(propertyName).SetValue(supporterDetails, result);
                }
            }
            else // Type is String
            {
                supporterDetails.GetType().GetProperty(propertyName).SetValue(supporterDetails, propertyValue);
            }
        }

        /// <summary>
        /// Get Value by ID
        /// </summary>
        /// <param name="request"></param>
        /// <param name="fieldCmsId"></param>
        /// <returns></returns>
        private object GetValueByCmsId(UpdateProfileRequest request, Guid fieldCmsId, bool isAddressValueRequired = false)
        {
            var field = request?.Fields?.FirstOrDefault(f => f.FieldId == fieldCmsId);

            return isAddressValueRequired ? (object)field?.AddressFieldValue : field?.FieldValue;
        }

        private string AddressValidation(IProfileFieldForm field, object fieldValue, bool isRequiredField)
        {
            var message = string.Empty;

            if (fieldValue  == null || !isRequiredField) { return message; }

            var address = (FieldFormTypeAddressValueModel)fieldValue;

            if ((string.IsNullOrEmpty(address.Address1) || string.IsNullOrEmpty(address.Suburb) || string.IsNullOrEmpty(address.Postcode) || string.IsNullOrEmpty(address.State) || string.IsNullOrEmpty(address.Country)))
            {
                message = field?.FieldValidations?.FirstOrDefault(f => f.TemplateId == GlobalValidationMessages.Templates.RequiredValidation)?.Value;
            }

            return message;
        }

        /// <summary>
        /// Get Validation message for Save action
        /// </summary>
        /// <param name="fieldsInfo"></param>
        /// <param name="isSuccessValidationMessage"></param>
        /// <returns></returns>
        private string GetSaveValidationMessage(List<IProfileFieldForm> fieldsInfo, bool isSuccessValidationMessage)
        {
            string message = string.Empty;

            Sitecore.Data.ID parentID = fieldsInfo?.FirstOrDefault()?.SitecoreItem?.ParentID;
            if (!Sitecore.Data.ID.IsNullOrEmpty(parentID))
            {
                var parentItem = _contentRepository.GetItem<IProfileFormSection>(parentID.Guid);
                if (parentItem == null) { return message; }

                var item = parentItem?.SitecoreItem?.GetChildren().FirstOrDefault(x => x.TemplateID == FormFields.SubmitButtonType);
                if (item == null) { return message; }

                var buttonField = _contentRepository.GetItem<IFormFieldTypeSubmitButton>(item.ID.Guid, Sitecore.Context.Database);
                if (buttonField == null || buttonField.SuccessValidationMessage == null || buttonField.FailureValidationMessage == null) { return message; }

                if (isSuccessValidationMessage)
                {
                    message = _contentRepository.GetItem<IRequiredValidation>(buttonField.SuccessValidationMessage.ID.Guid, Sitecore.Context.Database)?.Value;
                }
                else
                {
                    message = _contentRepository.GetItem<IRequiredValidation>(buttonField.FailureValidationMessage.ID.Guid, Sitecore.Context.Database)?.Value;
                }
            }

            return message;
        }

        private List<IProfileFieldForm> GetFieldsDetails(UpdatePasswordRequest request)
        {
            var fieldForms = new List<IProfileFieldForm>();

            foreach (var field in request?.Fields)
            {
                var item = _contentRepository.GetItem<IProfileFieldForm>(field.FieldId, Sitecore.Context.Database);

                fieldForms.Add(item);
            }

            return fieldForms;
        }

        /// <summary>
        /// Get Fields Information
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        private List<IProfileFieldForm> GetFieldsDetails(UpdateProfileRequest request)
        {
            var fieldForms = new List<IProfileFieldForm>();

            foreach (var field in request?.Fields)
            {
                var item = _contentRepository.GetItem<IProfileFieldForm>(field.FieldId, Sitecore.Context.Database);

                fieldForms.Add(item);
            }

            return fieldForms;
        }

        private UpdatePasswordDto ConvertToUpdatePasswordDto(UpdatePasswordRequest request, List<IProfileFieldForm> fieldForms)
        {
            var updatePasswordDto = new UpdatePasswordDto();

            foreach (var reqField in request.Fields)
            {
                var cmsField = fieldForms.FirstOrDefault(f => f.ID.Guid == reqField.FieldId);
                updatePasswordDto.GetType().GetProperty(cmsField.CRMValue).SetValue(updatePasswordDto, reqField.FieldValue);
            }

            return updatePasswordDto;
        }

        /// <summary>
        /// Update CustomerBase session variable if companyname/firstname/email updates
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        private void UpdateSessionVariables(SupporterDetailsDto request, SupporterDetailsDto response)
        {
            if ((!string.IsNullOrEmpty(response.CompanyName) && response.CompanyName.ToLower().Equals(request.CompanyName.ToLower())) ||
                 (!string.IsNullOrEmpty(response.FirstName) && response.FirstName.ToLower().Equals(request.FirstName.ToLower())) ||
                 (!string.IsNullOrEmpty(response.EmailAddress) && response.EmailAddress.ToLower().Equals(request.EmailAddress.ToLower()))
                )
            {
                // Update CustomerBase Session Variable
                _sessionService.GetLoginUserInfo(true);
            }
        }

        /// <summary>
        /// Get validation message based on CRM API's ErrorCodes
        /// </summary>
        /// <param name="errorCodes"></param>
        /// <returns></returns>
        private List<string> GetValidationMessageByApiErrorCode(List<int> errorCodes)
        {
            List<string> messages = new List<string>();

            foreach (var errorcode in errorCodes)
            {
               string message = string.Empty;
               if (errorcode == (int)ErrorCode.DuplicateEmailError)
                {
                   message = _contentRepository.GetItem<IRequiredValidation>(ProfileDetails.Item.Tab1DuplicateEmail.Guid)?.Value;
                }

               if (!string.IsNullOrEmpty(message))
                {
                    messages.Add(message);
                }
            }

            return messages;
        }

        private Guid? GetTab3IdFromCms(Guid dataSourceID)
        {
            Guid? tab3FormSectionGuid = null;

            //Get the supporter type facet of the current logged in user :
            if (!Enum.TryParse<SupporterType>(User.Current.Profile.GetCustomProperty(CoreConstants.Membership.CustomProperties.SupporterType), out SupporterType supporterType))
            {
                _logRepository.Warn("CreateCommunicationPreferencesApiModel - Sitecore User profile SupporterType is null");
                return tab3FormSectionGuid;
            }

            // CRM Input Data
            var loginUserInfo = _sessionService.GetLoginUserInfo();
            if (loginUserInfo == null) { return tab3FormSectionGuid; }

            // Get DataSource 
            IProfileDetails dataSource = _contentRepository.GetItem<IProfileDetails>(dataSourceID);

            if (loginUserInfo.CustomerType == SupporterClass.IndividualSupporter)
            {
                switch (supporterType)
                {
                    case SupporterType.Sponsor:
                        tab3FormSectionGuid = dataSource.IndividualSponsor?.Guid;
                        break;
                    case SupporterType.RegularDonor:
                        tab3FormSectionGuid = dataSource.IndividualRegularGiver?.Guid;
                        break;
                    case SupporterType.CashDonor:
                        tab3FormSectionGuid = dataSource.IndividualSingleDonor?.Guid;
                        break;
                }
            }
            else if (loginUserInfo.CustomerType == SupporterClass.BusinessSupporter)
            {
                switch (supporterType)
                {
                    case SupporterType.Sponsor:
                        tab3FormSectionGuid = dataSource.CorporateSponsor?.Guid;
                        break;
                    case SupporterType.RegularDonor:
                        tab3FormSectionGuid = dataSource.CorporateRegularGiver?.Guid;
                        break;
                    case SupporterType.CashDonor:
                        tab3FormSectionGuid = dataSource.CorporateSingleDonor?.Guid;
                        break;
                }
            }

            return tab3FormSectionGuid;
        }

        private async Task UpdteCommsPreferenceBreak(CustomerBase loginUserInfo, CommunicationPreferences commsPrfersCrmData)
        {
            if (commsPrfersCrmData.CommsBreakExpiryDate <= DateTime.Today &&
                    commsPrfersCrmData.CommsBreakShowNotification.HasValue &&
                    commsPrfersCrmData.CommsBreakShowNotification == true)
            {
                CommsPreferenceBreakDto commsPreferenceBreak = new CommsPreferenceBreakDto()
                {
                    CustomerType = (int)loginUserInfo.CustomerType,
                    CustomerEntityGuid = loginUserInfo.CustomerEntityGuid,
                    CommsBreakShowNotification = false
                };

                await _supporterCRMApiClient.UpdateCommsPreferenceBreak(commsPreferenceBreak);
            }
        }

        private UpdateProfileResponse PrepareUpdateProfileResponse(CommunicationPreferences crmApiRequest)
        {
            string successMessage = _contentRepository.GetItem<IRequiredValidation>(Constants.ProfileDetails.Item.Tab3SaveSuccessMessage.Guid)?.Value;
            bool breakCurrent = false;
            if (crmApiRequest.CommsBreakExpiryDate != null && crmApiRequest.CommsBreakExpiryDate > DateTime.Today)
            {
                breakCurrent = true;
            }

            return new UpdateProfileResponse() { SuccessValidationMessage = successMessage, BreakCurrent = breakCurrent };
        }
        #endregion
    }
}