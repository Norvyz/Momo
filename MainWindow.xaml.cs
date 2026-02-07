using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using System.Speech.Recognition;
using NAudio.CoreAudioApi;

using Momo.Models;
using Momo.Services;

// 🔹 WinForms (solo para NotifyIcon)
using System.Windows.Forms;
using Application = System.Windows.Application;
using System.Windows.Interop;

namespace Momo
{
    public partial class MainWindow : Window
    {
        private readonly Random random = new();
        private MomoConfig config;

        // 🧠 Servicio de conversación
        private readonly ConversationService conversationService = new();

        // 🎤 Voz
        private SpeechRecognitionEngine? recognizer;
        private bool isListening = false;

        // 🔔 Tray icon
        private NotifyIcon? trayIcon;

        public MainWindow()
        {
            InitializeComponent();
            LoadConfig();
            InitVoiceRecognition();

            InitNotifyIcon();

            Loaded += (_, __) => SendToBack();
            Deactivated += (_, __) => SendToBack();
        }

        // 🔧 Cargar configuración + skin
        private void LoadConfig()
        {
            config = ConfigService.Load();

            if (!string.IsNullOrWhiteSpace(config.SkinPath)
                && File.Exists(config.SkinPath))
            {
                MomoImage.Source = new BitmapImage(
                    new Uri(config.SkinPath, UriKind.Absolute)
                );
            }
        }

        // 🖱️ Arrastrar
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        // 🦎 Click en Momo
        private void Momo_Click(object sender, MouseButtonEventArgs e)
        {
            InputPanel.Visibility = Visibility.Visible;
            UserInput.Focus();
        }

