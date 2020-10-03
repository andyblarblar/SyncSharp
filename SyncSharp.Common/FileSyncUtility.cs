using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SyncSharp.Common.model;

namespace SyncSharp.Common
{
    public static class FileSyncUtility
    {

        /// <summary>
        /// Syncs files according to the passed config. If token is cancelled, then partially
        /// synced files are deleted.
        /// </summary>
        public static async Task Sync(Config config, CancellationToken token, ILogger logger)
        {
            if (!Directory.Exists(config.SavePath))
            {
                Directory.CreateDirectory(config.SavePath);
                logger.LogInformation($"created directory {config.SavePath}");
            }

            foreach (var path in config.Paths)
            {
                var pathIsDirAndExists = Directory.Exists(path.Path);
                var pathIsFileAndExists = File.Exists(path.Path);

                try
                {
                    if (pathIsDirAndExists)
                    {
                        var dict = CrawlDirectory(path);

                        foreach (var dir in dict.Keys)
                        {
                            if (!Directory.Exists(Path.Combine(config.SavePath, dir)))
                            {
                                Directory.CreateDirectory(Path.Combine(config.SavePath, dir));
                            }

                            foreach (var file in dict[dir])
                            {
                                await SyncFile(config, token,
                                new FileProfile {Path = file, LastSynced = path.LastSynced},logger, Path.Combine(config.SavePath, dir));
                            }
                        }

                        path.LastSynced = DateTime.Now;
                    }
                    else if (pathIsFileAndExists)
                    {
                        await SyncFile(config, token, path, logger, config.SavePath);
                        path.LastSynced = DateTime.Now;
                    }
                }
                catch(OperationCanceledException)//File is already deleted by here, so just return.
                {
                    return;
                }
            }

            //TODO save config in file 
        }

        private static async Task SyncFile(Config config, CancellationToken token, FileProfile path, ILogger logger,
            string configSavePath)
        {
            var fileInfo = new FileInfo(path.Path);

            //needs to be synced
            if (fileInfo.LastWriteTime - path.LastSynced > config.CheckInterval)//TODO this timing is off
            {
                logger.LogInformation($"Syncing {path.Path}");
                await using var sourceStream = File.OpenRead(path.Path);
                await using var destinationStream = File.Create(Path.Combine(configSavePath, fileInfo.Name));

                var res = sourceStream.CopyToAsync(destinationStream, 81920, token);
                await res;

                //Delete the bad file if copy is cancelled
                if (res.Status == TaskStatus.Canceled)
                {
                    await destinationStream.DisposeAsync();//free up file handle
                    await sourceStream.DisposeAsync();
                    File.Delete(Path.Combine(configSavePath, fileInfo.Name));
                    token.ThrowIfCancellationRequested();
                }
            }
            else
            {
                logger.LogInformation($"Skipping {path.Path}");
            }
        }

        /// <summary>
        /// Crawls the passed directory, accumulating the paths of all files in subdirectories.
        /// and the initial directory.
        /// </summary>
        /// <returns>A dictionary of directory:files[] where files are the files in that directory</returns>
        public static Dictionary<string,IEnumerable<string>> CrawlDirectory(FileProfile path)
        {
            var resultDict = new Dictionary<string,IEnumerable<string>>();

            static void Traverse(IEnumerable<string> subDirs, IDictionary<string, IEnumerable<string>> acc, string initialPath)
            {
                foreach (var dir in subDirs)
                {
                    acc.Add(Path.GetRelativePath(initialPath,dir),Directory.GetFiles(dir));

                    var newSubDir = Directory.GetDirectories(dir);
                    if(newSubDir.Length > 0) Traverse(newSubDir, acc, initialPath);
                }
            }

            Traverse(new List<string>{path.Path}, resultDict, path.Path);

            return resultDict;
        }

    }
}
