using System;
using System.Collections.Generic;

namespace WPF_Zoo_Manager.Models;

public partial class Zoo
{
    public int Id { get; set; }

    public string Location { get; set; } = null!;

    public virtual ICollection<ZooAnimal> ZooAnimals { get; set; } = new List<ZooAnimal>();
}
