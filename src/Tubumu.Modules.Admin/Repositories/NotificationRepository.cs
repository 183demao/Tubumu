﻿using System;
using System.Data.SqlClient;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Tubumu.Modules.Admin.Entities;
using Tubumu.Modules.Framework.Extensions;
using Tubumu.Modules.Framework.Models;
using XM = Tubumu.Modules.Admin.Models;

namespace Tubumu.Modules.Admin.Repositories
{
    public interface INotificationRepository
    {
        Task<Page<XM.NotificationUser>> GetPageAsync(XM.NotificationSearchCriteria criteria);
        Task<bool> SaveAsync(XM.NotificationInput notificationInput, ModelStateDictionary modelState);
        Task<bool> RemoveAsync(int notificationId, ModelStateDictionary modelState);
        Task<bool> ReadAsync(int userId, int[] notificationIds, ModelStateDictionary modelState);
        Task<bool> DeleteAsync(int userId, int[] notificationIds, ModelStateDictionary modelState);
        Task<XM.NotificationUser> GetNewestAsync(int userId, int? currentNotificationId = null);
    }

    public class NotificationRepository : INotificationRepository
    {
        private readonly Expression<Func<Notification, XM.Notification>> _notificationSelector;
        private readonly Expression<Func<Notification, XM.NotificationUser>> _notificationUserSelector;

        private readonly TubumuContext _tubumuContext;

        public NotificationRepository(TubumuContext tubumuContext)
        {
            _tubumuContext = tubumuContext;

            _notificationSelector = m => new XM.Notification
            {
                NotificationId = m.NotificationId,
                FromUser = new XM.UserInfoWarpper
                {
                    UserId = m.FromUserId.HasValue ? m.FromUser.UserId : 0,
                    Username = m.FromUserId.HasValue ? m.FromUser.Username : "",
                    DisplayName = m.FromUserId.HasValue ? m.FromUser.DisplayName : "",
                    HeadUrl = m.FromUserId.HasValue ? m.FromUser.HeadUrl : "",
                    LogoUrl = m.FromUserId.HasValue ? m.FromUser.LogoUrl : "",
                },
                ToUser = new XM.UserInfoWarpper
                {
                    UserId = m.ToUserId.HasValue ? m.ToUser.UserId : 0,
                    Username = m.ToUserId.HasValue ? m.ToUser.Username : "",
                    DisplayName = m.ToUserId.HasValue ? m.ToUser.DisplayName : "",
                    HeadUrl = m.ToUserId.HasValue ? m.ToUser.HeadUrl : "",
                    LogoUrl = m.ToUserId.HasValue ? m.ToUser.LogoUrl : "",
                },
                Title = m.Title,
                Message = m.Message,
                CreationDate = m.CreationDate,
                Url = m.Url,
            };

            _notificationUserSelector = m => new XM.NotificationUser
            {
                NotificationId = m.NotificationId,
                FromUser = new XM.UserInfoWarpper
                {
                    UserId = m.FromUserId.HasValue ? m.FromUser.UserId : 0,
                    Username = m.FromUserId.HasValue ? m.FromUser.Username : "",
                    DisplayName = m.FromUserId.HasValue ? m.FromUser.DisplayName : "",
                    HeadUrl = m.FromUserId.HasValue ? m.FromUser.HeadUrl : "",
                    LogoUrl = m.FromUserId.HasValue ? m.FromUser.LogoUrl : "",
                },
                ToUser = new XM.UserInfoWarpper
                {
                    UserId = m.ToUserId.HasValue ? m.ToUser.UserId : 0,
                    Username = m.ToUserId.HasValue ? m.ToUser.Username : "",
                    DisplayName = m.ToUserId.HasValue ? m.ToUser.DisplayName : "",
                    HeadUrl = m.ToUserId.HasValue ? m.ToUser.HeadUrl : "",
                    LogoUrl = m.ToUserId.HasValue ? m.ToUser.LogoUrl : "",
                },
                Title = m.Title,
                Message = m.Message,
                CreationDate = m.CreationDate,
                Url = m.Url,

                ReadTime = null,
                DeleteTime = null,
            };
        }

