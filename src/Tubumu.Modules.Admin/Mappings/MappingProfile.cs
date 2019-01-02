﻿using AutoMapper;
using Tubumu.Modules.Admin.Entities;
using XM = Tubumu.Modules.Admin.Models;

namespace Tubumu.Modules.Admin.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<Bulletin, XM.Bulletin>();
            CreateMap<XM.Bulletin, Bulletin>();
            CreateMap<XM.Bulletin, XM.Input.BulletinInput>();

            CreateMap<Permission, XM.Permission>();
            CreateMap<XM.Permission, Permission>();

            CreateMap<Region, XM.RegionInfoBase>();
        }
    }
}
