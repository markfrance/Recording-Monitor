using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;

namespace MSharp.Framework.Services
{
    partial class AutomatedTask
    {
        Action<AutomatedTask> Action;

        public TimeSpan Intervals { get; set; }
        Thread ExecutionThread;

        /// <summary>
        /// Creates a new AutomatedTask instance.
        /// </summary>
        public AutomatedTask(Action<AutomatedTask> action)
            : this()
        {
            if (action == null)
                throw new ArgumentNullException("action");

            Action = action;
            ExecutionThread = Thread.CurrentThread.CreateNew(Process, t => t.IsBackground = true);

            Priority = ThreadPriority.Lowest;
            Status = AutomatedTaskStatus.AwaitingFirstRun;
        }

        public AutomatedTaskStatus Status { get; private set; }

        /// <summary>
        /// Starts this automated task.
        /// </summary>
        public void Start()
        {
            ExecutionThread.Start();
        }

        /// <summary>
        /// Restarts this task.
        /// </summary>
        public void Restart()
        {
            try
            {
                ExecutionThread.Abort();
            }
            catch
            {
                // silence
            }

            ExecutionThread = Thread.CurrentThread.CreateNew(Process, t => { t.IsBackground = true; t.Priority = ThreadPriority.Lowest; });
            ExecutionThread.Start();
        }

        #region Persistent Execution Log
        static object ExecutionPersistenceSyncLock = new object();
        DateTime GetInitialNextTry()
        {
            var result = LocalTime.Now;

            lock (ExecutionPersistenceSyncLock)
            {
                if (ShouldPersistExecution())
                {
                    var file = GetExecutionStatusPath();

                    if (File.Exists(file))
                    {
                        try
                        {
                            var content = File.ReadAllText(file);
                            if (content.HasValue())
                            {
                                var taskData = XElement.Parse(content).Elements().FirstOrDefault(e => e.GetValue<string>("@Name") == Name);

                                if (taskData != null)
                                {
                                    result = DateTime.FromOADate(taskData.GetValue<string>("@LastRun").To<double>()).ToLocalTime();
                                    result = result.Add(Intervals);
                                }
                            }
                        }
                        catch
                        {
                            // The file is perhaps corrupted.
                        }
                    }
                }
            }

            return result;
        }

        static string GetExecutionStatusPath()
        {
            var result = Config.Get("Automated.Tasks.Status.Path", "App_Data\\AutomatedTasks.Status.xml");

            if (result.Length < 3)
                throw new Exception("Invalid settings for 'Automated.Task.Execution.Log' appSetting. The provided path is too short.");

            if (!result.StartsWith("\\") && result[1] != ':')
            {
                // Relative pth:            
                result = AppDomain.CurrentDomain.GetPath(result);
            }

            result.AsFile().Directory.EnsureExists();

            return result;
        }

        static bool ShouldPersistExecution()
        {
            return Config.Get<bool>("Automated.Tasks.Persist.Execution", defaultValue: false);
        }

        public static void DeleteExecutionStatusHistory()
        {
            lock (ExecutionPersistenceSyncLock)
            {
                GetExecutionStatusPath().AsFile().Delete(harshly: true);
            }
        }

        void PersistExecution()
        {
            if (!ShouldPersistExecution()) return;

            var path = GetExecutionStatusPath();

            lock (ExecutionPersistenceSyncLock)
            {
                var data = new XElement("Tasks");

                if (File.Exists(path))
                {
                    new Action(() =>
                    {
                        try
                        {

                            var content = File.ReadAllText(path);
                            if (content.HasValue()) data = XElement.Parse(content);
                        }
                        catch (FileNotFoundException)
                        {
                            // Somehow another thread has deleted it.
                        }
                    }).Invoke(10, TimeSpan.FromMilliseconds(300));
                }

                var element = data.Elements().FirstOrDefault(e => e.GetValue<string>("@Name") == Name);

                if (element == null)
                    data.Add(new XElement("Task", new XAttribute("Name", Name), new XAttribute("LastRun", LocalTime.Now.ToUniversalTime().ToOADate().ToString())));
                else
                    element.Attribute("LastRun").Value = LocalTime.Now.ToUniversalTime().ToOADate().ToString();

                try
                {
                    path.AsFile().WriteAllText(data.ToString());
                }
                catch
                {
                    // Error?
                }
            }
        }

        #endregion

        [System.Diagnostics.DebuggerStepThrough]
        void Process()
        {
            NextTry = GetInitialNextTry();

            // Startup delay:
            if (Delay > TimeSpan.Zero)
            {
                NextTry = NextTry.Value.Add(Delay);
                Thread.Sleep(Delay);
            }

            // Should we still wait?
            var stillWait = NextTry.Value - LocalTime.Now;
            if (stillWait.TotalMilliseconds > int.MaxValue) Thread.Sleep(int.MaxValue);
            else if (stillWait > TimeSpan.Zero) Thread.Sleep(stillWait);

            for (; /* ever */ ;)
            {
                Execute();

                // Now wait for the next itnerval:
                WaitEnough();
            }
        }

        [System.Diagnostics.DebuggerStepThrough]
        void WaitEnough()
        {
            Thread.Sleep(Intervals);
        }

        internal void Execute()
        {
            NextTry = null;

            if (SyncGroup != null)
            {
                Status = AutomatedTaskStatus.WaitingForLock;
                lock (SyncGroup) { DoExecute(); }
            }
            else
            {
                DoExecute();
            }

            NextTry = LocalTime.Now.Add(Intervals);
        }

        void DoExecute()
        {
            CurrentStartTime = LastRunStart = LocalTime.Now;

            try
            {
                Status = AutomatedTaskStatus.Running;
                Action?.Invoke(this);

                if (RecordSuccess)
                {
                    try { ApplicationEventManager.RecordScheduledTask(Name, CurrentStartTime.Value); }
                    catch { /*Problem in logging*/ }
                }

                Status = AutomatedTaskStatus.CompletedAwaitingNextRun;
            }
            catch (Exception ex)
            {
                //if (!WebTestManager.IsTddExecutionMode())
                {
                    if (RecordFailure)
                    {
                        try { ApplicationEventManager.RecordScheduledTask(Name, CurrentStartTime.Value, ex); }
                        catch { /*Problem in logging*/ }
                    }
                }

                Status = AutomatedTaskStatus.FailedAwaitingNextRun;
            }
            finally
            {
                CurrentStartTime = null;
                LastRunEnd = LocalTime.Now;
                PersistExecution();
            }
        }

        internal static IEnumerable<AutomatedTask> GetAllTasks()
        {
            var classes = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("TaskManager")).ExceptNull().Distinct().ToList();

            if (classes.None())
                throw new Exception("There is no class named TaskManager in the current application domain.");

            if (classes.HasMany())
                throw new Exception("There are multiple classes named TaskManager in the current application domain.");

            var tasks = classes.First().GetProperty("Tasks", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public).GetValue(null)
                 as IEnumerable<AutomatedTask>;

            if (tasks == null)
                throw new Exception("Class TaskManager doesn't have a property named Tasks of type IEnumerable<AutomatedTask>.");

            return tasks;
        }
    }
}