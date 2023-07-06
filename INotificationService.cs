using CSharpFunctionalExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using TSF.Feature.SupporterPortalMvc.Models.Domain;
using TSF.Feature.SupporterPortalMvc.Models.Dto;

namespace TSF.Feature.SupporterPortalMvc.Services
{
    public interface INotificationService
    {
        Result<IPortalNotification> CreatePortalNotificationModel(Guid participantId);
        Task<Result<NotificationApiModel>> GetSupporterNotifications(Guid portalNotificationDatasourceId, Guid participantId);
        Task<Result> DismissNotification(DismissedNotificationDto dismissedNotification);
        Result<NotificationItemApiModel> RegisterSupporterToFamilyPortal();
    }
}