        // ⌨️ Enter
        private async void UserInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(UserInput.Text))
            {
                string text = UserInput.Text.Trim();
                UserInput.Clear();
                InputPanel.Visibility = Visibility.Collapsed;

                string response = await GetResponseAsync(text, fromVoice: false);
                await ShowMessage(response);
            }
        }

        // Placeholder
        private void UserInput_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UserInputPlaceholder.Visibility =
                string.IsNullOrWhiteSpace(UserInput.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // 🔑 Wake word
        private string WakeWord =>
            string.IsNullOrWhiteSpace(config.Name)
            ? "momo"
            : config.Name.ToLower();

        private bool IsWakeWordDetected(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            return input.Trim().ToLower().StartsWith(WakeWord);
        }

        // 🧠 Respuesta
        private async Task<string> GetResponseAsync(string text, bool fromVoice)
        {
            if (!fromVoice && !IsWakeWordDetected(text))
                return "🤐 No escuché mi nombre, no puedo reaccionar.";

            if (IsWakeWordDetected(text))
                text = text.Substring(WakeWord.Length).Trim();

            var packResponse = await TryRunPackAsync(text);
            if (packResponse != null)
                return packResponse;

            if (TryOpenApplication(text, out string appResponse))
                return appResponse;

            return conversationService.GetResponse(text);
        }

        // 📦 PACKS
        private async Task<string?> TryRunPackAsync(string input)
        {
            input = input.ToLower();

            if (!input.Contains("abre"))
                return null;

            var pack = config.Packs.FirstOrDefault(p => input.Contains(p.Keyword));
            if (pack == null)
                return null;

            if (pack.Apps.Count == 0)
                return $"El grupo {pack.Name} no tiene aplicaciones aún 🦎";

            await ShowMessage($"Ejecutando grupo {pack.Name} 🚀");

            foreach (var app in pack.Apps)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = app.ExecutablePath,
                        UseShellExecute = true
                    });
                }
                catch
                {
                    await ShowMessage($"No pude abrir {app.Keyword} 😕");
                }

                await Task.Delay(pack.DelaySeconds * 1000);
            }

            return $"Listo 😎 Ya ejecuté el grupo {pack.Name}";
        }

        // 🚀 APPS
        private bool TryOpenApplication(string input, out string response)
        {
            response = "";
            input = input.ToLower();

            if (!input.Contains("abre"))
                return false;

            foreach (var app in config.Apps)
            {
                if (input.Contains(app.Keyword))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = app.ExecutablePath,
                            UseShellExecute = true
                        });

                        response = $"{config.Name} abrió {app.Keyword} 🚀";
                        return true;
                    }
                    catch
                    {
                        response = $"Ups… no pude abrir {app.Keyword} 😕";
                        return true;
                    }
                }
            }

            response = "No conozco esa aplicación aún 🦎";
            return true;
        }

        // 💬 Mostrar mensaje
        private async Task ShowMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            MomoText.Text = message;
            SpeechBubble.Visibility = Visibility.Visible;

            await Task.Delay(3000);

            SpeechBubble.Visibility = Visibility.Collapsed;
        }

        // 🎤 Voz
        private void InitVoiceRecognition()
        {
            if (!config.AllowMicrophone)
                return;

            try
            {
                recognizer?.Dispose();

                recognizer = new SpeechRecognitionEngine();
                recognizer.SetInputToDefaultAudioDevice();

                GrammarBuilder gb = new GrammarBuilder();
                gb.Append(WakeWord);
                gb.Append(new Choices("abre", "hola", "hey"));

                recognizer.LoadGrammar(new Grammar(gb));
                recognizer.SpeechRecognized += Recognizer_SpeechRecognized;
                recognizer.RecognizeAsync(RecognizeMode.Multiple);

                isListening = true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    "No se pudo iniciar el reconocimiento de voz: " + ex.Message
                );
            }
        }

        private async void Recognizer_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            string text = e.Result.Text.ToLower();
            if (!IsWakeWordDetected(text)) return;

            text = text.Substring(WakeWord.Length).Trim();
            string response = await GetResponseAsync(text, fromVoice: true);

            if (!string.IsNullOrWhiteSpace(response))
                await ShowMessage(response);
        }

        // 🔔 NotifyIcon
        private void InitNotifyIcon()
        {
            trayIcon = new NotifyIcon
            {
                Icon = new System.Drawing.Icon("Assets/momo.ico"),
                Text = "Momo",
                Visible = true
            };

            var menu = new ContextMenuStrip();

            menu.Items.Add("⚙ Configuración", null, (_, __) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Config_Click(this, new RoutedEventArgs());
                });
            });

            menu.Items.Add("👀 Mostrar / Ocultar", null, (_, __) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (IsVisible) Hide();
                    else Show();
                });
            });

            menu.Items.Add(new ToolStripSeparator());

            menu.Items.Add("❌ Cerrar Momo", null, (_, __) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Close_Click(this, new RoutedEventArgs());
                });
            });

            trayIcon.ContextMenuStrip = menu;
            trayIcon.DoubleClick += (_, __) =>
            {
                Application.Current.Dispatcher.Invoke(Show);
            };
        }

        // 🪟 Siempre detrás
        private void SendToBack()
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;

                NativeMethods.SetWindowPos(
                    hwnd,
                    NativeMethods.HWND_BOTTOM,
                    0, 0, 0, 0,
                    NativeMethods.SWP_NOMOVE |
                    NativeMethods.SWP_NOSIZE |
                    NativeMethods.SWP_NOACTIVATE
                );
            }
            catch { }
        }

        // ❌ Cerrar
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            trayIcon?.Dispose();

            if (recognizer != null)
            {
                recognizer.RecognizeAsyncStop();
                recognizer.Dispose();
            }

            Application.Current.Shutdown();
        }

        // ⚙️ Configuración
        private void Config_Click(object sender, RoutedEventArgs e)
        {
            ConfigWindow window = new ConfigWindow();
            window.ShowDialog();

            LoadConfig();

            if (config.AllowMicrophone)
                InitVoiceRecognition();
        }
    }

    // 🔧 Native interop
    internal static class NativeMethods
    {
        public static readonly IntPtr HWND_BOTTOM = new IntPtr(1);

        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOACTIVATE = 0x0010;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int X,
            int Y,
            int cx,
            int cy,
            uint uFlags);
    }
}
