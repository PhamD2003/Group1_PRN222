﻿using System;
using System.Collections.Generic;

namespace Group1_PRN222.Models;

public partial class OrderItem
{
    public int Id { get; set; }

    public int? OrderId { get; set; }

    public int? ProductId { get; set; }

    public int? Quantity { get; set; }

    public decimal? UnitPrice { get; set; }

    public virtual OrderTable? Order { get; set; }

    public virtual Product? Product { get; set; }
}