        public async Task<Page<XM.NotificationUser>> GetPageAsync(XM.NotificationSearchCriteria criteria)
        {
            if (criteria.ToUserId.HasValue)
            {
                return await GetNotificationUserPageAsync(criteria);
            }

            // 备注：忽略搜索条件的 IsReaded, ToUserId
            // 备注：因为查询所有 ToUserId, 所有不会标记已读未读

            IQueryable<Notification> query = _tubumuContext.Notification;
            if (criteria.FromUserId.HasValue)
            {
                query = query.Where(m => m.FromUserId == criteria.FromUserId);
            }
            if (criteria.Keyword != null)
            {
                var keyword = criteria.Keyword.Trim();
                if (keyword.Length != 0)
                {
                    query = query.Where(m => m.Title.Contains(keyword));
                }
            }
            if (criteria.CreationDateBegin.HasValue)
            {
                var begin = criteria.CreationDateBegin.Value.Date;
                query = query.Where(m => m.CreationDate >= begin);
            }
            if (criteria.CreationDateEnd.HasValue)
            {
                var end = criteria.CreationDateEnd.Value.Date.AddDays(1);
                query = query.Where(m => m.CreationDate < end);
            }

            IOrderedQueryable<Notification> orderedQuery;
            if (criteria.PagingInfo.SortInfo != null && !criteria.PagingInfo.SortInfo.Sort.IsNullOrWhiteSpace())
            {
                orderedQuery = query.Order(criteria.PagingInfo.SortInfo.Sort, criteria.PagingInfo.SortInfo.SortDir == SortDir.DESC);
            }
            else
            {
                // 默认排序
                orderedQuery = query.OrderByDescending(m => m.NotificationId);
            }

            var page = await orderedQuery.Select(_notificationUserSelector).GetPageAsync(criteria.PagingInfo);
            return page;
        }

        private async Task<Page<XM.NotificationUser>> GetNotificationUserPageAsync(XM.NotificationSearchCriteria criteria)
        {
            if (!criteria.ToUserId.HasValue)
            {
                throw new ArgumentNullException(nameof(criteria.ToUserId), "必须输入 ToUserId");
            }
            var userCreationDate = await _tubumuContext.User.AsNoTracking().Where(m => m.UserId == criteria.ToUserId.Value).Select(m => m.CreationDate).FirstOrDefaultAsync();

            // 备注：查询发送给所有人的以及本人的、未删除的记录
            var query1 = from n in _tubumuContext.Notification
                         where n.CreationDate > userCreationDate && (!n.ToUserId.HasValue || n.ToUserId == criteria.ToUserId.Value)
                         select n;

            if (criteria.FromUserId.HasValue)
            {
                query1 = query1.Where(m => m.FromUserId == criteria.FromUserId);
            }
            if (criteria.Keyword != null)
            {
                var keyword = criteria.Keyword.Trim();
                if (keyword.Length != 0)
                {
                    query1 = query1.Where(m => m.Title.Contains(keyword));
                }
            }
            if (criteria.CreationDateBegin.HasValue)
            {
                var begin = criteria.CreationDateBegin.Value.Date;
                query1 = query1.Where(m => m.CreationDate >= begin);
            }
            if (criteria.CreationDateEnd.HasValue)
            {
                var end = criteria.CreationDateEnd.Value.Date.AddDays(1);
                query1 = query1.Where(m => m.CreationDate < end);
            }

            // 剔除已逻辑删除的记录
            var query2 = from m in query1
                         from pu in m.NotificationUser.Where(n => n.UserId == criteria.ToUserId.Value).DefaultIfEmpty()
                         where pu == null || !pu.DeleteTime.HasValue
                         //join pu in m.NotificationUsers.Where(n=>n.UserId == criteria.ToUserId.Value) on m equals pu.Notification into purd
                         //from x in purd.DefaultIfEmpty()
                         //where x == null || !x.DeleteTime.HasValue
                         select new XM.NotificationUser
                         {
                             NotificationId = m.NotificationId,
                             FromUser = new XM.UserInfoWarpper
                             {
                                 UserId = m.FromUserId.HasValue ? m.FromUser.UserId : 0,
                                 Username = m.FromUserId.HasValue ? m.FromUser.Username : "",
                                 DisplayName = m.FromUserId.HasValue ? m.FromUser.DisplayName : "",
                                 HeadUrl = m.FromUserId.HasValue ? m.FromUser.HeadUrl : "",
                                 LogoUrl = m.FromUserId.HasValue ? m.FromUser.LogoUrl : "",
                             },
                             ToUser = new XM.UserInfoWarpper
                             {
                                 UserId = m.ToUserId.HasValue ? m.ToUser.UserId : 0,
                                 Username = m.ToUserId.HasValue ? m.ToUser.Username : "",
                                 DisplayName = m.ToUserId.HasValue ? m.ToUser.DisplayName : "",
                                 HeadUrl = m.ToUserId.HasValue ? m.ToUser.HeadUrl : "",
                                 LogoUrl = m.ToUserId.HasValue ? m.ToUser.LogoUrl : "",
                             },
                             Title = m.Title,
                             Message = m.Message,
                             Url = m.Url,
                             CreationDate = m.CreationDate,

                             ReadTime = pu != null ? pu.ReadTime : null,
                             DeleteTime = pu != null ? pu.DeleteTime : null,
                         };

            if (criteria.IsReaded.HasValue)
            {
                if (criteria.IsReaded.Value)
                {
                    // 备注，读取已读，也可通过用户的 NotificationsToUser 取
                    query2 = query2.Where(m => m.ReadTime.HasValue);
                }
                else
                {
                    query2 = query2.Where(m => !m.ReadTime.HasValue);
                }
            }

            IOrderedQueryable<XM.NotificationUser> orderedQuery;
            if (criteria.PagingInfo.SortInfo != null && !criteria.PagingInfo.SortInfo.Sort.IsNullOrWhiteSpace())
            {
                orderedQuery = query2.Order(criteria.PagingInfo.SortInfo.Sort, criteria.PagingInfo.SortInfo.SortDir == SortDir.DESC);
            }
            else
            {
                // 默认排序
                orderedQuery = query2.OrderByDescending(m => m.NotificationId);
            }

            var page = await orderedQuery.GetPageAsync(criteria.PagingInfo);
            return page;
        }

