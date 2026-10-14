using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WPF_Zoo_Manager.Data;
using WPF_Zoo_Manager.Models;

namespace WPF_Zoo_Manager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ListBox? _activeListBox;
        private object? _editingItem;

        private static readonly DbContextOptions<ZooContext> DbOptions =
            new DbContextOptionsBuilder<ZooContext>()
                .UseSqlServer(
                    "Server=(localdb)\\MSSQLLocalDB;" +
                    "Database=LocalDB;" +
                    "Integrated Security=True;" +
                    "TrustServerCertificate=True;")
                .Options;

        public MainWindow()
        {
            InitializeComponent();

            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                await LoadZoosAsync();
                await LoadAnimalsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Database error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task LoadAssociatedAnimalsAsync(int zooId)
        {
            await using var db = new ZooContext(DbOptions);

            var animals = await db.Animals
                .AsNoTracking()
                .Where(a => db.ZooAnimals.Any(za =>
                    za.ZooId == zooId &&
                    za.AnimalId == a.Id))
                .OrderBy(a => a.Name)
                .ToListAsync();

            // Prevent stale asynchronous results from appearing
            // if the user selected another zoo.
            if (ZooListBox.SelectedItem is Zoo selectedZoo &&
                selectedZoo.Id == zooId)
            {
                AssociatedAnimalsListBox.ItemsSource = animals;
            }
        }

        private async void ZooListBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            EditSource_SelectionChanged(sender, e);

            AssociatedAnimalsListBox.ItemsSource = null;

            if (ZooListBox.SelectedItem is not Zoo selectedZoo)
                return;

            try
            {
                await LoadAssociatedAnimalsAsync(selectedZoo.Id);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Database error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task LoadZoosAsync()
        {
            await using var db = new ZooContext(DbOptions);

            ZooListBox.ItemsSource = await db.Zoos
                .AsNoTracking()
                .OrderBy(z => z.Location)
                .ToListAsync();
        }

        private async void DeleteZoo_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (ZooListBox.SelectedItem is not Zoo selectedZoo)
                return;

            int zooId = selectedZoo.Id;

            var confirmation = MessageBox.Show(
                this,
                $"Delete zoo '{selectedZoo.Location}'?\n" +
                " Its animal associations will also be removed.",
                "Confirm deletion",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.Yes)
                return;

            try
            {
                await using var db = new ZooContext(DbOptions);

                int deleted = await db.Zoos
                    .Where(z => z.Id == zooId)
                    .ExecuteDeleteAsync();

                if (deleted == 0)
                {
                    MessageBox.Show("Zoo no longer exists.");
                    return;
                }

                // Refresh the zoo list.
                await LoadZoosAsync();

                AssociatedAnimalsListBox.ItemsSource = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Database operation error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void AddZoo_Click(object sender, RoutedEventArgs e)
        {
            string location = MyTextBox.Text.Trim();

            if (location.Length is 0 or > 50)
            {
                MessageBox.Show(
                    this,
                    "Zoo location must contain 1–50 characters.",
                    "Invalid location",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                await using var db = new ZooContext(DbOptions);

                var zoo = new Zoo
                {
                    Location = location
                };

                db.Zoos.Add(zoo);
                await db.SaveChangesAsync();

                // Reload the master list.
                await LoadZoosAsync();

                // Select the newly inserted record.
                ZooListBox.SelectedValue = zoo.Id;
            }
            catch (DbUpdateException ex)
            {
                MessageBox.Show(
                    this,
                    ex.GetBaseException().Message,
                    "Database insert error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Database error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        private async void AddAnimalToZoo_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (ZooListBox.SelectedItem is not Zoo zoo ||
                AnimalsListBox.SelectedItem is not Animal animal)
            {
                MessageBox.Show(
                    this,
                    "Please select both a zoo and an animal.",
                    "Selection required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            int zooId = zoo.Id;
            int animalId = animal.Id;

            try
            {
                await using var db = new ZooContext(DbOptions);

                // Check whether this association already exists.
                bool exists = await db.ZooAnimals.AnyAsync(za =>
                    za.ZooId == zooId &&
                    za.AnimalId == animalId);

                if (exists)
                {
                    MessageBox.Show(
                        this,
                        "This animal is already associated with this zoo.",
                        "Duplicate association",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                // Insert the many-to-many association.
                db.ZooAnimals.Add(new ZooAnimal
                {
                    ZooId = zooId,
                    AnimalId = animalId
                });

                await db.SaveChangesAsync();

                // Refresh associated animals from the database.
                await LoadAssociatedAnimalsAsync(zooId);
            }
            catch (DbUpdateException ex)
                when (ex.GetBaseException() is
                    Microsoft.Data.SqlClient.SqlException
                { Number: 2601 or 2627 })
            {
                MessageBox.Show(
                    this,
                    "This animal is already associated with this zoo.",
                    "Duplicate association",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Database operation error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task LoadAnimalsAsync()
        {
            await using var db = new ZooContext(DbOptions);

            AnimalsListBox.ItemsSource = await db.Animals
                .AsNoTracking()
                .OrderBy(a => a.Name)
                .ToListAsync();
        }

        private async void DeleteAnimal_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (AnimalsListBox.SelectedItem is not Animal selectedAnimal)
                return;

            int animalId = selectedAnimal.Id;

            var confirmation = MessageBox.Show(
                this,
                $"Delete animal '{selectedAnimal.Name}'?\n" +
                " This will also remove its associations with ALL zoos.",
                "Confirm deletion",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.Yes)
                return;

            try
            {
                await using var db = new ZooContext(DbOptions);

                // Delete the animal. SQL Server cascades the deletion
                // to all related ZooAnimal records.
                int deleted = await db.Animals
                    .Where(a => a.Id == animalId)
                    .ExecuteDeleteAsync();

                if (deleted == 0)
                {
                    MessageBox.Show(
                        this,
                        "Animal no longer exists.",
                        "Deletion",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                // Refresh the list of all animals.
                await LoadAnimalsAsync();

                // Refresh animals associated with the selected zoo.
                AssociatedAnimalsListBox.ItemsSource = null;

                if (ZooListBox.SelectedItem is Zoo selectedZoo)
                {
                    await LoadAssociatedAnimalsAsync(selectedZoo.Id);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Database deletion error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void AddAnimal_Click(
            object sender,
            RoutedEventArgs e)
        {
            string name = MyTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(name) || name.Length > 50)
            {
                MessageBox.Show(
                    this,
                    "Animal name must contain 1–50 characters.",
                    "Invalid animal name",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                await using var db = new ZooContext(DbOptions);

                var animal = new Animal
                {
                    Name = name
                };

                db.Animals.Add(animal);
                await db.SaveChangesAsync();

                // Reload all animals.
                await LoadAnimalsAsync();

                // Select the newly inserted animal.
                AnimalsListBox.SelectedValue = animal.Id;
            }
            catch (DbUpdateException ex)
            {
                MessageBox.Show(
                    this,
                    ex.GetBaseException().Message,
                    "Database insert error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Database error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void RemoveAnimal_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (ZooListBox.SelectedItem is not Zoo selectedZoo ||
                AssociatedAnimalsListBox.SelectedItem is not Animal selectedAnimal)
            {
                MessageBox.Show(
                    this,
                    "Please select a zoo and one of its associated animals.",
                    "Selection required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            int zooId = selectedZoo.Id;
            int animalId = selectedAnimal.Id;

            var confirmation = MessageBox.Show(
                this,
                $"Remove '{selectedAnimal.Name}' from " +
                $"'{selectedZoo.Location}'?\n" +
                "The animal itself will not be deleted.",
                "Confirm removal",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmation != MessageBoxResult.Yes)
                return;

            try
            {
                await using var db = new ZooContext(DbOptions);

                // Delete only the association from ZooAnimal.
                int removed = await db.ZooAnimals
                    .Where(za =>
                        za.ZooId == zooId &&
                        za.AnimalId == animalId)
                    .ExecuteDeleteAsync();

                if (removed == 0)
                {
                    MessageBox.Show(
                        this,
                        "This association no longer exists.",
                        "Removal",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                // Refresh the associated animals for the selected zoo.
                await LoadAssociatedAnimalsAsync(zooId);

                if (ZooListBox.SelectedItem is Zoo currentZoo &&
                    currentZoo.Id == zooId &&
                    _activeListBox == AssociatedAnimalsListBox)
                {
                    SetEditingSource(AssociatedAnimalsListBox);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Database operation error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void UpdateZoo_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_activeListBox != ZooListBox ||
                _editingItem is not Zoo zoo)
                return;

            string location = MyTextBox.Text.Trim();

            if (location.Length is 0 or > 50)
            {
                MessageBox.Show("Location must contain 1–50 characters.");
                return;
            }

            try
            {
                await using var db = new ZooContext(DbOptions);

                int updated = await db.Zoos
                    .Where(z => z.Id == zoo.Id)
                    .ExecuteUpdateAsync(setters =>
                        setters.SetProperty(
                            z => z.Location,
                            location));

                if (updated == 0)
                {
                    MessageBox.Show("Zoo no longer exists.");
                    return;
                }

                zoo.Location = location;
                ZooListBox.Items.Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Database update error");
            }
        }

        private async void UpdateAnimal_Click(
            object sender,
            RoutedEventArgs e)
        {
            if ((_activeListBox != AnimalsListBox &&
                 _activeListBox != AssociatedAnimalsListBox) ||
                _editingItem is not Animal animal)
                return;

            string name = MyTextBox.Text.Trim();

            if (name.Length is 0 or > 50)
            {
                MessageBox.Show("Name must contain 1–50 characters.");
                return;
            }

            try
            {
                await using var db = new ZooContext(DbOptions);

                int updated = await db.Animals
                    .Where(a => a.Id == animal.Id)
                    .ExecuteUpdateAsync(setters =>
                        setters.SetProperty(
                            a => a.Name,
                            name));

                if (updated == 0)
                {
                    MessageBox.Show("Animal no longer exists.");
                    return;
                }

                // Synchronize both ListBoxes, since they may hold
                // different Animal objects representing the same row.
                foreach (var list in new[]
                {
                     AnimalsListBox,
                     AssociatedAnimalsListBox
                })
                {
                    foreach (var item in list.Items
                                 .OfType<Animal>()
                                 .Where(a => a.Id == animal.Id))
                    {
                        item.Name = name;
                    }

                    list.Items.Refresh();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Database update error");
            }
        }

        private void SetEditingSource(ListBox listBox)
        {
            _activeListBox = listBox;
            _editingItem = listBox.SelectedItem;

            MyTextBox.Text = _editingItem switch
            {
                Zoo zoo => zoo.Location,
                Animal animal => animal.Name ?? "",
                _ => ""
            };

            UpdateZooButton.IsEnabled = _editingItem is Zoo;
            UpdateAnimalButton.IsEnabled = _editingItem is Animal;
        }

        private void EditSource_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (sender is ListBox listBox &&
                (e.AddedItems.Count > 0 ||
                 ReferenceEquals(_activeListBox, listBox)))
            {
                SetEditingSource(listBox);
            }
        }

        private void EditSource_GotKeyboardFocus(
            object sender,
            KeyboardFocusChangedEventArgs e)
        {
            if (sender is ListBox listBox)
                SetEditingSource(listBox);
        }
    }
}
