namespace MSharp.Framework.Data
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Linq;
    using MSharp.Framework.Services;

    /// <summary>
    /// Provides SQL profiling services.
    /// </summary>
    public class DataAccessProfiler
    {
        internal static bool IsEnabled = Config.Get<bool>("Database.Profile.Enabled", defaultValue: false);

        static ConcurrentBag<Watch> Watches = new ConcurrentBag<Watch>();

        static object SyncLock = new object();

        public static void Reset()
        {
            Watches = new ConcurrentBag<Watch>();
        }

        internal static Watch Start(string command)
        {
            var result = new Watch(command);

            return result;
        }

        internal static void Complete(Watch watch)
        {
            watch.Duration = DateTime.Now.Subtract(watch.Start);

            Watches.Add(watch);
        }

        internal class Watch
        {
            internal string Command;
            internal DateTime Start;
            internal TimeSpan Duration;

            public Watch(string command)
            {
                Command = command.ToLines().ToString(" ");
                Start = DateTime.Now;
            }
        }

        /// <summary>
        /// To invoice this you can send a request to the application using http://...?Web.Test.Command=Sql.Profile&amp;Mode=Snapshot
        /// </summary>
        /// <param name="snapshot">Determines whether the current log data should be removed (false) or kept for future combined future generated (true).</param>
        public static FileInfo GenerateReport(bool snapshot = false)
        {
            var items = Watches.ToArray().GroupBy(x => x.Command);

            if (!snapshot) Reset();

            var lines = items.Select(item => new
            {
                Command = item.Key,
                Calls = item.Count(),
                Total = item.Sum(x => x.Duration).TotalMilliseconds.Round(1),
                Average = item.Select(x => (x.Duration.TotalMilliseconds)).Average().Round(1),
                Median = item.Select(x => (int)(x.Duration.TotalMilliseconds * 100)).Median() * 0.01,
                Longest = item.Max(x => x.Duration).TotalMilliseconds.Round(1)
            }).ToList();

            var exporter = new ExcelExporter("Sql.Profile.Report");

            exporter.AddColumn("Command");
            exporter.AddColumn("Calls");
            exporter.AddColumn("Total ms");
            exporter.AddColumn("Longest ms");
            exporter.AddColumn("Average ms");
            exporter.AddColumn("Median ms");

            foreach (var line in lines.OrderByDescending(x => x.Total))
            {
                exporter.AddRow(line.Command, line.Calls, line.Total, line.Longest, line.Average, line.Median);
            }

            var result = exporter.Generate(ExcelExporter.Output.Csv);

            var file = AppDomain.CurrentDomain.GetPath("App_Data").AsDirectory().EnsureExists().GetFile("Sql.Profile." + DateTime.Now.ToOADate() + ".csv");

            file.WriteAllText(result);

            return file;
        }
    }
}