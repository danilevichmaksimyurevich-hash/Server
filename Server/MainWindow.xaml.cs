using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Server
{
    public partial class MainWindow : Window
    {
        private Socket _listenSocket;
        private bool _isListening;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += async (s, e) => await StartServer();
        }

        private async Task StartServer()
        {
            try
            {
                var ip = IPAddress.Parse("127.0.0.1");
                var port = 8080;
                var endPoint = new IPEndPoint(ip, port);

                _listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _listenSocket.Bind(endPoint);
                _listenSocket.Listen(10);

                _isListening = true;
                Log("Сервер запущен на 127.0.0.1:8080");
                Log("Ожидание данных в формате: [2 бита opId][5 бит facultyId][1 бит eduForm][16 бит answers]");
                Log("");

                await AcceptClientsAsync();
            }
            catch (Exception ex)
            {
                Log($"Ошибка: {ex.Message}");
            }
        }

        private async Task AcceptClientsAsync()
        {
            while (_isListening)
            {
                try
                {
                    var clientSocket = await _listenSocket.AcceptAsync();
                    _ = HandleClientAsync(clientSocket);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log($"Ошибка при принятии клиента: {ex.Message}");
                }
            }
        }

        private async Task HandleClientAsync(Socket clientSocket)
        {
            try
            {
                var buffer = new byte[3]; // ожидаем ровно 3 байта
                var received = 0;

                Log($"══════════════════════════════════════════════════════════════");
                Log($"Клиент подключен: {clientSocket.RemoteEndPoint}");
                Log("");

                while (received < 3)
                {
                    var bytesRead = await clientSocket.ReceiveAsync(new ArraySegment<byte>(buffer, received, 3 - received), SocketFlags.None);
                    if (bytesRead == 0) break;
                    received += bytesRead;
                }

                if (received == 3)
                {
                    Log("╔══════════════════════════════════════════════════════════════╗");
                    Log("║              ПРИНЯТЫЕ ДАННЫЕ (3 БАЙТА)                       ║");
                    Log("╚══════════════════════════════════════════════════════════════╝");
                    Log("");

                    // Отображение сырых байтов
                    Log("СЫРЫЕ БАЙТЫ:");
                    for (int i = 0; i < buffer.Length; i++)
                    {
                        Log($"  Байт[{i}]: DEC={buffer[i],3} | HEX=0x{buffer[i]:X2} | BIN={Convert.ToString(buffer[i], 2).PadLeft(8, '0')}");
                    }
                    Log("");

                    // === РАЗБОР ПЕРВОГО БАЙТА ===
                    Log("╔══════════════════════════════════════════════════════════════╗");
                    Log("║                 РАЗБОР ПЕРВОГО БАЙТА                        ║");
                    Log("╚══════════════════════════════════════════════════════════════╝");

                    byte firstByte = buffer[0];
                    byte operationId = (byte)((firstByte >> 6) & 0b11);
                    byte facultyId = (byte)((firstByte >> 1) & 0b11111);
                    bool educationForm = (firstByte & 1) == 0;

                    Log($"  Бинарное представление: {Convert.ToString(firstByte, 2).PadLeft(8, '0')}");
                    Log($"  ├─ Биты 7-6 (ID операции): {Convert.ToString(operationId, 2).PadLeft(2, '0')} = {operationId} {(operationId == 0b01 ? "✓ корректный" : "✗ ожидался 01")}");
                    Log($"  ├─ Биты 5-1 (ID факультета): {Convert.ToString(facultyId, 2).PadLeft(5, '0')} = {facultyId}");
                    Log($"  └─ Бит 0 (форма обучения): {(educationForm ? 1 : 0)} = {(educationForm ? "очная" : "заочная")}");
                    Log("");

                    // === РАЗБОР ОТВЕТОВ ===
                    Log("╔══════════════════════════════════════════════════════════════╗");
                    Log("║              РАЗБОР ОТВЕТОВ (16 БИТ = 8 ВОПРОСОВ)           ║");
                    Log("╚══════════════════════════════════════════════════════════════╝");

                    ushort answersBits = (ushort)(buffer[1] | (buffer[2] << 8));

                    Log($"  Байт 1 (младший): {Convert.ToString(buffer[1], 2).PadLeft(8, '0')}");
                    Log($"  Байт 2 (старший): {Convert.ToString(buffer[2], 2).PadLeft(8, '0')}");
                    Log($"  Объединенное значение (16 бит): {Convert.ToString(answersBits, 2).PadLeft(16, '0')}");
                    Log("");

                    Log("  ПОБИТОВЫЙ РАЗБОР ОТВЕТОВ:");
                    int[] answers = new int[8];
                    for (int i = 0; i < 8; i++)
                    {
                        answers[i] = (answersBits >> (i * 2)) & 0b11;
                        int startBit = i * 2;
                        Log($"    Вопрос {i + 1}: биты {startBit,2}-{startBit + 1,2} = {Convert.ToString(answers[i], 2).PadLeft(2, '0')} = {answers[i]}");
                    }
                    Log("");

                    // === ИТОГОВЫЕ ДАННЫЕ ===
                    Log("╔══════════════════════════════════════════════════════════════╗");
                    Log("║                  ИТОГОВЫЕ ДАННЫЕ                             ║");
                    Log("╚══════════════════════════════════════════════════════════════╝");
                    Log($"  ID операции: {operationId} {(operationId == 0b01 ? "(отправка ответов)" : "(неизвестная операция)")}");
                    Log($"  Факультет ID: {facultyId}");
                    Log($"  Форма обучения: {(educationForm ? "очная" : "заочная")}");
                    Log($"  Ответы на вопросы:");
                    for (int i = 0; i < 8; i++)
                    {
                        Log($"    Вопрос {i + 1}: {answers[i]}");
                    }
                    Log("");

                    // Проверка валидности
                    if (operationId != 0b01)
                    {
                        Log("⚠ ВНИМАНИЕ: Получен неизвестный ID операции!");
                    }

                    // Отправляем подтверждение
                    var response = Encoding.UTF8.GetBytes("Успех");
                    await clientSocket.SendAsync(new ArraySegment<byte>(response), SocketFlags.None);
                    Log("✓ Отправлен ответ клиенту: \"Успех\"");
                }
                else
                {
                    Log($"✗ Ошибка: Получено недостаточно данных ({received} из 3 байт)");
                }

                Log("");
                Log($"Клиент отключен: {clientSocket.RemoteEndPoint}");
                Log("══════════════════════════════════════════════════════════════");

                clientSocket.Shutdown(SocketShutdown.Both);
            }
            catch (Exception ex)
            {
                Log($"✗ Ошибка при обработке клиента: {ex.Message}");
            }
            finally
            {
                clientSocket.Close();
            }
        }

        private void Log(string message)
        {
            Dispatcher.Invoke(() => lbLog.Items.Add(message));
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _isListening = false;
            _listenSocket?.Close();
        }
    }
}