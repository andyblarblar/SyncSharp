﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtoBuf;
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
                logger.LogDebug($"created directory {config.SavePath}");
            }

            //This prevents saving of the file if application has never been used.
            if (config?.Paths is null)
            {
                return;
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
                        //Place file under its directory in the backup folder
                        var saveDir = Path.Combine(config.SavePath, new string(Path.GetDirectoryName(path.Path).Reverse().TakeWhile(c => c != Path.DirectorySeparatorChar).Reverse().ToArray()));
                        if (!Directory.Exists(saveDir)) Directory.CreateDirectory(saveDir);

                        await SyncFile(config, token, path, logger, saveDir);
                        path.LastSynced = DateTime.Now;
                    }
                }
                catch(OperationCanceledException)//File is already deleted by here, so just return.
                {
                    SaveConfig(config, $".{Path.DirectorySeparatorChar}conf.bin");
                    return;
                }
            }

            SaveConfig(config, $".{Path.DirectorySeparatorChar}conf.bin");
        }

        private static async Task SyncFile(Config config, CancellationToken token, FileProfile path, ILogger logger, string configSavePath)
        {
            var fileInfo = new FileInfo(path.Path);

            //needs to be synced
            if (fileInfo.LastWriteTime - path.LastSynced > config.CheckInterval)//TODO this timing is off
            {
                logger.LogDebug($"Syncing {path.Path}");
                await using var sourceStream = File.OpenRead(path.Path);
                await using var destinationStream = File.Create(Path.Combine(configSavePath, fileInfo.Name));

                try
                {
                    await sourceStream.CopyToAsync(destinationStream, 81920, token);
                    token.ThrowIfCancellationRequested();
                }
                catch (OperationCanceledException)//Delete file if cancelled
                {
                    logger.LogDebug($"deleteing file {Path.Combine(configSavePath, fileInfo.Name)}");
                    await destinationStream.DisposeAsync();//free up file handles
                    await sourceStream.DisposeAsync();
                    File.Delete(Path.Combine(configSavePath, fileInfo.Name));
                    throw;
                }

            }
            else
            {
                logger.LogDebug($"Skipping {path.Path}");
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
                    //Preserve path for root dir
                    if (dir == initialPath)
                    {
                        acc.Add(new string(dir.Reverse().TakeWhile(c => c != Path.DirectorySeparatorChar).Reverse().ToArray()), Directory.GetFiles(dir));
                    }
                    else
                    {
                         acc.Add(Path.GetRelativePath(initialPath,dir),Directory.GetFiles(dir));
                    }
                    
                    var newSubDir = Directory.GetDirectories(dir);
                    if(newSubDir.Length > 0) Traverse(newSubDir, acc, initialPath);
                }
            }

            Traverse(new List<string>{path.Path}, resultDict, path.Path);

            return resultDict;
        }

        /// <summary>
        /// Saves the passed config in proto-buf form.
        /// </summary>
        public static void SaveConfig(Config conf,string savePath)
        {
            using var stream = File.Create(savePath);

            Serializer.Serialize(stream, conf);
        }

        /// <summary>
        /// Loads config.
        /// </summary>
        public static Config LoadConfig(string path)
        {
            try
            {
                using var stream = File.OpenRead(path);
                return Serializer.Deserialize<Config>(stream);
            }
            catch (Exception)//File doesn't exist
            {
                //Default TODO change before release
                return new Config{
                    CheckInterval = TimeSpan.FromMinutes(.5),
                    Paths = new List<FileProfile> { new FileProfile { LastSynced = DateTime.MinValue, Path = "Y:\\Documents\\School" } },
                    SavePath = "C:\\Users\\Andyblarblar\\Downloads\\Backu"
                };
            }
        }


    }
}