        public async Task<bool> SaveAsync(XM.NotificationInput notificationInput, ModelStateDictionary modelState)
        {
            User fromUser = null;
            User toUser = null;
            if (notificationInput.FromUserId.HasValue)
            {
                fromUser = await _tubumuContext.User.FirstOrDefaultAsync(m => m.UserId == notificationInput.FromUserId);
                if (fromUser == null)
                {
                    modelState.AddModelError("FromUserId", "无法获取通知发布者");
                    return false;
                }
            }
            if (notificationInput.ToUserId.HasValue)
            {
                toUser = await _tubumuContext.User.FirstOrDefaultAsync(m => m.UserId == notificationInput.ToUserId);
                if (toUser == null)
                {
                    modelState.AddModelError("FromUserId", "无法获取通知接收者");
                    return false;
                }
            }
            Notification itemToSave;
            if (notificationInput.NotificationId.HasValue)
            {
                itemToSave = await _tubumuContext.Notification.FirstOrDefaultAsync(m => m.NotificationId == notificationInput.NotificationId);
                if (itemToSave == null)
                {
                    modelState.AddModelError("FromUserId", "无法获取编辑的记录");
                    return false;
                }
            }
            else
            {
                itemToSave = new Notification
                {
                    FromUser = fromUser,
                    ToUser = toUser,
                    CreationDate = DateTime.Now,
                    Url = notificationInput.Url,
                };

                _tubumuContext.Notification.Add(itemToSave);
            }

            itemToSave.Title = notificationInput.Title;
            itemToSave.Message = notificationInput.Message;

            await _tubumuContext.SaveChangesAsync();

            return true;
        }

        public async Task<bool> RemoveAsync(int notificationId, ModelStateDictionary modelState)
        {
            // 需删除 NotificationUser 的记录

            var sql = "DELETE [NotificationUser] WHERE NotificationId = @NotificationId; DELETE [Notification] WHERE NotificationId = @NotificationId;";
            await _tubumuContext.Database.ExecuteSqlCommandAsync(sql
                , new SqlParameter("NotificationId", notificationId)
                );

            return true;
        }

