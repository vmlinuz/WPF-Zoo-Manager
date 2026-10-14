using Microsoft.EntityFrameworkCore;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WPF_Zoo_Manager.Data;
using WPF_Zoo_Manager.Models;

namespace WPF_Zoo_Manager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int MaximumInputLength = 50;

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
                ShowDatabaseError(ex, "Database error");
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
                ShowDatabaseError(ex, "Database error");
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
                ShowDatabaseError(ex, "Database operation error");
            }
        }

        private async void AddZoo_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetValidInput(
                    MyTextBox.Text,
                    "Zoo location",
                    "Invalid location",
                    out string location))
                return;

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
                ShowDatabaseError(ex, "Database insert error", useBaseException: true);
            }
            catch (Exception ex)
            {
                ShowDatabaseError(ex, "Database error");
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
                ShowDatabaseError(ex, "Database operation error");
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
                ShowDatabaseError(ex, "Database deletion error");
            }
        }

        private async void AddAnimal_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!TryGetValidInput(
                    MyTextBox.Text,
                    "Animal name",
                    "Invalid animal name",
                    out string name))
                return;

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
                ShowDatabaseError(ex, "Database insert error", useBaseException: true);
            }
            catch (Exception ex)
            {
                ShowDatabaseError(ex, "Database error");
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
                ShowDatabaseError(ex, "Database operation error");
            }
        }

        private async void UpdateZoo_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_activeListBox != ZooListBox ||
                _editingItem is not Zoo zoo)
                return;

            if (!TryGetValidInput(
                    MyTextBox.Text,
                    "Location",
                    errorTitle: null,
                    out string location))
                return;

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
                ShowDatabaseError(ex, "Database update error");
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

            if (!TryGetValidInput(
                    MyTextBox.Text,
                    "Name",
                    errorTitle: null,
                    out string name))
                return;

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
                ShowDatabaseError(ex, "Database update error");
            }
        }

        private bool TryGetValidInput(
            string input,
            string fieldName,
            string? errorTitle,
            out string value)
        {
            value = input.Trim();

            if (value.Length is > 0 and <= MaximumInputLength)
                return true;

            string message = $"{fieldName} must contain 1–{MaximumInputLength} characters.";

            if (errorTitle is null)
                MessageBox.Show(message);
            else
                MessageBox.Show(
                    this,
                    message,
                    errorTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

            return false;
        }

        private void ShowDatabaseError(
            Exception exception,
            string title,
            bool useBaseException = false)
        {
            string message = useBaseException
                ? exception.GetBaseException().Message
                : exception.Message;

            MessageBox.Show(
                this,
                message,
                title,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
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
