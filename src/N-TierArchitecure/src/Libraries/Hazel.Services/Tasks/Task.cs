using Hazel.Core.Caching;
using Hazel.Core.Domain.Tasks;
using Hazel.Core.Infrastructure;
using Hazel.Services.Localization;
using Hazel.Services.Logging;
using System;
using System.Linq;

namespace Hazel.Services.Tasks
{
    /// <summary>
    /// Task.
    /// </summary>
    public partial class Task
    {
        /// <summary>
        /// Defines the _enabled.
        /// </summary>
        private bool? _enabled;

        /// <summary>
        /// Initializes a new instance of the <see cref="Task"/> class.
        /// </summary>
        /// <param name="task">Task .</param>
        public Task(ScheduleTask task)
        {
            ScheduleTask = task;
        }

        /// <summary>
        /// Initialize and execute task.
        /// </summary>
        private void ExecuteTask()
        {
            var scheduleTaskService = EngineContext.Current.Resolve<IScheduleTaskService>();

            if (!Enabled)
                return;

            var type = Type.GetType(ScheduleTask.Type) ??
                //ensure that it works fine when only the type name is specified (do not require fully qualified names)
                AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType(ScheduleTask.Type))
                .FirstOrDefault(t => t != null);
            if (type == null)
                throw new Exception($"Schedule task ({ScheduleTask.Type}) cannot by instantiated");

            object instance = null;
            try
            {
                instance = EngineContext.Current.Resolve(type);
            }
            catch
            {
                //try resolve
            }

            if (instance == null)
            {
                //not resolved
                instance = EngineContext.Current.ResolveUnregistered(type);
            }

            var task = instance as IScheduleTask;
            if (task == null)
                return;

            ScheduleTask.LastStartUtc = DateTime.UtcNow;
            //update appropriate datetime properties
            scheduleTaskService.UpdateTask(ScheduleTask);
            task.Execute();
            ScheduleTask.LastEndUtc = ScheduleTask.LastSuccessUtc = DateTime.UtcNow;
            //update appropriate datetime properties
            scheduleTaskService.UpdateTask(ScheduleTask);
        }

        /// <summary>
        /// Is task already running?.
        /// </summary>
        /// <param name="scheduleTask">Schedule task.</param>
        /// <returns>Result.</returns>
        protected virtual bool IsTaskAlreadyRunning(ScheduleTask scheduleTask)
        {
            //task run for the first time
            if (!scheduleTask.LastStartUtc.HasValue && !scheduleTask.LastEndUtc.HasValue)
                return false;

            var lastStartUtc = scheduleTask.LastStartUtc ?? DateTime.UtcNow;

            //task already finished
            if (scheduleTask.LastEndUtc.HasValue && lastStartUtc < scheduleTask.LastEndUtc)
                return false;

            //task wasn't finished last time
            if (lastStartUtc.AddSeconds(scheduleTask.Seconds) <= DateTime.UtcNow)
                return false;

            return true;
        }

        /// <summary>
        /// Executes the task.
        /// </summary>
        /// <param name="throwException">A value indicating whether exception should be thrown if some error happens.</param>
        /// <param name="ensureRunOncePerPeriod">A value indicating whether we should ensure this task is run once per run period.</param>
        public void Execute(bool throwException = false, bool ensureRunOncePerPeriod = true)
        {
            if (ScheduleTask == null || !Enabled)
                return;

            if (ensureRunOncePerPeriod)
            {
                //task already running
                if (IsTaskAlreadyRunning(ScheduleTask))
                    return;

                //validation (so nobody else can invoke this method when he wants)
                if (ScheduleTask.LastStartUtc.HasValue && (DateTime.UtcNow - ScheduleTask.LastStartUtc).Value.TotalSeconds < ScheduleTask.Seconds)
                    //too early
                    return;
            }

            try
            {
                //get expiration time
                var expirationInSeconds = Math.Min(ScheduleTask.Seconds, 300) - 1;
                var expiration = TimeSpan.FromSeconds(expirationInSeconds);

                //execute task with lock
                var locker = EngineContext.Current.Resolve<ILocker>();
                locker.PerformActionWithLock(ScheduleTask.Type, expiration, ExecuteTask);
            }
            catch (Exception exc)
            {
                var scheduleTaskService = EngineContext.Current.Resolve<IScheduleTaskService>();
                var localizationService = EngineContext.Current.Resolve<ILocalizationService>();

                ScheduleTask.Enabled = !ScheduleTask.StopOnError;
                ScheduleTask.LastEndUtc = DateTime.UtcNow;
                scheduleTaskService.UpdateTask(ScheduleTask);

                var message = string.Format(localizationService.GetResource("ScheduleTasks.Error"), ScheduleTask.Name,
                    exc.Message, ScheduleTask.Type);

                //log error
                var logger = EngineContext.Current.Resolve<ILogger>();
                logger.Error(message, exc);
                if (throwException)
                    throw;
            }
        }

        /// <summary>
        /// Gets the ScheduleTask
        /// Schedule task.
        /// </summary>
        public ScheduleTask ScheduleTask { get; }

        /// <summary>
        /// Gets or sets a value indicating whether Enabled
        /// A value indicating whether the task is enabled.
        /// </summary>
        public bool Enabled
        {
            get
            {
                if (!_enabled.HasValue)
                    _enabled = ScheduleTask?.Enabled;

                return _enabled.HasValue && _enabled.Value;
            }

            set => _enabled = value;
        }
    }
}
