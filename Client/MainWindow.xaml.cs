using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Client
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // Создаем поля для 8 вопросов
            CreateAnswerFields();
        }

        private void CreateAnswerFields()
        {
            // Создаем StackPanel для вопросов, если его нет
            var questionsPanel = new System.Windows.Controls.StackPanel();

            for (int i = 1; i <= 8; i++)
            {
                var panel = new System.Windows.Controls.StackPanel()
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    Margin = new Thickness(5)
                };

                var label = new System.Windows.Controls.Label()
                {
                    Content = $"Вопрос {i}:",
                    Width = 80,
                    Margin = new Thickness(5)
                };

                var textBox = new System.Windows.Controls.TextBox()
                {
                    Name = $"txtAnswer{i}",
                    Width = 50,
                    Margin = new Thickness(5),
                    Text = "0"
                };

                panel.Children.Add(label);
                panel.Children.Add(textBox);
                questionsPanel.Children.Add(panel);
            }

            // Добавляем панель в окно (нужно разместить в нужном месте XAML)
            // Для примера, предполагаем что есть StackPanel с именем QuestionsContainer
            if (this.FindName("QuestionsContainer") is System.Windows.Controls.StackPanel container)
            {
                container.Children.Add(questionsPanel);
            }
        }

        private async void btnSend_Click(object sender, RoutedEventArgs e)
        {
            btnSend.IsEnabled = false;
            tbResult.Text = "Отправка...\n";

            try
            {
                // 1. ID операции (всегда 01 для отправки ответов)
                byte operationId = 0b01; // 2 бита

                // 2. Чтение ID факультета (0-31)
                if (!byte.TryParse(txtFacultyId.Text, out byte facultyId) || facultyId > 31)
                {
                    tbResult.Text = "Ошибка: ID факультета должен быть целым числом от 0 до 31";
                    return;
                }

                // 3. Форма обучения (0 или 1)
                byte eduForm = (byte)(cbEducationForm.SelectedIndex == 0 ? 0 : 1);

                // 4. Чтение 8 ответов (каждый от 0 до 3)
                byte[] answers = new byte[8];
                for (int i = 0; i < 8; i++)
                {
                    var box = FindName($"txtAnswer{i + 1}") as System.Windows.Controls.TextBox;
                    if (box == null)
                    {
                        tbResult.Text = $"Внутренняя ошибка: не найдено поле для вопроса {i + 1}";
                        return;
                    }

                    if (!byte.TryParse(box.Text, out byte val))
                    {
                        tbResult.Text = $"Ошибка: вопрос {i + 1} должен содержать целое число";
                        return;
                    }
                    if (val > 3)
                    {
                        tbResult.Text = $"Ошибка: вопрос {i + 1} — значение не может быть больше 3";
                        return;
                    }
                    answers[i] = val;
                }

                // 5. Упаковка данных в 3 байта:
                //    Байт 0: [2 бита operationId | 5 бит facultyId | 1 бит eduForm]
                //    Байты 1-2: 8 вопросов по 2 бита (всего 16 бит)

                byte[] data = new byte[3];

                // Упаковка первого байта: operationId (2 бита) + facultyId (5 бит) + eduForm (1 бит)
                // Формат: [op1][op0][fac4][fac3][fac2][fac1][fac0][form]
                data[0] = (byte)((operationId << 6) | (facultyId << 1) | eduForm);

                // Упаковка ответов в 16 бит (байты 1 и 2)
                // Каждый вопрос занимает 2 бита
                ushort answersBits = 0;
                for (int i = 0; i < 8; i++)
                {
                    answersBits |= (ushort)(answers[i] << (i * 2));
                }

                // Записываем в little-endian формате (младший байт первый)
                data[1] = (byte)(answersBits & 0xFF);        // Младшие 8 бит
                data[2] = (byte)((answersBits >> 8) & 0xFF); // Старшие 8 бит

                // === ДЕТАЛЬНОЕ ОТОБРАЖЕНИЕ ОТПРАВЛЯЕМЫХ ДАННЫХ ===
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("╔══════════════════════════════════════════════════════════════╗");
                sb.AppendLine("║              ОТПРАВЛЯЕМЫЕ ДАННЫЕ (3 БАЙТА)                  ║");
                sb.AppendLine("╚══════════════════════════════════════════════════════════════╝\n");

                // Отображение сырых байтов
                sb.AppendLine("СЫРЫЕ БАЙТЫ:");
                for (int i = 0; i < data.Length; i++)
                {
                    sb.AppendLine($"  Байт[{i}]: DEC={data[i],3} | HEX=0x{data[i]:X2} | BIN={Convert.ToString(data[i], 2).PadLeft(8, '0')}");
                }

                // Разбор первого байта
                sb.AppendLine("\n╔══════════════════════════════════════════════════════════════╗");
                sb.AppendLine("║                 РАЗБОР ПЕРВОГО БАЙТА                        ║");
                sb.AppendLine("╚══════════════════════════════════════════════════════════════╝");

                byte extractedOpId = (byte)((data[0] >> 6) & 0b11);
                byte extractedFacultyId = (byte)((data[0] >> 1) & 0b11111);
                byte extractedEduForm = (byte)(data[0] & 0b1);

                sb.AppendLine($"  Бинарное представление: {Convert.ToString(data[0], 2).PadLeft(8, '0')}");
                sb.AppendLine($"  ├─ Биты 7-6 (ID операции): {Convert.ToString(extractedOpId, 2).PadLeft(2, '0')} = {extractedOpId} (должно быть 01)");
                sb.AppendLine($"  ├─ Биты 5-1 (ID факультета): {Convert.ToString(extractedFacultyId, 2).PadLeft(5, '0')} = {extractedFacultyId}");
                sb.AppendLine($"  └─ Бит 0 (форма обучения): {extractedEduForm} = {(extractedEduForm == 1 ? "очная" : "заочная")}");

                // Разбор ответов
                sb.AppendLine("\n╔══════════════════════════════════════════════════════════════╗");
                sb.AppendLine("║              РАЗБОР ОТВЕТОВ (16 БИТ = 8 ВОПРОСОВ)           ║");
                sb.AppendLine("╚══════════════════════════════════════════════════════════════╝");

                ushort receivedAnswersBits = (ushort)(data[1] | (data[2] << 8));
                sb.AppendLine($"  Байт 1 (младший): {Convert.ToString(data[1], 2).PadLeft(8, '0')}");
                sb.AppendLine($"  Байт 2 (старший): {Convert.ToString(data[2], 2).PadLeft(8, '0')}");
                sb.AppendLine($"  Объединенное значение (16 бит): {Convert.ToString(receivedAnswersBits, 2).PadLeft(16, '0')}");
                sb.AppendLine();

                sb.AppendLine("  ПОБИТОВЫЙ РАЗБОР ОТВЕТОВ:");
                for (int i = 0; i < 8; i++)
                {
                    int answer = (receivedAnswersBits >> (i * 2)) & 0b11;
                    int startBit = i * 2;
                    sb.AppendLine($"    Вопрос {i + 1}: биты {startBit}-{startBit + 1} = {Convert.ToString(answer, 2).PadLeft(2, '0')} = {answer}");
                }

                // Проверка корректности упаковки
                sb.AppendLine("\n╔══════════════════════════════════════════════════════════════╗");
                sb.AppendLine("║              ПРОВЕРКА КОРРЕКТНОСТИ УПАКОВКИ                ║");
                sb.AppendLine("╚══════════════════════════════════════════════════════════════╝");

                bool allCorrect = true;
                for (int i = 0; i < 8; i++)
                {
                    int unpacked = (receivedAnswersBits >> (i * 2)) & 0b11;
                    if (unpacked != answers[i])
                    {
                        sb.AppendLine($"  ✗ Вопрос {i + 1}: ожидалось {answers[i]}, получено {unpacked}");
                        allCorrect = false;
                    }
                    else
                    {
                        sb.AppendLine($"  ✓ Вопрос {i + 1}: {answers[i]} → упаковано корректно");
                    }
                }

                if (allCorrect)
                    sb.AppendLine("\n  ✓ ВСЕ ДАННЫЕ УПАКОВАНЫ КОРРЕКТНО!");
                else
                    sb.AppendLine("\n  ✗ ОБНАРУЖЕНЫ ОШИБКИ УПАКОВКИ!");

                tbResult.Text = sb.ToString();

                // === ОТПРАВКА НА СЕРВЕР ===
                tbResult.Text += "\n╔══════════════════════════════════════════════════════════════╗\n";
                tbResult.Text += "║                    ОТПРАВКА НА СЕРВЕР                      ║\n";
                tbResult.Text += "╚══════════════════════════════════════════════════════════════╝\n";

                using (var tcpClient = new TcpClient())
                {
                    await tcpClient.ConnectAsync("127.0.0.1", 8080);
                    tbResult.Text += $"✓ Подключение установлено\n";

                    using (var networkStream = tcpClient.GetStream())
                    {
                        await networkStream.WriteAsync(data, 0, data.Length);
                        tbResult.Text += $"✓ Отправлено {data.Length} байт данных\n";

                        // Получаем ответ от сервера
                        var buffer = new byte[1024];
                        var received = new StringBuilder();
                        int bytesRead;

                        networkStream.ReadTimeout = 5000;

                        tbResult.Text += "\n╔══════════════════════════════════════════════════════════════╗\n";
                        tbResult.Text += "║                    ОТВЕТ СЕРВЕРА                         ║\n";
                        tbResult.Text += "╚══════════════════════════════════════════════════════════════╝\n";

                        do
                        {
                            bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length);
                            if (bytesRead > 0)
                            {
                                received.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                            }
                        }
                        while (bytesRead > 0);

                        tbResult.Text += $"✓ Получен ответ: \"{received}\"\n";
                    }
                }

                tbResult.Text += "\n╔══════════════════════════════════════════════════════════════╗\n";
                tbResult.Text += "║                    ОПЕРАЦИЯ ЗАВЕРШЕНА                      ║\n";
                tbResult.Text += "╚══════════════════════════════════════════════════════════════╝";
            }
            catch (Exception ex)
            {
                tbResult.Text = $"╔══════════════════════════════════════════════════════════════╗\n";
                tbResult.Text += $"║                       ОШИБКА                               ║\n";
                tbResult.Text += $"╚══════════════════════════════════════════════════════════════╝\n";
                tbResult.Text += $"\n{ex.Message}";
            }
            finally
            {
                btnSend.IsEnabled = true;
            }
        }
    }
}