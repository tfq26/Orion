using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Orion.Core.Services
{
    public interface IBuildService
    {
        Task<bool> BuildAsync(Guid appId, string appName, string repoUrl, Guid jobId, string ownerId, string? buildCommand = null, string? runCommand = null, string? buildFolder = null);
        Task<List<string>> GetRepoDirectoriesAsync(string repoUrl);
        Task<string?> GetLatestRevisionAsync(string repoUrl);
    }

    public class BuildService : IBuildService
    {
        private const string LocalUploadScheme = "orion-upload://";
        private readonly ILogger<BuildService> _logger;
        private readonly ILogService _logService;
        private readonly IStorageService _storage;

        public BuildService(ILogger<BuildService> logger, ILogService logService, IStorageService storage)
        {
            _logger = logger;
            _logService = logService;
            _storage = storage;
        }

        public async Task<bool> BuildAsync(Guid appId, string appName, string repoUrl, Guid jobId, string ownerId, string? buildCommand = null, string? runCommand = null, string? buildFolder = null)
        {
            var buildDir = Path.Combine(Path.GetTempPath(), "orion-builds", jobId.ToString());
            
            async Task LogAsync(string message, string level = "Info")
            {
                _logger.Log(level == "Error" ? LogLevel.Error : LogLevel.Information, message);
                await _logService.LogAsync(appId, jobId, ownerId, message, level);
            }

            try
            {
                await LogAsync($"Starting build for {appName} (Job: {jobId})");
                
                var canonicalName = repoUrl.Contains(":") ? repoUrl.Split(":")[0] : repoUrl;
                var sourceHash = ComputeHash(canonicalName);
                
                if (Directory.Exists(buildDir))
                    Directory.Delete(buildDir, true);
                
                Directory.CreateDirectory(buildDir);

                bool buildResult;

                if (IsLocalUploadSource(repoUrl))
                {
                    var sourceDir = ResolveLocalUploadDirectory(repoUrl);
                    await LogAsync($"[SOURCE] Using local uploaded source from {sourceDir}");
                    buildResult = await CopyDirectoryAsync(sourceDir, buildDir, LogAsync);
                }
                else
                {
                    // 1. Clone Repo
                    await LogAsync($"Cloning {repoUrl} into {buildDir}");
                    buildResult = await ExecuteProcessAsync("git", $"clone {repoUrl} .", buildDir, LogAsync);
                }
                
                if (!buildResult)
                {
                    await LogAsync(IsLocalUploadSource(repoUrl) ? "[BUILD] Local source preparation failed" : "[BUILD] Clone failed", "Error");
                    return false;
                }

                // 2. Resolve Build Command
                if (string.IsNullOrEmpty(buildCommand))
                {
                    if (File.Exists(Path.Combine(buildDir, "package.json")))
                    {
                        await LogAsync("[BUILD] package.json detected. Using default node build sequence.");
                        buildCommand = "bun install && bun run build"; // Prefer bun for speed, fallback to npm if needed
                    }
                    else
                    {
                        await LogAsync("[BUILD] No build command provided and no package.json found. Skipping build step.");
                        buildCommand = "echo 'No build required'";
                    }
                }

                // 3. Execute Build Command
                await LogAsync($"[BUILD] Executing: {buildCommand}");
                
                // Note: In a real world scenario, we'd use a containerized builder (Docker/Podman) 
                // to execute the command safely. For this demo, we execute it in a shell.
                buildResult = await ExecuteProcessAsync("sh", $"-c \"{buildCommand.Replace("\"", "\\\"")}\"", buildDir, LogAsync);

                if (!buildResult)
                {
                    await LogAsync("[BUILD] command execution failed.", "Error");
                    return false;
                }

                // 4. Archive Artifact (Simulation of pushing WASM/Image)
                var artifactSourceDir = buildDir;
                if (!string.IsNullOrEmpty(buildFolder))
                {
                    artifactSourceDir = Path.Combine(buildDir, buildFolder);
                    if (!Directory.Exists(artifactSourceDir))
                    {
                        await LogAsync($"[BUILD] Specified build folder '{buildFolder}' not found. Falling back to root.", "Warning");
                        artifactSourceDir = buildDir;
                    }
                    else
                    {
                        await LogAsync($"[BUILD] Using specified build folder: {buildFolder}");
                    }
                }

                var storageBlobId = $"cache/blobs/{sourceHash}.wasm";
                await LogAsync($"[STORAGE] Archiving build artifact from {artifactSourceDir} to: {storageBlobId}");
                
                byte[] wasmMarker = { 0x00, 0x61, 0x73, 0x6d, 0x01, 0x00, 0x00, 0x00 };
                await _storage.SaveBlobAsync(storageBlobId, wasmMarker);

                await LogAsync($"Build successful for {appName}!");
                return true;
            }
            catch (Exception ex)
            {
                await LogAsync($"Build failed for {appName} (Job: {jobId}): {ex.Message}", "Error");
                return false;
            }
            finally
            {
                if (Directory.Exists(buildDir))
                    Directory.Delete(buildDir, true);
            }
        }

        public async Task<List<string>> GetRepoDirectoriesAsync(string repoUrl)
        {
            if (IsLocalUploadSource(repoUrl))
            {
                var sourceDir = ResolveLocalUploadDirectory(repoUrl);
                if (!Directory.Exists(sourceDir)) return new List<string>();

                return Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories)
                    .Select(d => Path.GetRelativePath(sourceDir, d))
                    .Where(d => !string.IsNullOrWhiteSpace(d))
                    .OrderBy(d => d)
                    .ToList();
            }

            var tempDir = Path.Combine(Path.GetTempPath(), "orion-explore", Guid.NewGuid().ToString());
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);

            try
            {
                // Blobless clone: ONLY download the tree structure, NO file contents (blobs)
                // This is extremely fast even for massive repositories.
                var success = await ExecuteProcessAsync("git", $"clone --filter=blob:none --no-checkout --depth 1 {repoUrl} .", tempDir, (m, l) => Task.CompletedTask);
                if (!success) return new List<string>();

                // Get all directories via git ls-tree
                var processInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "ls-tree -r --name-only HEAD",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    WorkingDirectory = tempDir
                };
                using var process = Process.Start(processInfo);
                if (process == null) return new List<string>();
                
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                var dirs = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(Path.GetDirectoryName)
                    .Where(d => !string.IsNullOrEmpty(d))
                    .Distinct()
                    .OrderBy(d => d)
                    .ToList();

                return dirs!;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to explore repository: {repoUrl}");
                return new List<string>();
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        public async Task<string?> GetLatestRevisionAsync(string repoUrl)
        {
            if (IsLocalUploadSource(repoUrl))
            {
                return null;
            }

            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                processInfo.ArgumentList.Add("ls-remote");
                processInfo.ArgumentList.Add(repoUrl);
                processInfo.ArgumentList.Add("HEAD");

                using var process = Process.Start(processInfo);
                if (process == null)
                {
                    return null;
                }

                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    var error = await process.StandardError.ReadToEndAsync();
                    _logger.LogWarning("Failed to resolve latest revision for {RepoUrl}: {Error}", repoUrl, error);
                    return null;
                }

                var revision = output
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Split('\t', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
                    .FirstOrDefault(hash => !string.IsNullOrWhiteSpace(hash));

                return revision;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to inspect remote revision for {RepoUrl}", repoUrl);
                return null;
            }
        }

        private string ComputeHash(string input)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(input);
            var hashBytes = sha.ComputeHash(bytes);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }

        private async Task<bool> ExecuteProcessAsync(string fileName, string arguments, string buildDir, Func<string, string, Task> logAsync)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = buildDir
            };

            using var process = new Process { StartInfo = processInfo };
            
            process.OutputDataReceived += async (sender, e) => { if (e.Data != null) await logAsync(e.Data, "Info"); };
            process.ErrorDataReceived += async (sender, e) => { if (e.Data != null) await logAsync(e.Data, "Error"); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            return process.ExitCode == 0;
        }

        private static bool IsLocalUploadSource(string repoUrl)
        {
            return repoUrl.StartsWith(LocalUploadScheme, StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveLocalUploadDirectory(string repoUrl)
        {
            var uploadId = repoUrl.Replace(LocalUploadScheme, "", StringComparison.OrdinalIgnoreCase);
            return Path.Combine(Directory.GetCurrentDirectory(), "uploaded_sources", uploadId);
        }

        private static async Task<bool> CopyDirectoryAsync(string sourceDir, string destinationDir, Func<string, string, Task> logAsync)
        {
            if (!Directory.Exists(sourceDir))
            {
                await logAsync($"[SOURCE] Uploaded source directory not found: {sourceDir}", "Error");
                return false;
            }

            foreach (var directory in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(directory.Replace(sourceDir, destinationDir));
            }

            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                var targetPath = file.Replace(sourceDir, destinationDir);
                var targetDirectory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }

                File.Copy(file, targetPath, overwrite: true);
            }

            await logAsync("[SOURCE] Local files copied into build workspace.", "Info");
            return true;
        }
    }
}
