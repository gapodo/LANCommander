﻿using LANCommander.Data.Models;
using LANCommander.Extensions;
using LANCommander.Models;
using LANCommander.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Controllers.Api
{
    [Authorize(AuthenticationSchemes = "Bearer")]
    [Route("api/[controller]")]
    [ApiController]
    public class RedistributableController : ControllerBase
    {
        private readonly RedistributableService RedistributableService;
        private readonly LANCommanderSettings Settings = SettingService.GetSettings();

        public RedistributableController(RedistributableService redistributableService)
        {

            RedistributableService = redistributableService;
        }

        [HttpGet]
        public async Task<IEnumerable<Redistributable>> Get()
        {
            return await RedistributableService.Get();
        }

        [HttpGet("{id}")]
        public async Task<Redistributable> Get(Guid id)
        {
            return await RedistributableService.Get(id);
        }

        [HttpGet("{id}/Download")]
        public async Task<IActionResult> Download(Guid id)
        {
            var redistributable = await RedistributableService.Get(id);

            if (redistributable == null)
                return NotFound();

            if (redistributable.Archives == null || redistributable.Archives.Count == 0)
                return NotFound();

            var archive = redistributable.Archives.OrderByDescending(a => a.CreatedOn).First();

            var filename = Path.Combine(Settings.Archives.StoragePath, archive.ObjectKey);

            if (!System.IO.File.Exists(filename))
                return NotFound();

            return File(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read), "application/octet-stream", $"{redistributable.Name.SanitizeFilename()}.zip");
        }
    }
}
