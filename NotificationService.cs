using CSharpFunctionalExtensions;
using Glass.Mapper.Sc.Fields;
using Sitecore;
using Sitecore.Collections;
using Sitecore.Diagnostics;
using Sitecore.Security.Accounts;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using TSF.Feature.SupporterPortalMvc.Models.Domain;
using TSF.Feature.SupporterPortalMvc.Models.Dto;
using TSF.Feature.SupporterPortalMvc.Repositories;
using TSF.Foundation.Content.Models;
using TSF.Foundation.Content.Repositories;
using TSF.Foundation.Core;
using TSF.Foundation.CRM.Extensions;
using TSF.Foundation.CRM.Types;
using TSF.Foundation.CRM_D365.Enums;
using TSF.Foundation.CRM_D365.Models.Domain;
using TSF.Foundation.Email.Helpers;
using TSF.Foundation.Extensions;
using TSF.Foundation.Helpers;
using TSF.Foundation.Logging.Repositories;
using TSF.Foundation.Portal.Extensions;
using TSF.Foundation.Portal.Models;
using TSF.Foundation.Portal.Models.Session;
using TSF.Foundation.Portal.Services;
using TSF.Foundation.SupporterPortal.ApiClient;
using TSF.Foundation.SupporterPortal.Models.Domain;
using TSF.Foundation.SupporterPortal.Models.Dto;

