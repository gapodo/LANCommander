﻿using LANCommander.SDK.Enums;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LANCommander.SDK
{
    public class GameManager
    {
        private readonly ILogger Logger;
        private Client Client { get; set; }
        private string DefaultInstallDirectory { get; set; }

        public delegate void OnArchiveEntryExtractionProgressHandler(object sender, ArchiveEntryExtractionProgressArgs e);
        public event OnArchiveEntryExtractionProgressHandler OnArchiveEntryExtractionProgress;

        public delegate void OnArchiveExtractionProgressHandler(long position, long length);
        public event OnArchiveExtractionProgressHandler OnArchiveExtractionProgress;

        private TrackableStream Stream;
        private IReader Reader;

        public GameManager(Client client, string defaultInstallDirectory)
        {
            Client = client;
            DefaultInstallDirectory = defaultInstallDirectory;
        }

        public GameManager(Client client, string defaultInstallDirectory, ILogger logger)
        {
            Client = client;
            DefaultInstallDirectory = defaultInstallDirectory;
            Logger = logger;
        }

        /// <summary>
        /// Downloads, extracts, and runs post-install scripts for the specified game
        /// </summary>
        /// <param name="game">Game to install</param>
        /// <param name="maxAttempts">Maximum attempts in case of transmission error</param>
        /// <returns>Final install path</returns>
        /// <exception cref="Exception"></exception>
        public string Install(Guid gameId, int maxAttempts = 10)
        {
            GameManifest manifest = null;

            var game = Client.GetGame(gameId);

            var destination = Path.Combine(DefaultInstallDirectory, game.Title.SanitizeFilename());

            try
            {
                if (ManifestHelper.Exists(destination))
                    manifest = ManifestHelper.Read(destination);
            }
            catch (Exception ex)
            {
                Logger?.LogTrace(ex, "Error reading manifest before install");
            }

            if (manifest == null || manifest.Id != gameId)
            {
                Logger?.LogTrace("Installing game {GameTitle} ({GameId})", game.Title, game.Id);

                var result = RetryHelper.RetryOnException<ExtractionResult>(maxAttempts, TimeSpan.FromMilliseconds(500), new ExtractionResult(), () =>
                {
                    Logger?.LogTrace("Attempting to download and extract game");

                    return DownloadAndExtract(game, destination);
                });

                if (!result.Success && !result.Canceled)
                    throw new Exception("Could not extract the installer. Retry the install or check your connection");
                else if (result.Canceled)
                    return "";

                game.InstallDirectory = result.Directory;
            }
            else
            {
                Logger?.LogTrace("Game {GameTitle} ({GameId}) is already installed to {InstallDirectory}", game.Title, game.Id, destination);

                game.InstallDirectory = destination;
            }

            var writeManifestSuccess = RetryHelper.RetryOnException(maxAttempts, TimeSpan.FromSeconds(1), false, () =>
            {
                Logger?.LogTrace("Attempting to get game manifest");

                manifest = Client.GetGameManifest(game.Id);

                ManifestHelper.Write(manifest, game.InstallDirectory);

                return true;
            });

            if (!writeManifestSuccess)
                throw new Exception("Could not grab the manifest file. Retry the install or check your connection");

            Logger?.LogTrace("Saving scripts");

            ScriptHelper.SaveScript(game, ScriptType.Install);
            ScriptHelper.SaveScript(game, ScriptType.Uninstall);
            ScriptHelper.SaveScript(game, ScriptType.NameChange);
            ScriptHelper.SaveScript(game, ScriptType.KeyChange);

            return game.InstallDirectory;
        }

        public void Uninstall(string installDirectory)
        {

            Logger?.LogTrace("Attempting to delete the install directory");

            if (Directory.Exists(installDirectory))
                Directory.Delete(installDirectory, true);

            Logger?.LogTrace("Deleted install directory {InstallDirectory}", installDirectory);
        }

        private ExtractionResult DownloadAndExtract(Game game, string destination)
        {
            if (game == null)
            {
                Logger?.LogTrace("Game failed to download, no game was specified");

                throw new ArgumentNullException("No game was specified");
            }

            Logger?.LogTrace("Downloading and extracting {Game} to path {Destination}", game.Title, destination);

            var extractionResult = new ExtractionResult
            {
                Canceled = false,
            };

            try
            {
                Directory.CreateDirectory(destination);

                Stream = Client.StreamGame(game.Id);
                Reader = ReaderFactory.Open(Stream);

                Stream.OnProgress += (pos, len) =>
                {
                    OnArchiveExtractionProgress?.Invoke(pos, len);
                };

                Reader.EntryExtractionProgress += (object sender, ReaderExtractionEventArgs<IEntry> e) =>
                {
                    OnArchiveEntryExtractionProgress?.Invoke(this, new ArchiveEntryExtractionProgressArgs
                    {
                        Entry = e.Item,
                        Progress = e.ReaderProgress,
                    });
                };

                while (Reader.MoveToNextEntry())
                {
                    if (Reader.Cancelled)
                        break;

                    Reader.WriteEntryToDirectory(destination, new ExtractionOptions()
                    {
                        ExtractFullPath = true,
                        Overwrite = true,
                        PreserveFileTime = true,
                    });
                }

                Reader.Dispose();
                Stream.Dispose();
            }
            catch (ReaderCancelledException ex)
            {
                Logger?.LogTrace("User cancelled the download");

                extractionResult.Canceled = true;

                if (Directory.Exists(destination))
                {
                    Logger?.LogTrace("Cleaning up orphaned files after cancelled install");

                    Directory.Delete(destination, true);
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Could not extract to path {Destination}", destination);

                if (Directory.Exists(destination))
                {
                    Logger?.LogTrace("Cleaning up orphaned install files after bad install");

                    Directory.Delete(destination, true);
                }

                throw new Exception("The game archive could not be extracted, is it corrupted? Please try again");
            }

            if (!extractionResult.Canceled)
            {
                extractionResult.Success = true;
                extractionResult.Directory = destination;

                Logger?.LogTrace("Game {Game} successfully downloaded and extracted to {Destination}", game.Title, destination);
            }

            return extractionResult;
        }

        public void CancelInstall()
        {
            Reader?.Cancel();
            // Reader?.Dispose();
            // Stream?.Dispose();
        }
    }
}
