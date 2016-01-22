﻿using System.IO;
using System.Threading.Tasks;
using Etg.Yams.Application;
using Etg.Yams.Storage;
using Etg.Yams.Storage.Config;
using Etg.Yams.Utils;
using FileMode = Etg.Yams.Storage.FileMode;

namespace Etg.Yams.Test.Storage
{
    public class LocalYamsRepository : IYamsRepository
    {
        private readonly string _path;
        private readonly string _deploymentConfigPath;

        public LocalYamsRepository(string path)
        {
            _path = path;
            _deploymentConfigPath = Path.Combine(_path, Constants.DeploymentConfigFileName);
        }

        public Task<DeploymentConfig> FetchDeploymentConfig()
        {
            string data = File.ReadAllText(_deploymentConfigPath);
            return Task.FromResult(new DeploymentConfig(data));
        }

        public Task PublishDeploymentConfig(DeploymentConfig deploymentConfig)
        {
            File.WriteAllText(_deploymentConfigPath, deploymentConfig.RawData());
            return Task.CompletedTask;
        }

        public Task UploadApplicationBinaries(AppIdentity appIdentity, string localPath, FileMode fileMode)
        {
            if (FileUtils.DirectoryDoesntExistOrEmpty(localPath))
            {
                throw new BinariesNotFoundException(
                    $"Binaries were not be uploaded because they were not found at the given path {localPath}");
            }

            string destPath = GetBinariesPath(appIdentity);
            bool binariesExist = FileUtils.DirectoryDoesntExistOrEmpty(destPath);
            if (binariesExist)
            {
                if (fileMode == FileMode.DoNothingIfBinariesExist)
                {
                    return Task.CompletedTask;
                }
                if (fileMode == FileMode.FailIfBinariesExist)
                {
                    throw new DuplicateBinariesException();
                }
            }
            return FileUtils.CopyDir(_path, localPath, true);
        }

        private string GetBinariesPath(AppIdentity appIdentity)
        {
            return Path.Combine(_path, GetDeploymentRelativePath(appIdentity));
        }

        private static string GetDeploymentRelativePath(AppIdentity appIdentity)
        {
            return Path.Combine(appIdentity.Id, appIdentity.Version.ToString());
        }

        public Task DeleteApplicationBinaries(AppIdentity appIdentity)
        {
            string path = GetBinariesPath(appIdentity);
            if (FileUtils.DirectoryDoesntExistOrEmpty(path))
            {
                throw new BinariesNotFoundException(
                    $"Cannot delete binaries for application {appIdentity} because they were not found");
            }
            Directory.Delete(path, true);
            return Task.CompletedTask;
        }

        public Task<bool> HasApplicationBinaries(AppIdentity appIdentity)
        {
            string path = GetBinariesPath(appIdentity);
            return Task.FromResult(!FileUtils.DirectoryDoesntExistOrEmpty(path));
        }

        public async Task DownloadApplicationBinaries(AppIdentity appIdentity, string localPath, FileMode fileMode)
        {
            bool exists = !FileUtils.DirectoryDoesntExistOrEmpty(localPath);
            if (exists)
            {
                if (fileMode == FileMode.DoNothingIfBinariesExist)
                {
                    return;
                }
                if (fileMode == FileMode.FailIfBinariesExist)
                {
                    throw new DuplicateBinariesException(
                        $"Cannot download the binaries because the destination directory {localPath} contains files");
                }
            }

            string path = GetBinariesPath(appIdentity);
            if (FileUtils.DirectoryDoesntExistOrEmpty(path))
            {
                throw new BinariesNotFoundException($"The binaries were not found in the Yams repository");
            }
            await FileUtils.CopyDir(path, localPath, true);
        }
    }
}