namespace TSF.Feature.SupporterPortalMvc.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ILogRepository _logRepository;
        private readonly IContentRepository _contentRepository;
        private readonly IRenderingRepository _renderingRepository;
        private readonly ISupporterPortalMvcRepository _supporterPortalMvcRepository;
        private readonly IUserService _userService;
        private readonly IFamilyPortalService _familyPortalService;
        private readonly ISupporterCRMApiClient _supporterCRMApiClient;
        private readonly ISessionService _sessionService;

        private const string _dismissedNotificationSessionId = "SESSION_DISMISSED_NOTIFICATIONS";

        private INotificationTypeSettings _notificationTypeSettings;
        private INotificationTypeSettings NotificationTypeSettings
        {
            get
            {
                if(_notificationTypeSettings == null)
                    _notificationTypeSettings = _contentRepository.GetItem<INotificationTypeSettings>(Constants.ItemIds.NotificationTypeFolderID.Guid, Sitecore.Context.Database);

                return _notificationTypeSettings;
            }
        }

        public NotificationService(ILogRepository logRepository,
            IContentRepository contentRepository,
            IRenderingRepository renderingRepository,
            ISupporterPortalMvcRepository supporterPortalMvcRepository,
            IUserService userService,
            IFamilyPortalService familyPortalService,
            ISupporterCRMApiClient supporterCRMApiClient,
            ISessionService sessionService)
        {
            _logRepository = logRepository;
            _contentRepository = contentRepository;
            _renderingRepository = renderingRepository;
            _supporterPortalMvcRepository = supporterPortalMvcRepository;
            _userService = userService;
            _familyPortalService = familyPortalService;
            _supporterCRMApiClient = supporterCRMApiClient;
            _sessionService = sessionService;
        }

        public Result<IPortalNotification> CreatePortalNotificationModel(Guid participantId)
        {
            IPortalNotification modelDatasource = _renderingRepository.GetDataSourceItem<IPortalNotification>();
            if (modelDatasource == null)
            {
                _logRepository.Warn("Datasource is null SupporterPortalMvcService CreatePortalNotificationModel");
                return Result.Failure<IPortalNotification>("CreatePortalNotificationModel : Datasource is null");
            }

            modelDatasource.ParticipantId = participantId;

            var renderingParams = _renderingRepository.GetRenderingParameters<IHideModuleHeadingParam>();
            if (renderingParams == null)
            {
                _logRepository.Warn($"Rendering Parameters are null SupporterPortalMvcService CreatePortalNotificationModel");
            }
            else
            {
                modelDatasource.HideModuleHeading = renderingParams.HideModuleHeading;
            }

            return Result.Success(modelDatasource);
        }

        public async Task<Result<NotificationApiModel>> GetSupporterNotifications(Guid portalNotificationDatasourceId, Guid participantId)
        {
            //Get the datasource item
            var portalNotification = _contentRepository.GetItem<IPortalNotification>(portalNotificationDatasourceId, Sitecore.Context.Database);

            if (portalNotification == null)
            {
                _logRepository.Warn($"The portal notification datasource (id: {portalNotificationDatasourceId}) doesn't exist");
                return Result.Failure<NotificationApiModel>("GetSupporterNotifications : Datasource is null");
            }

            var model = new NotificationApiModel()
            {
                SummaryLimit = portalNotification.NumberOfSummaryLimit,
                ProcessingOverlay = new ProcessingOverlay
                {
                    ProcessingHeading = portalNotification.ProcessingHeading,
                    ProcessingText = portalNotification.ProcessingText,
                    ProcessingLogoImageSrc = portalNotification.ProcessingLogoImage?.Src
                }
            };

            //Get the list of all available notification types
            var notificationTypes = _supporterPortalMvcRepository.GetNotificationTypes();

            //Get the crm notification id from the selected notification types
            var notificationTypeIds = portalNotification.NotificationTypes
                                        .Select(t => notificationTypes.FirstOrDefault(n => n.Id == t))
                                        .Where(n => n != null)
                                        .Select(t => t.CrmNotification)
                                        .ToList();

            //Check whether the annual renewal notifications need to be retrieved from CRM
            var selectedTypes = notificationTypes.Where(t => portalNotification.NotificationTypes.Any(n => n == t.Id));

            //If any notifications selected are Annual Renewal items - we pass that flag to the API to get the appropriate notifications back.
            var includeAnnualRenewal = selectedTypes.Any(t => t.IsAnnualRenewal);
            
            var loginUserInfo = _sessionService.GetLoginUserInfo();
            if (loginUserInfo == null)
            {
                _logRepository.Warn("GetSupporterNotifications:Can't find session variable");
                return Result.Failure<NotificationApiModel>("GetSupporterNotifications: CustomerEntityGuid and CustomerType for supporter can't be found");
            }

            //Get the number of days for renewal notification from the supporter portal settings
            ISupporterPortalSettings portalSettings = _contentRepository.GetItem<ISupporterPortalSettings>(CoreConstants.IDs.SupporterPortalSettings.Guid.ToString(), Sitecore.Context.Database.Name);
            int numberOfDaysAnnualRenewal = portalSettings.RenewalNotificationDays;

            //Get the notifications from the CRM API
            var crmNotifications = await _supporterCRMApiClient.GetSupporterNotifications(loginUserInfo.CustomerEntityGuid, loginUserInfo.CustomerType, participantId, 
                includeAnnualRenewal, numberOfDaysAnnualRenewal, notificationTypeIds);

            //Filter out the dismissed notifications
            crmNotifications = FilterOutDismissedCrmNotification(crmNotifications);

            var notifications = MapNotificationItems(crmNotifications, notificationTypes);

            //Get the additional account / family portal registration notification if the user is eligible
            var additionalAccountNotification = GetAdditionalAccountNotification();
            if(additionalAccountNotification != null)
            {
                //Insert the notification below the annual renewal notification, if any, or to the top of the list
                if (notifications.Any(n => n.IsAnnualRenewal))
                    notifications.Insert(1, additionalAccountNotification);
                else
                    notifications.Insert(0, additionalAccountNotification);
            }

            //Filter out Notifications marked as 'multiple' - Only 1 displays (the first)
            //Generally for annual renewal notifications
            notifications = FilterOutMultipleNotifications(notifications);

            model.Items = notifications.ToList();

            return Result.Success(model);
        }

        public async Task<Result> DismissNotification(DismissedNotificationDto dismissedNotification)
        {
            //Get the list of dismissed notifications from the session object
            List<DismissedNotificationDto> dismissedNotifications = (List<DismissedNotificationDto>) HttpContext.Current.Session[_dismissedNotificationSessionId];

            //Initialise the list if the session object is empty
            if (dismissedNotifications == null)
                dismissedNotifications = new List<DismissedNotificationDto>();

            //For dismissed annual renewal notification, participantId is irrelevant
            if (dismissedNotification.NotificationId == Guid.Empty)
                dismissedNotification.ParticipantId = Guid.Empty;

            //Add the dismissed notification to the dismissed list if the list empty or if it doesn't exists in the list
            if (!dismissedNotifications.Any() || !dismissedNotifications.Any(n => n.NotificationId == dismissedNotification.NotificationId 
            && n.ParticipantId == dismissedNotification.ParticipantId))
            {
                dismissedNotifications.Add(dismissedNotification);

                HttpContext.Current.Session[_dismissedNotificationSessionId] = dismissedNotifications;
            }

            //Exit if the notification id is empty, no need to update the CRM
            //NotificationId == Guid.Empty --> Annual Renewal notification or Add Portal notification
            if (dismissedNotification.NotificationId == Guid.Empty)
                return Result.Success();

            //Check whether the ActionDisablesNotification setting of type of the notification
            var notificationType = _contentRepository.GetItem<ISupporterNotificationType>(dismissedNotification.NotificationTypeId, Sitecore.Context.Database);
            if (notificationType?.ActionDisablesNotification != true)
                return Result.Success();    //Exit if the ActionDisablesNotification is false

            //Update the status in CRM
            var notificationStatusUpdates = await _supporterCRMApiClient.SetNotificationStatuses(new List<NotificationStatusDto> { new NotificationStatusDto{
                NotificationGuid = dismissedNotification.NotificationId,
                NotificationStatus = NotificationStatus.Cancelled
            } });

            if(!notificationStatusUpdates.Any(u => u.EntityGuid == dismissedNotification.NotificationId && u.NotificationStatus == NotificationStatus.Cancelled))
            {
                //log
                _logRepository.Warn($"The dismissed notification (id:{dismissedNotification.NotificationId} is still not canceled in CRM");
                return Result.Failure("Failed dismissing the notification");
            }

            return Result.Success();
        }

        public Result<NotificationItemApiModel> RegisterSupporterToFamilyPortal()
        {
            NotificationItemApiModel notification = null;

            try
            {
                if (Sitecore.Context.User.IsAuthenticated)
                {
                    // Get login info from session
                    CustomerBase userInfo = _sessionService.GetLoginUserInfo();
                    if (userInfo != null)
                    {
                        CustomerSummary customerSummary = AutoMapper.Mapper.Map<CustomerBase, CustomerSummary>(userInfo);

                        // Check eligibility of login user for family portal. AND
                        // Register user to family portal.
                        if (_familyPortalService.GetCurrentContactIfEligible(customerSummary) && _familyPortalService.RegisterForPortal(Sitecore.Context.User, customerSummary, SupporterClass.Family))
                        {
                            var bodyReplaces = new Hashtable
                            {
                                {CoreConstants.Tokens.SupporterFullName, userInfo.LoggedInName}
                            };

                            // inform the user by sending an email.
                            EmailHelper.SendEmail(CoreConstants.Items.AccountsLinkageConfirmationEmail,
                                bodyReplaces,
                                userInfo.Email,
                                true);

                            notification = GetAdditionalAccountNotificationResponse(true);

                            if (notification == null)
                                return Result.Failure<NotificationItemApiModel>("");

                            return Result.Success(notification);
                        }

                    }
                }

                notification = GetAdditionalAccountNotificationResponse(false);
                return Result.Success(notification);
            }
            catch (Exception ex)
            {
                _logRepository.Error($"[RegisterSupporterToFamilyPortal] Error: {ex.Message}", ex);

                notification = GetAdditionalAccountNotificationResponse(false);
                return Result.Success(notification);
            }
        }

        private List<CRMNotification> FilterOutDismissedCrmNotification(List<CRMNotification> crmNotifications)
        {
            //Get the list of dismissed notifications from the session object
            List<DismissedNotificationDto> dismissedNotifications = (List<DismissedNotificationDto>)HttpContext.Current.Session[_dismissedNotificationSessionId];

            if (dismissedNotifications?.Any() != true)
                return crmNotifications;

            //Exclude notifications that are already dismissed. For annual renewal, only match the notification id (Guid.Empty) and ignore the participant Id
            crmNotifications = crmNotifications.Where(n => !dismissedNotifications.Any(d => d.NotificationId != Guid.Empty ? d.NotificationId == n.NotificationGuid 
                                                        && d.ParticipantId == n.ParticipantGuid : d.NotificationId == n.NotificationGuid))
                                                        .ToList();

            return crmNotifications;
        }

        private bool IsNotificationDismissed(Guid notificationId, Guid participantId)
        {
            List<DismissedNotificationDto> dismissedNotifications = (List<DismissedNotificationDto>)HttpContext.Current.Session[_dismissedNotificationSessionId];

            if (dismissedNotifications?.Any() != true)
                return false;

            return dismissedNotifications.Any(d => d.NotificationId != Guid.Empty ? d.NotificationId == notificationId
                                                        && d.ParticipantId == participantId : d.NotificationId == notificationId);
        }

        private List<NotificationItemApiModel> MapNotificationItems(List<CRMNotification> crmNotifications, List<ISupporterNotificationType> notificationTypes)
        {
            List<NotificationItemApiModel> items = new List<NotificationItemApiModel>();

            int totalChildrenDueRenewalNotifications = crmNotifications.Count(n => n.AnnualRenewalDate != null && n.AnnualRenewalDate != DateTime.MinValue && n.ParticipantGuid!= Guid.Empty);

            foreach (var crmNotification in crmNotifications)
            {
                //First get the notification type definition defined in Sitecore that matches the CRM notification type id
                var notificationType = notificationTypes.FirstOrDefault(n => n.CrmNotification == crmNotification.NotificationType.ToString());
                if (notificationType == null)
                {
                    Log.Warn($"Definition for notification type {crmNotification.NotificationType} doesn't exist in Sitecore", this);
                    continue;
                }

                //Get the replacement values for the allowed tokens
                var tokenDictionary = new Dictionary<string, string>()
                {
                    { CoreConstants.Tokens.ParticipantGuid, crmNotification.ParticipantGuid.ToString("N") },
                    { CoreConstants.Tokens.ParticipantFirstName, crmNotification.ParticipantFirstName },
                    { CoreConstants.Tokens.ParticipantPossessivePronoun, crmNotification.ParticipantGender.PossessivePronoun() },
                    { CoreConstants.Tokens.SupporterFirstName, _userService.GetUserFirstName() },
                    { CoreConstants.Tokens.AnnualRenewalDate, crmNotification.AnnualRenewalDate.TSFDisplayFormat() },
                    { CoreConstants.Tokens.AdditionalChildrenDueRenewal, $"{totalChildrenDueRenewalNotifications - 1}" }
                };

                //Map the notification item
                var notification = MapNotificationItem(notificationType, crmNotification, tokenDictionary);

                items.Add(notification);
            }

            return items;
        }

        private NotificationItemApiModel MapNotificationItem(ISupporterNotificationType notificationType, CRMNotification crmNotification, Dictionary<string, string> tokenDictionary)
        {
            var notification = new NotificationItemApiModel
            {
                Title = notificationType.HeaderText.Replace(tokenDictionary),
                Text = notificationType.Message.Replace(tokenDictionary),
                Id = crmNotification.NotificationGuid,
                ParticipantId = crmNotification.ParticipantGuid,
                NotificationTypeId = notificationType.Id,
                Type = !string.IsNullOrWhiteSpace(notificationType.Class) ? 
                        notificationType.Class : NotificationTypeSettings.DefaultClass.ToString(),
                ActionDisablesNotification = notificationType.ActionDisablesNotification,
                IsAnnualRenewal = notificationType.IsAnnualRenewal,
                IsMultiple = notificationType.IsMultiple,
            };

            SafeDictionary<string> dict = new SafeDictionary<string>();

            //Set the Button Link
            if (!string.IsNullOrWhiteSpace(notificationType.ButtonLinkToAction?.Text) &&
                !string.IsNullOrWhiteSpace(notificationType.ButtonLinkToAction?.Url))
            {
                var buttonLinkUrl = notificationType.ButtonLinkToAction?.BuildUrl(dict)?.Replace(tokenDictionary);
                notification.ButtonLink = new NotificationLink
                {
                    Text = notificationType.ButtonLinkToAction.Text,
                    Url = buttonLinkUrl,
                    Target = notificationType.ButtonLinkToAction.Target
                };
            }

            //Set the Text Link
            if (!string.IsNullOrWhiteSpace(notificationType.TextLinkToAction?.Text) &&
                !string.IsNullOrWhiteSpace(notificationType.TextLinkToAction?.Url))
            {
                var textLinkUrl = notificationType.TextLinkToAction?.BuildUrl(dict)?.Replace(tokenDictionary);
                notification.TextLink = new NotificationLink
                {
                    Text = notificationType.TextLinkToAction.Text,
                    Url = textLinkUrl,
                    Target = notificationType.TextLinkToAction.Target
                };
            }

            return notification;
        }

        private List<NotificationItemApiModel> FilterOutMultipleNotifications(List<NotificationItemApiModel> notificationItems)
        {
            //Get all the annual renewal notifications
            var multipleNotifications = notificationItems.Where(n => n.IsMultiple);

            //Return the original list if there's only one annual renewal
            if (multipleNotifications.Count() == 1)
                return notificationItems;

            //Get the annual renewal notifications to be removed (index 2,...,n)
            var multipleNotificationsToBeRemoved = multipleNotifications.Skip(1);

            //Return the notification list with the duplicate annual renewal removed
            return notificationItems.Except(multipleNotificationsToBeRemoved).ToList();
        }

        private NotificationItemApiModel GetAdditionalAccountNotification()
        {
            //Check the user's eligibility for the family portal
            EligibilityResult isEligibile = _familyPortalService.IsCurrentContactEligibleV2();

            //Return null if not eligible
            if (isEligibile != EligibilityResult.Eligible)
                return null;

            //Get the additional account notification type item
            var notificationType = NotificationTypeSettings.AdditionalAccountNotificationType;

            if (notificationType == null)
                return null;

            //Check whether the notification has been dismissed within the current session
            if (IsNotificationDismissed(notificationType.Id, Guid.Empty))
                return null;

            //Get the replacement values for the allowed tokens
            var tokenDictionary = new Dictionary<string, string>()
                {
                    { CoreConstants.Tokens.SupporterFirstName, _userService.GetUserFirstName() },
                };

            var notification = new NotificationItemApiModel
            {
                Title = notificationType.HeaderText,
                Text = notificationType.Message.Replace(tokenDictionary),
                Id = notificationType.Id,
                ParticipantId = Guid.Empty,
                NotificationTypeId = notificationType.Id,
                Type = !string.IsNullOrWhiteSpace(notificationType.Class) ? 
                    notificationType.Class : NotificationTypeSettings.DefaultClass.ToLower()
            };

            SafeDictionary<string> dict = new SafeDictionary<string>();

            //Set the Button Link
            if (!string.IsNullOrWhiteSpace(notificationType.ButtonLinkToAction?.Text) &&
                !string.IsNullOrWhiteSpace(notificationType.ButtonLinkToAction?.Url))
            {
                var buttonLinkUrl = notificationType.ButtonLinkToAction?.BuildUrl(dict);
                notification.ButtonLink = new NotificationLink
                {
                    Text = notificationType.ButtonLinkToAction.Text,
                    Url = buttonLinkUrl,
                    Target = notificationType.ButtonLinkToAction.Target,
                    IsApi = true
                };
            }

            return notification;
        }

        private NotificationItemApiModel GetAdditionalAccountNotificationResponse(bool IsComplete)
        {
            var notificationType = NotificationTypeSettings.AdditionalAccountNotificationType;

            if (notificationType == null)
                return null;

            var notification = new NotificationItemApiModel
            {
                Title = notificationType.HeaderText,
                Id = notificationType.Id,
                ParticipantId = Guid.Empty,
                NotificationTypeId = notificationType.Id
            };

            Link ButtonLink;

            if(IsComplete)
            {
                notification.Text = notificationType.CompletedMessage;
                notification.Type = !string.IsNullOrWhiteSpace(notificationType.CompletedClass) ?
                    notificationType.CompletedClass : NotificationTypeSettings.DefaultClass;
                ButtonLink = notificationType.CompletedButtonLinkAction;
            }
            else
            {
                notification.Text = notificationType.FailedMessage;
                notification.Type = !string.IsNullOrWhiteSpace(notificationType.FailedClass) ?
                    notificationType.FailedClass : NotificationTypeSettings.DefaultClass;
                ButtonLink = notificationType.FailedButtonLinkAction;
            }

            SafeDictionary<string> dict = new SafeDictionary<string>();

            //Set the Button Link
            if (!string.IsNullOrWhiteSpace(ButtonLink?.Text) &&
                !string.IsNullOrWhiteSpace(ButtonLink?.Url))
            {
                var buttonLinkUrl = ButtonLink?.BuildUrl(dict);
                notification.ButtonLink = new NotificationLink
                {
                    Text = ButtonLink.Text,
                    Url = buttonLinkUrl,
                    Target = ButtonLink.Target,
                    IsApi = !IsComplete
                };
            }

            return notification;
        }
    }
}