        public async Task<bool> ReadAsync(int userId, int[] notificationIds, ModelStateDictionary modelState)
        {
            var notifications = await _tubumuContext.Notification.AsNoTracking().Where(m => notificationIds.Contains(m.NotificationId)).
                Select(m => new
                {
                    m.NotificationId,
                    m.ToUserId,
                }).
                ToArrayAsync();
            if (notifications != null && notifications.Any(m => m.ToUserId.HasValue && m.ToUserId != userId))
            {
                modelState.AddModelError("Error", "尝试读取不存在或非发给本人的通知");
                return false;
            }

            // TODO: 批量查询出 NotificationUsers，或以其他方式实现
            foreach (var notification in notifications)
            {
                var notificationUser = await _tubumuContext.NotificationUser.Where(m => m.NotificationId == notification.NotificationId && m.UserId == userId).FirstOrDefaultAsync();
                if (notificationUser == null)
                {
                    var nu = new NotificationUser
                    {
                        UserId = userId,
                        NotificationId = notification.NotificationId,
                        ReadTime = DateTime.Now,
                    };
                    _tubumuContext.NotificationUser.Add(nu);
                }
                else if (!notificationUser.ReadTime.HasValue)
                {
                    notificationUser.ReadTime = DateTime.Now;
                }

            }
            await _tubumuContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int userId, int[] notificationIds, ModelStateDictionary modelState)
        {
            var notifications = await _tubumuContext.Notification.AsNoTracking().Where(m => notificationIds.Contains(m.NotificationId)).
                Select(m => new
                {
                    m.NotificationId,
                    m.ToUserId,
                }).
                ToArrayAsync();
            if (notifications.Any(m => m.ToUserId.HasValue && m.ToUserId != userId))
            {
                modelState.AddModelError("Error", "尝试读取不存在或非发给本人的通知");
                return false;
            }

            // TODO: 批量查询出 NotificationUsers，或以其他方式实现
            foreach (var notification in notifications)
            {
                var notificationUser = await _tubumuContext.NotificationUser.Where(m => m.NotificationId == notification.NotificationId && m.UserId == userId).FirstOrDefaultAsync();
                if (notificationUser == null)
                {
                    var nu = new NotificationUser
                    {
                        UserId = userId,
                        NotificationId = notification.NotificationId,
                        DeleteTime = DateTime.Now,
                    };
                    _tubumuContext.NotificationUser.Add(nu);
                }
                else if (!notificationUser.DeleteTime.HasValue)
                {
                    notificationUser.DeleteTime = DateTime.Now;
                }
            }

            await _tubumuContext.SaveChangesAsync();
            return true;
        }

        public async Task<XM.NotificationUser> GetNewestAsync(int userId, int? currentNotificationId = null)
        {
            var userCreationDate = await _tubumuContext.User.AsNoTracking().Where(m => m.UserId == userId).Select(m => m.CreationDate).FirstOrDefaultAsync();

            var query1 = from n in _tubumuContext.Notification.AsNoTracking()
                         where n.CreationDate > userCreationDate && (!n.ToUserId.HasValue || n.ToUserId == userId)
                         select n;

            if (currentNotificationId.HasValue)
            {
                query1 = query1.Where(n => n.NotificationId > currentNotificationId.Value);
            }

            IQueryable<XM.NotificationUser> query2;
            query2 = from m in query1
                     from pu in m.NotificationUser.Where(n => n.UserId == userId).DefaultIfEmpty()
                     where pu == null || (!pu.DeleteTime.HasValue && !pu.ReadTime.HasValue)
                     //join pu in DbContext.NotificationUsers.Where(n => n.UserId == userId) on m equals pu.Notification into purd
                     //from x in purd.DefaultIfEmpty()
                     //where x == null || (!x.DeleteTime.HasValue && !x.ReadTime.HasValue)
                     orderby m.NotificationId descending
                     select new XM.NotificationUser
                     {
                         NotificationId = m.NotificationId,
                         FromUser = new XM.UserInfoWarpper
                         {
                             UserId = m.FromUserId.HasValue ? m.FromUser.UserId : 0,
                             Username = m.FromUserId.HasValue ? m.FromUser.Username : "",
                             DisplayName = m.FromUserId.HasValue ? m.FromUser.DisplayName : "",
                             HeadUrl = m.FromUserId.HasValue ? m.FromUser.HeadUrl : "",
                             LogoUrl = m.FromUserId.HasValue ? m.FromUser.LogoUrl : "",
                         },
                         ToUser = new XM.UserInfoWarpper
                         {
                             UserId = m.ToUserId.HasValue ? m.ToUser.UserId : 0,
                             Username = m.ToUserId.HasValue ? m.ToUser.Username : "",
                             DisplayName = m.ToUserId.HasValue ? m.ToUser.DisplayName : "",
                             HeadUrl = m.ToUserId.HasValue ? m.ToUser.HeadUrl : "",
                             LogoUrl = m.ToUserId.HasValue ? m.ToUser.LogoUrl : "",
                         },
                         Title = m.Title,
                         Message = m.Message,
                         Url = m.Url,
                         CreationDate = m.CreationDate,

                         ReadTime = pu != null ? pu.ReadTime : null,
                         DeleteTime = pu != null ? pu.DeleteTime : null,
                     };

            return await query2.FirstOrDefaultAsync();
        }

    }
}
