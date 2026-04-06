using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ModernClient
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<QuestionModel> questions;

        public MainWindow()
        {
            InitializeComponent();
            InitializeQuestions();
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Анимация появления
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.5));
            this.BeginAnimation(OpacityProperty, fadeIn);
        }

        private void InitializeQuestions()
        {
            questions = new ObservableCollection<QuestionModel>
            {
                new QuestionModel { Text = "1. Удовлетворены ли вы качеством преподавания?" },
                new QuestionModel { Text = "2. Оцените доступность учебных материалов" },
                new QuestionModel { Text = "3. Насколько эффективна обратная связь с преподавателями?" },
                new QuestionModel { Text = "4. Удовлетворены ли вы техническим оснащением аудиторий?" },
                new QuestionModel { Text = "5. Оцените качество организации практических занятий" },
                new QuestionModel { Text = "6. Насколько комфортна психологическая атмосфера?" },
                new QuestionModel { Text = "7. Удовлетворены ли вы работой деканата?" },
                new QuestionModel { Text = "8. Оцените качество учебных программ в целом" }
            };

            questionsItemsControl.ItemsSource = questions;
        }

        private async void btnSend_Click(object sender, RoutedEventArgs e)
        {
            btnSend.IsEnabled = false;

            // Анимация кнопки
            var animation = new DoubleAnimation(0.95, TimeSpan.FromMilliseconds(100))
            {
                AutoReverse = true
            };
            btnSend.RenderTransform = new ScaleTransform();
            btnSend.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
            btnSend.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);

            await Task.Delay(100);

            tbResult.Text = "⏳ Отправка данных...\n";

            try
            {
                // Получение параметров
                byte facultyId = GetFacultyId();
                byte eduForm = GetEduForm();

                // Получение ответов
                byte[] answers = new byte[8];
                for (int i = 0; i < questions.Count; i++)
                {
                    var container = questionsItemsControl.ItemContainerGenerator.ContainerFromIndex(i) as ContentPresenter;
                    if (container != null)
                    {
                        var comboBox = FindVisualChild<ComboBox>(container);
                        if (comboBox?.SelectedItem is ComboBoxItem selectedItem)
                        {
                            answers[i] = byte.Parse(selectedItem.Tag.ToString());
                        }
                        else
                        {
                            answers[i] = 0;
                        }
                    }
                }

                // Формирование заголовка
                byte operationId = 0;
                byte header = (byte)((operationId << 6) | (facultyId << 1) | eduForm);

                // Упаковка ответов
                ushort answersBits = 0;
                for (int i = 0; i < 8; i++)
                {
                    answersBits |= (ushort)(answers[i] << (i * 2));
                }

                byte[] data = new byte[3];
                data[0] = header;
                data[1] = (byte)(answersBits & 0xFF);
                data[2] = (byte)((answersBits >> 8) & 0xFF);

                // Отправка на сервер
                string serverResponse = await SendToServer(data);

                tbResult.Text += $"📡 Ответ сервера: {serverResponse}\n";

                if (serverResponse == "OK")
                {
                    tbResult.Text += "\n✅ Ответы успешно отправлены!";
                    await Task.Delay(1000);
                    ClearAnswers();
                }
                else
                {
                    tbResult.Text += $"\n⚠️ Сервер вернул: {serverResponse}";
                }
            }
            catch (Exception ex)
            {
                tbResult.Text += $"\n❌ Ошибка: {ex.Message}";
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnSend.IsEnabled = true;
            }
        }

        private async Task<string> SendToServer(byte[] data)
        {
            using (var tcpClient = new TcpClient())
            {
                await tcpClient.ConnectAsync("10.30.167.83", 34543);
                using (var networkStream = tcpClient.GetStream())
                {
                    await networkStream.WriteAsync(data, 0, data.Length);

                    var buffer = new byte[1024];
                    var response = new StringBuilder();
                    int bytesRead;

                    networkStream.ReadTimeout = 5000;

                    do
                    {
                        bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            response.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                        }
                    }
                    while (bytesRead > 0);

                    return response.ToString();
                }
            }
        }

        private byte GetFacultyId()
        {
            if (cmbFaculty.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag != null)
            {
                return byte.Parse(selectedItem.Tag.ToString());
            }
            return 0;
        }

        private byte GetEduForm()
        {
            if (cmbEduForm.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag != null)
            {
                return byte.Parse(selectedItem.Tag.ToString());
            }
            return 1;
        }

        private void ClearAnswers()
        {
            foreach (var question in questions)
            {
                question.Answer = 0;
            }

            // Сброс ComboBox в первое значение
            for (int i = 0; i < questions.Count; i++)
            {
                var container = questionsItemsControl.ItemContainerGenerator.ContainerFromIndex(i) as ContentPresenter;
                if (container != null)
                {
                    var comboBox = FindVisualChild<ComboBox>(container);
                    if (comboBox != null)
                    {
                        comboBox.SelectedIndex = 0;
                    }
                }
            }
        }

        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                    return result;

                var descendant = FindVisualChild<T>(child);
                if (descendant != null)
                    return descendant;
            }
            return null;
        }
    }

    public class QuestionModel : INotifyPropertyChanged
    {
        private int _answer;

        public string Text { get; set; }

        public int Answer
        {
            get => _answer;
            set
            {
                _answer = value;
                OnPropertyChanged(nameof(Answer));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}