﻿using LANCommander.Data;
using LANCommander.Extensions;
using LANCommander.Models;
using LANCommander.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class DownloadController : Controller
    {
        private readonly ArchiveService ArchiveService;
        private readonly LANCommanderSettings Settings = SettingService.GetSettings();

        public DownloadController(ArchiveService archiveService)
        {
            ArchiveService = archiveService;
        }

        public async Task<IActionResult> Archive(Guid id)
        {
            var archive = await ArchiveService.Get(id);

            if (archive == null)
                return NotFound();

            var filename = Path.Combine(Settings.Archives.StoragePath, archive.ObjectKey);

            if (!System.IO.File.Exists(filename))
                return NotFound();

            string name = "";

            if (archive.GameId != null && archive.GameId != Guid.Empty)
                name = $"{archive.Game.Title.SanitizeFilename()}.zip";
            else if (archive.RedistributableId != null && archive.RedistributableId != Guid.Empty)
                name = $"{archive.Redistributable.Name.SanitizeFilename()}.zip";

            return File(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read), "application/octet-stream", name);
        }
    }
}
