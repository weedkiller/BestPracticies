using Hazel.Core;
using Hazel.Core.Caching;
using Hazel.Core.Domain.ApplicationUsers;
using Hazel.Core.Domain.Logging;
using Hazel.Data;
using Hazel.Data.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Hazel.Services.Logging
{
    /// <summary>
    /// ApplicationUser activity service.
    /// </summary>
    public class ApplicationUserActivityService : IApplicationUserActivityService
    {
        /// <summary>
        /// Defines the _dbContext.
        /// </summary>
        private readonly IDbContext _dbContext;

        /// <summary>
        /// Defines the _activityLogRepository.
        /// </summary>
        private readonly IRepository<ActivityLog> _activityLogRepository;

        /// <summary>
        /// Defines the _activityLogTypeRepository.
        /// </summary>
        private readonly IRepository<ActivityLogType> _activityLogTypeRepository;

        /// <summary>
        /// Defines the _cacheManager.
        /// </summary>
        private readonly IStaticCacheManager _cacheManager;

        /// <summary>
        /// Defines the _webHelper.
        /// </summary>
        private readonly IWebHelper _webHelper;

        /// <summary>
        /// Defines the _workContext.
        /// </summary>
        private readonly IWorkContext _workContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApplicationUserActivityService"/> class.
        /// </summary>
        /// <param name="dbContext">The dbContext<see cref="IDbContext"/>.</param>
        /// <param name="activityLogRepository">The activityLogRepository<see cref="IRepository{ActivityLog}"/>.</param>
        /// <param name="activityLogTypeRepository">The activityLogTypeRepository<see cref="IRepository{ActivityLogType}"/>.</param>
        /// <param name="cacheManager">The cacheManager<see cref="IStaticCacheManager"/>.</param>
        /// <param name="webHelper">The webHelper<see cref="IWebHelper"/>.</param>
        /// <param name="workContext">The workContext<see cref="IWorkContext"/>.</param>
        public ApplicationUserActivityService(IDbContext dbContext,
            IRepository<ActivityLog> activityLogRepository,
            IRepository<ActivityLogType> activityLogTypeRepository,
            IStaticCacheManager cacheManager,
            IWebHelper webHelper,
            IWorkContext workContext)
        {
            _dbContext = dbContext;
            _activityLogRepository = activityLogRepository;
            _activityLogTypeRepository = activityLogTypeRepository;
            _cacheManager = cacheManager;
            _webHelper = webHelper;
            _workContext = workContext;
        }

        /// <summary>
        /// Activity log type for caching.
        /// </summary>
        [Serializable]
        public class ActivityLogTypeForCaching
        {
            /// <summary>
            /// Gets or sets the Id
            /// Identifier.
            /// </summary>
            public int Id { get; set; }

            /// <summary>
            /// Gets or sets the SystemKeyword
            /// System keyword.
            /// </summary>
            public string SystemKeyword { get; set; }

            /// <summary>
            /// Gets or sets the Name
            /// Name.
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether Enabled
            /// Enabled.
            /// </summary>
            public bool Enabled { get; set; }
        }

        /// <summary>
        /// Gets all activity log types (class for caching).
        /// </summary>
        /// <returns>Activity log types.</returns>
        protected virtual IList<ActivityLogTypeForCaching> GetAllActivityTypesCached()
        {
            //cache
            return _cacheManager.Get(HazelLoggingDefaults.ActivityTypeAllCacheKey, () =>
            {
                var result = new List<ActivityLogTypeForCaching>();
                var activityLogTypes = GetAllActivityTypes();
                foreach (var alt in activityLogTypes)
                {
                    var altForCaching = new ActivityLogTypeForCaching
                    {
                        Id = alt.Id,
                        SystemKeyword = alt.SystemKeyword,
                        Name = alt.Name,
                        Enabled = alt.Enabled
                    };
                    result.Add(altForCaching);
                }

                return result;
            });
        }

        /// <summary>
        /// Inserts an activity log type item.
        /// </summary>
        /// <param name="activityLogType">Activity log type item.</param>
        public virtual void InsertActivityType(ActivityLogType activityLogType)
        {
            if (activityLogType == null)
                throw new ArgumentNullException(nameof(activityLogType));

            _activityLogTypeRepository.Insert(activityLogType);
            _cacheManager.RemoveByPrefix(HazelLoggingDefaults.ActivityTypePrefixCacheKey);
        }

        /// <summary>
        /// Updates an activity log type item.
        /// </summary>
        /// <param name="activityLogType">Activity log type item.</param>
        public virtual void UpdateActivityType(ActivityLogType activityLogType)
        {
            if (activityLogType == null)
                throw new ArgumentNullException(nameof(activityLogType));

            _activityLogTypeRepository.Update(activityLogType);
            _cacheManager.RemoveByPrefix(HazelLoggingDefaults.ActivityTypePrefixCacheKey);
        }

        /// <summary>
        /// Deletes an activity log type item.
        /// </summary>
        /// <param name="activityLogType">Activity log type.</param>
        public virtual void DeleteActivityType(ActivityLogType activityLogType)
        {
            if (activityLogType == null)
                throw new ArgumentNullException(nameof(activityLogType));

            _activityLogTypeRepository.Delete(activityLogType);
            _cacheManager.RemoveByPrefix(HazelLoggingDefaults.ActivityTypePrefixCacheKey);
        }

        /// <summary>
        /// Gets all activity log type items.
        /// </summary>
        /// <returns>Activity log type items.</returns>
        public virtual IList<ActivityLogType> GetAllActivityTypes()
        {
            var query = from alt in _activityLogTypeRepository.Table
                        orderby alt.Name
                        select alt;
            var activityLogTypes = query.ToList();
            return activityLogTypes;
        }

        /// <summary>
        /// Gets an activity log type item.
        /// </summary>
        /// <param name="activityLogTypeId">Activity log type identifier.</param>
        /// <returns>Activity log type item.</returns>
        public virtual ActivityLogType GetActivityTypeById(int activityLogTypeId)
        {
            if (activityLogTypeId == 0)
                return null;

            return _activityLogTypeRepository.GetById(activityLogTypeId);
        }

        /// <summary>
        /// Inserts an activity log item.
        /// </summary>
        /// <param name="systemKeyword">System keyword.</param>
        /// <param name="comment">Comment.</param>
        /// <param name="entity">Entity.</param>
        /// <returns>Activity log item.</returns>
        public virtual ActivityLog InsertActivity(string systemKeyword, string comment, BaseEntity entity = null)
        {
            return InsertActivity(_workContext.CurrentApplicationUser, systemKeyword, comment, entity);
        }

        /// <summary>
        /// Inserts an activity log item.
        /// </summary>
        /// <param name="ApplicationUser">ApplicationUser.</param>
        /// <param name="systemKeyword">System keyword.</param>
        /// <param name="comment">Comment.</param>
        /// <param name="entity">Entity.</param>
        /// <returns>Activity log item.</returns>
        public virtual ActivityLog InsertActivity(ApplicationUser ApplicationUser, string systemKeyword, string comment, BaseEntity entity = null)
        {
            if (ApplicationUser == null)
                return null;

            //try to get activity log type by passed system keyword
            var activityLogType = GetAllActivityTypesCached().FirstOrDefault(type => type.SystemKeyword.Equals(systemKeyword));
            if (!activityLogType?.Enabled ?? true)
                return null;

            //insert log item
            var logItem = new ActivityLog
            {
                ActivityLogTypeId = activityLogType.Id,
                EntityId = entity?.Id,
                EntityName = entity?.GetUnproxiedEntityType().Name,
                ApplicationUserId = ApplicationUser.Id,
                Comment = CommonHelper.EnsureMaximumLength(comment ?? string.Empty, 4000),
                CreatedOnUtc = DateTime.UtcNow,
                IpAddress = _webHelper.GetCurrentIpAddress()
            };
            _activityLogRepository.Insert(logItem);

            return logItem;
        }

        /// <summary>
        /// Deletes an activity log item.
        /// </summary>
        /// <param name="activityLog">Activity log type.</param>
        public virtual void DeleteActivity(ActivityLog activityLog)
        {
            if (activityLog == null)
                throw new ArgumentNullException(nameof(activityLog));

            _activityLogRepository.Delete(activityLog);
        }

        /// <summary>
        /// Gets all activity log items.
        /// </summary>
        /// <param name="createdOnFrom">Log item creation from; pass null to load all records.</param>
        /// <param name="createdOnTo">Log item creation to; pass null to load all records.</param>
        /// <param name="ApplicationUserId">ApplicationUser identifier; pass null to load all records.</param>
        /// <param name="activityLogTypeId">Activity log type identifier; pass null to load all records.</param>
        /// <param name="ipAddress">IP address; pass null or empty to load all records.</param>
        /// <param name="entityName">Entity name; pass null to load all records.</param>
        /// <param name="entityId">Entity identifier; pass null to load all records.</param>
        /// <param name="pageIndex">Page index.</param>
        /// <param name="pageSize">Page size.</param>
        /// <returns>Activity log items.</returns>
        public virtual IPagedList<ActivityLog> GetAllActivities(DateTime? createdOnFrom = null, DateTime? createdOnTo = null,
            int? ApplicationUserId = null, int? activityLogTypeId = null, string ipAddress = null, string entityName = null, int? entityId = null,
            int pageIndex = 0, int pageSize = int.MaxValue)
        {
            var query = _activityLogRepository.Table;

            //filter by IP
            if (!string.IsNullOrEmpty(ipAddress))
                query = query.Where(logItem => logItem.IpAddress.Contains(ipAddress));

            //filter by creation date
            if (createdOnFrom.HasValue)
                query = query.Where(logItem => createdOnFrom.Value <= logItem.CreatedOnUtc);
            if (createdOnTo.HasValue)
                query = query.Where(logItem => createdOnTo.Value >= logItem.CreatedOnUtc);

            //filter by log type
            if (activityLogTypeId.HasValue && activityLogTypeId.Value > 0)
                query = query.Where(logItem => activityLogTypeId == logItem.ActivityLogTypeId);

            //filter by ApplicationUser
            if (ApplicationUserId.HasValue && ApplicationUserId.Value > 0)
                query = query.Where(logItem => ApplicationUserId.Value == logItem.ApplicationUserId);

            //filter by entity
            if (!string.IsNullOrEmpty(entityName))
                query = query.Where(logItem => logItem.EntityName.Equals(entityName));
            if (entityId.HasValue && entityId.Value > 0)
                query = query.Where(logItem => entityId.Value == logItem.EntityId);

            query = query.OrderByDescending(logItem => logItem.CreatedOnUtc).ThenBy(logItem => logItem.Id);

            return new PagedList<ActivityLog>(query, pageIndex, pageSize);
        }

        /// <summary>
        /// Gets an activity log item.
        /// </summary>
        /// <param name="activityLogId">Activity log identifier.</param>
        /// <returns>Activity log item.</returns>
        public virtual ActivityLog GetActivityById(int activityLogId)
        {
            if (activityLogId == 0)
                return null;

            return _activityLogRepository.GetById(activityLogId);
        }

        /// <summary>
        /// Clears activity log.
        /// </summary>
        public virtual void ClearAllActivities()
        {
            //do all databases support "Truncate command"?
            var activityLogTableName = _dbContext.GetTableName<ActivityLog>();
            _dbContext.ExecuteSqlCommand($"TRUNCATE TABLE [{activityLogTableName}]");
        }
    }
}
