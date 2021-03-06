﻿using System.IO;
using System.Linq;
using System.Reflection;
using HidCerberus.Vigils.Core.YAML.Core;
using HidCerberus.Vigils.Core.YAML.Core.YAML;
using Serilog;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace HidCerberus.Vigils.Core.YAML.Public
{
    public class EntryPoint
    {
        static EntryPoint()
        {
            var assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.RollingFile(Path.Combine(assemblyFolder, "HidCerberus.Vigils.Core.YAML-{Date}.log"))
                .CreateLogger();

            var rulesFile = Path.Combine(assemblyFolder, @"rules.yaml");
            Log.Information("Looking for rules definition file {Rules}", rulesFile);

            if (!File.Exists(rulesFile))
            {
                Log.Fatal("File {Rules} not found, can't continue", rulesFile);
                throw new FileNotFoundException($"{rulesFile} is missing");
            }

            using (TextReader reader = File.OpenText(rulesFile))
            {
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(new CamelCaseNamingConvention())
                    .WithTypeConverter(new RuleFilterConverter())
                    .Build();

                Config = deserializer.Deserialize<Document>(reader);
            }

            Log.Information($"Loaded {Config.Rules.Count} rules");
        }

        private static Document Config { get; }

        /// <summary>
        ///     Gets called by HidCerberus when an access decision has to be made.
        /// </summary>
        /// <param name="hardwareId">The Hardware ID identifying </param>
        /// <param name="instanceId"></param>
        /// <param name="processId"></param>
        /// <param name="isAllowed"></param>
        /// <param name="isPermanent"></param>
        /// <param name="deviceId"></param>
        /// <returns></returns>
        public static bool ProcessAccessRequest(
            string hardwareId,
            string deviceId,
            string instanceId,
            uint processId,
            out bool isAllowed,
            out bool isPermanent
        )
        {
            var target = new Target
            {
                HardwareId = hardwareId,
                DeviceId = deviceId,
                InstanceId = instanceId
            };

            var match = Config.Rules.FirstOrDefault(r => target.Equals(r.Target));

            if (match != null && match.Filter.Validate((int) processId))
            {
                isAllowed = match.IsAllowed;
                isPermanent = match.IsPermanent;

                return true;
            }

            isAllowed = false;
            isPermanent = false;

            return false;
        }
    }
}