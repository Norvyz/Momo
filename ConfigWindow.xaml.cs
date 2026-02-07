using Microsoft.Win32;
using Momo.Models;
using Momo.Services;
using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Momo
{
    public partial class ConfigWindow : Window
    {
        private MomoConfig config;
        private AppPack? selectedPack;

        private List<MMDevice> microphones = new();
        private bool isLoadingMicrophones = false;

        public ConfigWindow()
        {
            InitializeComponent();
            LoadConfig();
            LoadMicrophones();
        }

        // =========================
        // 🔥 AUTO START CON WINDOWS
        // =========================
        private void SetAutoStart(bool enable)
        {
            string appName = "Momo";
            string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName;

            using RegistryKey rk = Registry.CurrentUser.OpenSubKey(
                "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true
            );

            if (enable)
                rk.SetValue(appName, $"\"{exePath}\"");
            else
                rk.DeleteValue(appName, false);
        }

        private void AutoStartCheck_Checked(object sender, RoutedEventArgs e)
        {
            SetAutoStart(true);
            config.AutoStart = true;
        }

        private void AutoStartCheck_Unchecked(object sender, RoutedEventArgs e)
        {
            SetAutoStart(false);
            config.AutoStart = false;
        }

   
        // =========================
        // ⚙️ CONFIGURACIÓN GENERAL
        // =========================
        private void LoadConfig()
        {
            config = ConfigService.Load();

            NameBox.Text = config.Name;
            AutoStartCheck.IsChecked = config.AutoStart;

            // 🎤 Voz
            VoiceEnabledCheckBox.IsChecked = config.AllowMicrophone;
            MicrophoneComboBox.IsEnabled = config.AllowMicrophone;

            // 🌙 Tema
            ThemeToggle.IsChecked = config.IsDarkMode;
            ApplyTheme(config.IsDarkMode);

            AppsList.ItemsSource = config.Apps;
            PacksList.ItemsSource = config.Packs;

            LoadPreview();
        }

        // =========================
        // 🌗 TEMA (DARK / LIGHT)
        // =========================
        private void ThemeToggle_Checked(object sender, RoutedEventArgs e)
        {
            ApplyTheme(true);
        }

        private void ThemeToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            ApplyTheme(false);
        }

        private void ApplyTheme(bool isDark)
        {
            if (isDark)
                ApplyDarkTheme();
            else
                ApplyLightTheme();
        }

        private void ApplyDarkTheme()
        {
            Resources["WindowBackground"] = BrushFrom("#181818");
            Resources["PanelBackground"] = BrushFrom("#1F1F1F");
            Resources["BorderColor"] = BrushFrom("#2D2D2D");

            Resources["TextForeground"] = BrushFrom("#EAEAEA");
            Resources["TextSecondary"] = BrushFrom("#A0A0A0");

            Resources["ButtonBackground"] = BrushFrom("#252525");
            Resources["ButtonHover"] = BrushFrom("#2F2F2F");
        }


        private void ApplyLightTheme()
        {
            Resources["WindowBackground"] = BrushFrom("#FAFAFA");
            Resources["PanelBackground"] = BrushFrom("#FFFFFF");
            Resources["BorderColor"] = BrushFrom("#E6E6E6");

            Resources["TextForeground"] = BrushFrom("#1F1F1F");
            Resources["TextSecondary"] = BrushFrom("#6F6F6F");

            Resources["ButtonBackground"] = BrushFrom("#F2F2F2");
            Resources["ButtonHover"] = BrushFrom("#E8E8E8");
        }

        private SolidColorBrush BrushFrom(string hex)
        {
            return new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(hex));
        }

        // =========================
        // 🎤 MICRÓFONOS
        // =========================
        private void LoadMicrophones()
        {
            if (MicrophoneComboBox == null) return;

            isLoadingMicrophones = true;

            MicrophoneComboBox.Items.Clear();
            microphones.Clear();

            var enumerator = new MMDeviceEnumerator();
            microphones = enumerator
                .EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                .ToList();

            foreach (var mic in microphones)
                MicrophoneComboBox.Items.Add(mic.FriendlyName);

            if (!string.IsNullOrWhiteSpace(config.SelectedMicrophoneId))
            {
                int index = microphones.FindIndex(m => m.ID == config.SelectedMicrophoneId);
                if (index >= 0)
                    MicrophoneComboBox.SelectedIndex = index;
            }

            isLoadingMicrophones = false;
        }

        private void VoiceEnabledCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (isLoadingMicrophones) return;

            MicrophoneComboBox.IsEnabled = true;
            config.AllowMicrophone = true;
        }

        private void VoiceEnabledCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (isLoadingMicrophones) return;

            MicrophoneComboBox.IsEnabled = false;
            config.AllowMicrophone = false;
            config.SelectedMicrophoneId = null;
            config.SelectedMicrophoneName = null;
        }

        private void MicrophoneComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isLoadingMicrophones || MicrophoneComboBox.SelectedIndex < 0) return;

            var mic = microphones[MicrophoneComboBox.SelectedIndex];
            config.SelectedMicrophoneId = mic.ID;
            config.SelectedMicrophoneName = mic.FriendlyName;
        }

        // =========================
        // 🎨 SKIN
        // =========================
        private void LoadPreview()
        {
            string path = (!string.IsNullOrWhiteSpace(config.SkinPath) && File.Exists(config.SkinPath))
                ? config.SkinPath
                : "Assets/momo.png";

            PreviewImage.Source = new BitmapImage(
                new Uri(path, UriKind.RelativeOrAbsolute));
        }

        private void ChangeSkin_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new()
            {
                Filter = "Imágenes (*.png;*.jpg)|*.png;*.jpg"
            };

            if (dialog.ShowDialog() == true)
            {
                config.SkinPath = dialog.FileName;
                LoadPreview();
            }
        }

