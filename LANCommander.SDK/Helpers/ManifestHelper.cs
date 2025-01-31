﻿using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;

namespace LANCommander.SDK.Helpers
{
    public static class ManifestHelper
    {
        public static readonly ILogger Logger;

        public const string ManifestFilename = "_manifest.yml";

        public static bool Exists(string installDirectory)
        {
            var path = GetPath(installDirectory);

            return File.Exists(path);
        }

        public static GameManifest Read(string installDirectory)
        {
            var source = GetPath(installDirectory);
            var yaml = File.ReadAllText(source);

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(new PascalCaseNamingConvention())
                .Build();

            Logger?.LogTrace("Deserializing manifest");

            var manifest = deserializer.Deserialize<GameManifest>(yaml);

            return manifest;
        }

        public static string Write(GameManifest manifest, string installDirectory)
        {
            var destination = GetPath(installDirectory);

            Logger?.LogTrace("Attempting to write manifest to path {Destination}", destination);

            var yaml = Serialize(manifest);

            Logger?.LogTrace("Writing manifest file");

            File.WriteAllText(destination, yaml);

            return destination;
        }

        public static string Serialize(GameManifest manifest)
        {
            var serializer = new SerializerBuilder()
                .WithNamingConvention(new PascalCaseNamingConvention())
                .Build();

            Logger?.LogTrace("Serializing manifest");

            var yaml = serializer.Serialize(manifest);

            return yaml;
        }

        public static string GetPath(string installDirectory)
        {
            return Path.Combine(installDirectory, ManifestFilename);
        }
    }
}
