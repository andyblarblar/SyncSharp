using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SyncSharp.Common.model;

namespace SyncSharp.Common
{
    public static class FileSyncUtility
    {

        /// <summary>
        /// Syncs files according to the passed config. If token is cancelled, then partially
        /// synced files are deleted.
        /// </summary>
        public static async Task Sync(Config config, CancellationToken token)
        {
            foreach (var path in config.Paths)
            {
                var pathIsDirAndExists = Directory.Exists(path.Path);
                var pathIsFileAndExists = File.Exists(path.Path);

                try
                {
                    if (pathIsDirAndExists)
                    {
                        foreach (var filePath in Directory.GetFiles(path.Path))
                        {
                            await SyncFile(config, token,
                                new FileProfile {Path = filePath, LastSynced = path.LastSynced});
                        }

                        path.LastSynced = DateTime.Now;
                    }
                    else if (pathIsFileAndExists)
                    {
                        await SyncFile(config, token, path);
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

        private static async Task SyncFile(Config config, CancellationToken token, FileProfile path)
        {
            var fileInfo = new FileInfo(path.Path);

            //needs to be synced
            if (fileInfo.LastWriteTime - path.LastSynced > config.CheckInterval)//TODO this timing is off
            {
                using var sourceStream = File.OpenRead(path.Path);
                using var destinationStream = File.Create(Path.Combine(config.SavePath, fileInfo.Name));

                var res = sourceStream.CopyToAsync(destinationStream, 81920, token);
                await res;

                //Delete the bad file if copy is cancelled
                if (res.Status == TaskStatus.Canceled)
                {
                    destinationStream.Dispose();//free up file handle
                    sourceStream.Dispose();
                    File.Delete(Path.Combine(config.SavePath, fileInfo.Name));
                    token.ThrowIfCancellationRequested();
                }
            }
        }
    }
}
