using log4net;
using log4net.Repository.Hierarchy;
using System.Diagnostics;

namespace Linienrechner.Klassen.Utils
{
    /// <summary>
    /// LogFileCleanupTask Klasse, räumt alte Logfiles auf
    /// </summary>
    internal class LogFileCleanupTask
    {

        /// <summary>Räumt Logfiles auf, die älter als die angegebene Anzahl an Tagen sind</summary>
        /// <param name="beforeDays"></param>
        public void CleanUp(int beforeDays)
        {
            string directory = string.Empty;

            var repo = LogManager.GetAllRepositories().FirstOrDefault();
            if (repo == null)
            {
                return;
            }

            var app = repo.GetAppenders().OfType<log4net.Appender.RollingFileAppender>().FirstOrDefault();
            if (app != null)
            {
                var appender = app as log4net.Appender.RollingFileAppender;

                var basePath = AppDomain.CurrentDomain.BaseDirectory;

                directory = Path.Combine(basePath, "logs");

                if (!Directory.Exists(directory))
                {
                    return;
                }

                var date = DateTime.Now.AddDays(-beforeDays);
                CleanUp(directory, date);
            }
        }

        /// <summary>Prüft ob das Logverzeichnis existiert und löscht alle Logfiles die älter als das angegebene Datum sind</summary>
        /// <param name="logDirectory"></param>
        /// <param name="date"></param>
        /// <exception cref="ArgumentException"></exception>
        public void CleanUp(string logDirectory, DateTime date)
        {
            if (string.IsNullOrEmpty(logDirectory))
            {
                throw new ArgumentException("logDirectory is missing");
            }

            var dirInfo = new DirectoryInfo(logDirectory);
            if (!dirInfo.Exists)
            {
                return;
            }

            var yearDirectories = dirInfo.GetDirectories();
            foreach (var yearDir in yearDirectories)
            {
                var monthDirectories = yearDir.GetDirectories();
                foreach (var monthDir in monthDirectories)
                {
                    var fileInfos = monthDir.GetFiles();
                    if (fileInfos.Length == 0)
                    {
                        return;
                    }

                    foreach (var info in fileInfos)
                    {
                        if (info.CreationTime < date)
                        {
                            info.Delete();
                        }
                    }

                }
            }
        }


    }
}
