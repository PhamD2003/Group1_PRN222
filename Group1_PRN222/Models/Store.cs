﻿using System;
using System.Collections.Generic;

namespace Group1_PRN222.Models;

public partial class Store
{
    public int Id { get; set; }

    public int? SellerId { get; set; }

    public string? StoreName { get; set; }

    public string? Description { get; set; }

    public string? BannerImageUrl { get; set; }

    public virtual User? Seller { get; set; }
}
