﻿using System;
using System.Collections.Generic;

namespace Tubumu.Modules.Admin.Entities
{
    public partial class GroupRole
    {
        public Guid GroupId { get; set; }
        public Guid RoleId { get; set; }

        public virtual Group Group { get; set; }
        public virtual Role Role { get; set; }
    }
}
