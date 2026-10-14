using System;
using System.Collections.Generic;

namespace WPF_Zoo_Manager.Models;

public partial class Animal
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public virtual ICollection<ZooAnimal> ZooAnimals { get; set; } = new List<ZooAnimal>();
}
