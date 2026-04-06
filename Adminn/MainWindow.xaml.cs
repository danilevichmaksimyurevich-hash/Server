using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ModernClient
{
    public partial class AdminWindow : Window
    {
        private bool isPaused = false;
        private const string SERVER_IP = "10.30.167.83";
        private const int SERVER_PORT = 34543;

        public AdminWindow()
        {
            InitializeComponent();
            this.Loaded += AdminWindow_Loaded;
        }

        private void AdminWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0, 1,
                TimeSpan.FromSeconds(0.5));
            this.BeginAnimation(OpacityProperty, fadeIn);
        }

        private async void btnLoadStats_Click(object sender, RoutedEventArgs e)
        {
            await LoadStatistics(false);
        }

        private async void btnLoadAllStats_Click(object sender, RoutedEventArgs e)
        {
            await LoadStatistics(true);
        }

        private async Task LoadStatistics(bool allGroups)
        {
            try
            {
                tbStatsInfo.Text = "⏳ Загрузка статистики...";

                byte facultyId = 31;
                byte eduForm = 0;

                if (!allGroups)
                {
                    var selectedFaculty = cmbStatsFaculty.SelectedItem as ComboBoxItem;
                    if (selectedFaculty != null)
                    {
                        facultyId = byte.Parse(selectedFaculty.Tag.ToString());
                    }

                    var selectedForm = cmbStatsForm.SelectedItem as ComboBoxItem;
                    if (selectedForm != null)
                    {
                        eduForm = byte.Parse(selectedForm.Tag.ToString());
                    }
                }

                byte operationId = 1;
                byte header = (byte)((operationId << 6) | (facultyId << 1) | eduForm);

                using (var tcpClient = new TcpClient())
                {
                    await tcpClient.ConnectAsync(SERVER_IP, SERVER_PORT);
                    using (var networkStream = tcpClient.GetStream())
                    {
                        await networkStream.WriteAsync(new byte[] { header }, 0, 1);

                        byte[] statsRaw = new byte[128];
                        int bytesRead = await networkStream.ReadAsync(statsRaw, 0, 128);

                        if (bytesRead == 128)
                        {
                            uint[] stats = new uint[32];
                            for (int i = 0; i < 32; i++)
                            {
                                stats[i] = BitConverter.ToUInt32(statsRaw, i * 4);
                            }

                            DisplayStatistics(stats);
                            tbStatsInfo.Text = "✅ Статистика успешно загружена";
                        }
                        else
                        {
                            tbStatsInfo.Text = "❌ Ошибка: получены неполные данные";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                tbStatsInfo.Text = $"❌ Ошибка: {ex.Message}";
                MessageBox.Show($"Ошибка загрузки статистики: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DisplayStatistics(uint[] stats)
        {
            var statsList = new ObservableCollection<StatisticModel>();

            string[] questions = new string[]
            {
                "1. Качество преподавания",
                "2. Доступность учебных материалов",
                "3. Обратная связь с преподавателями",
                "4. Техническое оснащение аудиторий",
                "5. Организация практических занятий",
                "6. Психологическая атмосфера",
                "7. Работа деканата",
                "8. Качество учебных программ в целом"
            };

            for (int i = 0; i < 8; i++)
            {
                statsList.Add(new StatisticModel
                {
                    QuestionText = questions[i],
                    Satisfied = stats[i].ToString("N0"),
                    MostlySatisfied = stats[i + 8].ToString("N0"),
                    MostlyUnsatisfied = stats[i + 16].ToString("N0"),
                    Unsatisfied = stats[i + 24].ToString("N0")
                });
            }

            statsItemsControl.ItemsSource = statsList;
        }

        private async void btnResetStats_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Вы уверены, что хотите сбросить статистику?\n\nЭто действие необратимо!",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    byte facultyId = 31;
                    byte eduForm = 0;

                    var selectedFaculty = cmbStatsFaculty.SelectedItem as ComboBoxItem;
                    if (selectedFaculty != null && selectedFaculty.Tag.ToString() != "31")
                    {
                        facultyId = byte.Parse(selectedFaculty.Tag.ToString());
                        var selectedForm = cmbStatsForm.SelectedItem as ComboBoxItem;
                        eduForm = byte.Parse(selectedForm?.Tag.ToString() ?? "1");
                    }

                    byte operationId = 2;
                    byte header = (byte)((operationId << 6) | (facultyId << 1) | eduForm);

                    using (var tcpClient = new TcpClient())
                    {
                        await tcpClient.ConnectAsync(SERVER_IP, SERVER_PORT);
                        using (var networkStream = tcpClient.GetStream())
                        {
                            await networkStream.WriteAsync(new byte[] { header }, 0, 1);

                            var buffer = new byte[256];
                            int bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length);
                            string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                            MessageBox.Show($"Результат: {response}", "Сброс статистики",
                                MessageBoxButton.OK, MessageBoxImage.Information);

                            // Обновляем статистику после сброса
                            await LoadStatistics(false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка сброса статистики: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void btnTogglePause_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                byte header = (byte)(3 << 6);

                using (var tcpClient = new TcpClient())
                {
                    await tcpClient.ConnectAsync(SERVER_IP, SERVER_PORT);
                    using (var networkStream = tcpClient.GetStream())
                    {
                        await networkStream.WriteAsync(new byte[] { header }, 0, 1);

                        var buffer = new byte[256];
                        int bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length);
                        string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                        isPaused = response.Contains("Paused");

                        btnTogglePause.Content = isPaused ? "▶️ Возобновить приём опросов" : "⏸️ Приостановить приём опросов";
                        tbServerStatus.Text = isPaused ? "Приостановлен" : "Активен";
                        tbServerStatus.Foreground = isPaused ?
                            System.Windows.Media.Brushes.Orange :
                            System.Windows.Media.Brushes.Green;

                        MessageBox.Show($"Сервер: {response}", "Управление",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class StatisticModel : INotifyPropertyChanged
    {
        public string QuestionText { get; set; }
        public string Satisfied { get; set; }
        public string MostlySatisfied { get; set; }
        public string MostlyUnsatisfied { get; set; }
        public string Unsatisfied { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}