// =========================
// 🚀 APLICACIONES
// =========================
private void AddApp_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new()
            {
                Filter = "Aplicaciones (*.exe)|*.exe"
            };

            if (dialog.ShowDialog() != true) return;

            string keyword = Microsoft.VisualBasic.Interaction.InputBox(
                "Escribe la palabra clave para abrir esta app:",
                "Palabra clave");

            if (string.IsNullOrWhiteSpace(keyword)) return;
            keyword = keyword.Trim().ToLower();

            if (config.Apps.Any(a => a.Keyword == keyword))
            {
                MessageBox.Show("Esa palabra clave ya existe.");
                return;
            }

            config.Apps.Add(new AppCommand
            {
                Keyword = keyword,
                ExecutablePath = dialog.FileName
            });

            AppsList.Items.Refresh();
        }

        private void EditApp_Click(object sender, RoutedEventArgs e)
        {
            if (AppsList.SelectedItem is not AppCommand app) return;

            string keyword = Microsoft.VisualBasic.Interaction.InputBox(
                "Editar palabra clave:",
                "Editar",
                app.Keyword);

            if (!string.IsNullOrWhiteSpace(keyword))
                app.Keyword = keyword.Trim().ToLower();

            AppsList.Items.Refresh();
        }

        private void RemoveApp_Click(object sender, RoutedEventArgs e)
        {
            if (AppsList.SelectedItem is AppCommand app)
            {
                config.Apps.Remove(app);
                AppsList.Items.Refresh();
            }
        }

        private void AppsList_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        // =========================
        // 📦 GRUPOS / PACKS
        // =========================
        private void AddPack_Click(object sender, RoutedEventArgs e)
        {
            string name = Microsoft.VisualBasic.Interaction.InputBox(
                "Nombre del grupo:",
                "Nuevo grupo");

            if (string.IsNullOrWhiteSpace(name)) return;

            string keyword = Microsoft.VisualBasic.Interaction.InputBox(
                "Palabra clave para activar el grupo:",
                "Keyword del grupo");

            if (string.IsNullOrWhiteSpace(keyword)) return;

            string delayInput = Microsoft.VisualBasic.Interaction.InputBox(
                "Delay entre aplicaciones (segundos):",
                "Delay",
                "3");

            if (!int.TryParse(delayInput, out int delay)) delay = 3;
            keyword = keyword.Trim().ToLower();

            if (config.Packs.Any(p => p.Keyword == keyword))
            {
                MessageBox.Show("Esa palabra clave de grupo ya existe.");
                return;
            }

            config.Packs.Add(new AppPack
            {
                Name = name.Trim(),
                Keyword = keyword,
                DelaySeconds = delay
            });

            PacksList.Items.Refresh();
        }

        private void EditPack_Click(object sender, RoutedEventArgs e)
        {
            if (PacksList.SelectedItem is not AppPack pack) return;

            string name = Microsoft.VisualBasic.Interaction.InputBox(
                "Editar nombre del grupo:",
                "Editar grupo",
                pack.Name);

            if (!string.IsNullOrWhiteSpace(name))
                pack.Name = name.Trim();

            string delayInput = Microsoft.VisualBasic.Interaction.InputBox(
                "Editar delay (segundos):",
                "Delay",
                pack.DelaySeconds.ToString());

            if (int.TryParse(delayInput, out int delay))
                pack.DelaySeconds = delay;

            PacksList.Items.Refresh();
        }

        private void RemovePack_Click(object sender, RoutedEventArgs e)
        {
            if (PacksList.SelectedItem is AppPack pack)
            {
                config.Packs.Remove(pack);
                PackAppsList.ItemsSource = null;
                PacksList.Items.Refresh();
            }
        }

        private void PacksList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            selectedPack = PacksList.SelectedItem as AppPack;

            PackAppsList.ItemsSource = selectedPack?.Apps;
            PackAppsList.Items.Refresh();
        }

        // =========================
        // 📦 APPS DENTRO DEL PACK
        // =========================
        private void AddPackApp_Click(object sender, RoutedEventArgs e)
        {
            if (selectedPack == null)
            {
                MessageBox.Show("Selecciona un grupo primero 🦎");
                return;
            }

            OpenFileDialog dialog = new()
            {
                Filter = "Aplicaciones (*.exe)|*.exe"
            };

            if (dialog.ShowDialog() != true) return;

            string exePath = dialog.FileName;

            if (config.Packs.Any(p =>
                p.Apps.Any(a =>
                    a.ExecutablePath.Equals(exePath, StringComparison.OrdinalIgnoreCase))))
            {
                MessageBox.Show("Esta aplicación ya está asignada a otro grupo.");
                return;
            }

            string keyword = Microsoft.VisualBasic.Interaction.InputBox(
                "Palabra clave para esta aplicación dentro del grupo:",
                "Keyword del grupo");

            if (string.IsNullOrWhiteSpace(keyword)) return;

            keyword = keyword.Trim().ToLower();

            if (selectedPack.Apps.Any(a => a.Keyword == keyword))
            {
                MessageBox.Show("Ese keyword ya existe dentro del grupo.");
                return;
            }

            selectedPack.Apps.Add(new AppCommand
            {
                Keyword = keyword,
                ExecutablePath = exePath
            });

            PackAppsList.Items.Refresh();
            PacksList.Items.Refresh();
        }

        private void RemovePackApp_Click(object sender, RoutedEventArgs e)
        {
            if (selectedPack == null) return;

            if (PackAppsList.SelectedItem is AppCommand app)
            {
                selectedPack.Apps.Remove(app);
                PackAppsList.Items.Refresh();
                PacksList.Items.Refresh();
            }
        }

        
            // =====================================
            // 🔗 LINKS DE COMUNIDAD
            // =====================================
            private void GitHubLink_Click(object sender, MouseButtonEventArgs e)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/Norvyz",
                    UseShellExecute = true
                });
            }

            private void DiscordLink_Click(object sender, MouseButtonEventArgs e)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://discord.gg/tuServidorDiscord",
                    UseShellExecute = true
                });
            }

     
    // =========================
    // 💾 GUARDAR / CANCELAR
    // =========================
    private void Save_Click(object sender, RoutedEventArgs e)
        {
            config.Name = string.IsNullOrWhiteSpace(NameBox.Text)
                ? "Momo"
                : NameBox.Text.Trim();

            config.AutoStart = AutoStartCheck.IsChecked == true;
            config.AllowMicrophone = VoiceEnabledCheckBox.IsChecked == true;
            config.IsDarkMode = ThemeToggle.IsChecked == true;

            // Aplicar auto start automáticamente también al guardar
            SetAutoStart(config.AutoStart);

            ConfigService.Save(config);
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        // =========================
        // 🎬 ANIMACIÓN FADE-IN
        // =========================
        private void TabControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is TabControl tabControl)
            {
                tabControl.BeginAnimation(OpacityProperty,
                    new System.Windows.Media.Animation.DoubleAnimation
                    {
                        From = 0,
                        To = 1,
                        Duration = TimeSpan.FromMilliseconds(250)
                    });
            }
        }

    }
}
