﻿using System;
using System.Linq;
using System.Reflection;
using AutoMapper;

namespace Tubumu.Modules.Framework.Mappings
{
    public static class Initalizer
    {
        public static void Initialize()
        {
            var assembliesToScan = AppDomain.CurrentDomain.GetAssemblies();

            var allTypes = assembliesToScan
                .Where(a => a.GetName().Name != nameof(AutoMapper))
                .SelectMany(a => a.DefinedTypes)
                .ToArray();

            var profileTypeInfo = typeof(Profile).GetTypeInfo();
            var profiles = allTypes
                .Where(t => profileTypeInfo.IsAssignableFrom(t) && !t.IsAbstract)
                .Select(t => t.AsType())
                .ToArray();

            Mapper.Initialize(cfg =>
            {
                foreach (var profile in profiles)
                {
                    cfg.AddProfile(profile);
                }
            });
        }
    }
}
