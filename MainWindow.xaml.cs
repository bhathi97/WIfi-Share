using QRCoder;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Security.Principal;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace WIfi_Share
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            if (!IsRunningAsAdmin())
            {
                RestartAsAdmin();
                return;
            }

            
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            PopulateWifiDropdown(cbWifiName);
        }

        private async void btnGetPassword_Click(object sender, RoutedEventArgs e)
        {
            string ssid = cbWifiName.SelectedValue?.ToString()?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(ssid))
            {
                txtStatus.Text = "Please select a Network Name (SSID)";
                return;
            }

            try
            {
                btnGetPassword.IsEnabled = false;
                txtStatus.Text = "Retrieving password...";

                string password = await Task.Run(() => GetWifiPassword(ssid));

                if (string.IsNullOrEmpty(password))
                {
                    txtStatus.Text = "WiFi network not found or password couldn't be retrieved";
                    btnGetPassword.IsEnabled = true;
                    return;
                }

                lblPassword.Text = "Password: " + password;
                txtStatus.Text = "Generating QR code...";

                await Task.Run(() =>
                {
                    try
                    {
                        string escapedSsid = EscapeSpecialCharacters(ssid);
                        string escapedPassword = EscapeSpecialCharacters(password);

                        string wifiString = $"WIFI:T:WPA;S:{escapedSsid};P:{escapedPassword};;";

                        Bitmap qrCodeImage = GenerateQrCode(wifiString);

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            imgQr.Source = BitmapToImageSource(qrCodeImage);
                            txtStatus.Text = "Scan the QR code to connect to this network";
                        });
                    }
                    catch (Exception ex)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            txtStatus.Text = $"Error generating QR code: {ex.Message}";
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Error: {ex.Message}";
            }
            finally
            {
                btnGetPassword.IsEnabled = true;
            }
        }

        private bool IsRunningAsAdmin()
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private void RestartAsAdmin()
        {
            try
            {
                ProcessStartInfo startInfo = new()
                {
                    FileName = Environment.ProcessPath,
                    UseShellExecute = true,
                    Verb = "runas"
                };

                Process.Start(startInfo);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"This application requires administrator privileges.\n\nError: {ex.Message}",
                    "Administrator Privileges Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                Application.Current.Shutdown();
            }
        }

        private string GetWifiPassword(string ssid)
        {
            try
            {
                using Process process = new();
                process.StartInfo.FileName = "netsh";
                process.StartInfo.Arguments = $"wlan show profile name=\"{ssid}\" key=clear";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.CreateNoWindow = true;
                process.Start();

                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string line in lines)
                {
                    if (line.Contains("Key Content"))
                    {
                        string[] parts = line.Split(':');
                        if (parts.Length > 1)
                        {
                            return parts[1].Trim();
                        }
                    }
                }

                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private string EscapeSpecialCharacters(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            return input
                .Replace("\\", "\\\\")
                .Replace(";", "\\;")
                .Replace(",", "\\,")
                .Replace(":", "\\:")
                .Replace("\"", "\\\"");
        }

        private Bitmap GenerateQrCode(string content)
        {
            using QRCodeGenerator qrGenerator = new();
            using QRCodeData qrCodeData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
            using QRCode qrCode = new(qrCodeData);

            return qrCode.GetGraphic(20, Color.Black, Color.White, true);
        }

        private BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using MemoryStream memory = new();
            bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
            memory.Position = 0;

            BitmapImage bitmapImage = new();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = memory;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();
            bitmapImage.Freeze();

            return bitmapImage;
        }

        private void cbWifiName_DropDownOpened(object sender, EventArgs e)
        {
            PopulateWifiDropdown(cbWifiName);
        }


        private List<string> GetAllWifiProfiles()
        {
            List<string> wifiProfiles = new List<string>();

            try
            {
                using Process process = new Process();
                process.StartInfo.FileName = "netsh";
                process.StartInfo.Arguments = "wlan show profiles";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.CreateNoWindow = true;
                process.Start();

                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string line in lines)
                {
                    if (line.Contains("All User Profile") || line.Contains("Current User Profile"))
                    {
                        string[] parts = line.Split(':');
                        if (parts.Length > 1)
                        {
                            string profileName = parts[1].Trim();
                            if (!string.IsNullOrEmpty(profileName))
                            {
                                wifiProfiles.Add(profileName);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving WiFi profiles: {ex.Message}");
            }

            return wifiProfiles;
        }


        private void PopulateWifiDropdown(ComboBox comboBox)
        {
            try
            {
                List<string> profiles = GetAllWifiProfiles();
                comboBox.Items.Clear();

                foreach (string profile in profiles)
                {
                    comboBox.Items.Add(profile);
                }

                // Optional: Select first item if available
                if (comboBox.Items.Count > 0)
                {
                    comboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error populating dropdown: {ex.Message}");
            }
        }
    }
}