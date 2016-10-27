using System;
using System.Collections.Generic;
using System.Linq;

namespace MSharp.Framework.Services
{
    public class Profiler : IDisposable
    {
        public static Dictionary<string, TimeSpan> Jobs = new Dictionary<string, TimeSpan>();

        static List<string> RunningJobs = new List<string>();

        DateTime Start;
        string Job;
        bool Disabled;

        /// <summary>
        /// Creates a new Profiler instance.
        /// </summary>
        public Profiler(string job)
        {
            Disabled = RunningJobs.Contains(job);

            if (!Disabled)
            {
                RunningJobs.Add(job);
                Job = job;
                Start = LocalTime.Now;
            }
        }

        public void Dispose()
        {
            if (!Disabled)
            {
                var time = LocalTime.Now.Subtract(Start);

                if (Jobs.ContainsKey(Job))
                    Jobs[Job] = Jobs[Job].Add(time);
                else Jobs[Job] = time;

                RunningJobs.Remove(Job);
            }
        }
        
        public static string GetAllTimes()
        {
            return Jobs.OrderByDescending(j => j.Value.Ticks)
                .Select(j => j.Key + ": " + j.Value.TotalMilliseconds + " msec")
                .ToLinesString();
        }

        public static void Reset()
        {
            Jobs.Clear();
            RunningJobs.Clear();
        }
    }
}