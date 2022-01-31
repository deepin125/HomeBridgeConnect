using System;
using System.ComponentModel;
using System.Drawing;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HomeBridgeConnect.Properties;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace HomeBridgeConnect
{
    public partial class Form1 : Form
    {
        private const int WM_POWERBROADCAST = 536; // (0x218)
        private const int PBT_APMPOWERSTATUSCHANGE = 10; // (0xA) - Power status has changed.

        private const int
            PBT_APMRESUMEAUTOMATIC =
                18; // (0x12) - Operation is resuming automatically from a low-power state.This message is sent every time the system resumes.

        private const int
            PBT_APMRESUMESUSPEND =
                7; // (0x7) - Operation is resuming from a low-power state.This message is sent after PBT_APMRESUMEAUTOMATIC if the resume is triggered by user input, such as pressing a key.

        private const int PBT_APMSUSPEND = 4; // (0x4) - System is suspending operation.
        private const int PBT_POWERSETTINGCHANGE = 32787; // (0x8013) - A power setting change event has been received.
        private const int DEVICE_NOTIFY_CALLBACK = 2;
        private static DeviceNotifyCallbackRoutine callbackDelegate;
        private static readonly HttpClient client = new HttpClient();
        public static IntPtr registrationHandle;
        private static Task<HttpResponseMessage> _hResultTask;

        private bool TestSetup = false;
        private bool allowshowdisplay;
        private readonly ContextMenu contextMenu1;
        private readonly MenuItem menuItem1;
        private readonly NotifyIcon notifyIcon1;

        public Form1()
        {
            InitializeComponent();
            Icon = Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);
            components = new Container();
            contextMenu1 = new ContextMenu();
            menuItem1 = new MenuItem();

            // Initialize contextMenu1
            contextMenu1.MenuItems.AddRange(
                new[] {menuItem1});

            // Initialize menuItem1
            menuItem1.Index = 0;
            menuItem1.Text = "E&xit";
            menuItem1.Click += menuItem1_Click;

            // Set up how the form should be displayed.
            Text = "HomeBridge Connect";

            // Create the NotifyIcon.
            notifyIcon1 = new NotifyIcon(components);

            // The Icon property sets the icon that will appear
            // in the systray for this application.
            notifyIcon1.Icon = new Icon("homebridge.ico");

            // The ContextMenu property sets the menu that will
            // appear when the systray icon is right clicked.
            notifyIcon1.ContextMenu = contextMenu1;

            // The Text property sets the text that will be displayed,
            // in a tooltip, when the mouse hovers over the systray icon.
            notifyIcon1.Text = "Waiting for ACPI events...";
            notifyIcon1.Visible = true;

            comboBox1.DropDownStyle = ComboBoxStyle.DropDownList;

            Hide();
            // Handle the DoubleClick event to activate the form.
            notifyIcon1.DoubleClick += notifyIcon1_DoubleClick;

            //Setting up tooltips for help during runtime
            this.toolTip1.SetToolTip(comboBox1, "Setup configuration for normal http communication or using ssl (https://).");
            this.toolTip2.SetToolTip(textBox1, "IP address or hostname of the HomeBridge server.");
            this.toolTip3.SetToolTip(textBox2, "This is the user defined notificationID that is configured in the https-switch configuration. Must match exactly, example 'my-switch' sans special characters.");
            this.toolTip4.SetToolTip(textBox3, "Password being used for the homebridge-http-switch configuration file.");
            this.toolTip5.SetToolTip(checkBox1, "This will ensure that a POST packet is sent on wake. This will turn the switch to the on position.");
            this.toolTip6.SetToolTip(checkBox2, "This will ensure that a POST packet is sent on sleep. This will turn the switch to the off position.");
            this.toolTip7.SetToolTip(checkBox3, "Configure this program to startup automatically on reboot.");

            checkBox1.Checked = Settings.Default.Wake_up_send;
            checkBox2.Checked = Settings.Default.Sleep_send;
            textBox1.Text = Settings.Default.HomeBridgeIPAddress;
            textBox2.Text = Settings.Default.notificationID_value;
            checkBox3.Checked = Settings.Default.Auto_Start;
            comboBox1.Text = Settings.Default.http_or_s;
            textBox3.Text = Settings.Default.password;

            setup_power_handle();
        }

        [DllImport("Powrprof.dll", SetLastError = true)]
        private static extern uint PowerRegisterSuspendResumeNotification(uint flags,
            ref DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS receipient, ref IntPtr registrationHandle);

        [DllImport("Powrprof.dll", SetLastError = true)]
        private static extern uint PowerUnregisterSuspendResumeNotification(ref IntPtr registrationHandle);

        [STAThread]
        private static void Run()
        {
            Application.Run(new Form1());
        }

        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(allowshowdisplay ? value : allowshowdisplay);
        }


        private static void setup_power_handle()
        {
            var recipient = new DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS();

            if (callbackDelegate != null)
                recipient.Callback = callbackDelegate;
            else
                recipient.Callback = DeviceNotifyCallback;
            if (callbackDelegate == null) callbackDelegate = recipient.Callback;

            recipient.Context = IntPtr.Zero;

            var pRecipient = Marshal.AllocHGlobal(Marshal.SizeOf(recipient));
            Marshal.StructureToPtr(recipient, pRecipient, false);

            var result =
                PowerRegisterSuspendResumeNotification(DEVICE_NOTIFY_CALLBACK, ref recipient, ref registrationHandle);

            if (result != 0)
                Console.WriteLine("Error registering for power notifications: " + Marshal.GetLastWin32Error());
            else
                Console.WriteLine("Successfully Registered for power notifications!");
            Marshal.FreeHGlobal(pRecipient);
        }

        private static async Task<HttpResponseMessage> post_http_request(bool computerState)
        {
            HttpResponseMessage HResultTask = new HttpResponseMessage();
            var homebridgeIP = Settings.Default.HomeBridgeIPAddress;

            string url;

            var json_pk = new Json_packet
            {
                characteristic = "On",
                value = computerState,
                notificationID = Settings.Default.notificationID_value,
                password = Settings.Default.password,
                accessory = "HTTP-SWITCH",
                service = "switch-service"
            };

            var json_serialized = JsonConvert.SerializeObject(json_pk, Formatting.Indented);

            url = Settings.Default.http_or_s + homebridgeIP + ":8080/" + json_pk.notificationID;
            HResultTask = await client.PostAsync(url, new StringContent(json_serialized, Encoding.UTF8, "application/json"));

            return HResultTask;
        }

        private static int DeviceNotifyCallback(IntPtr context, int type, IntPtr setting)
        {
            Console.WriteLine("Device notify callback called: ");
            var sendWake = Settings.Default.Wake_up_send;
            var sendSleep = Settings.Default.Sleep_send;

            switch (type)
            {
                case PBT_APMPOWERSTATUSCHANGE:
                    Console.WriteLine("\tPower status has changed.");
                    break;

                case PBT_APMRESUMEAUTOMATIC:
                    Console.WriteLine(
                        "\tOperation is resuming automatically from a low-power state.This message is sent every time the system resumes.");
                    if (sendWake) _hResultTask = post_http_request(true);
                    break;
                case PBT_APMRESUMESUSPEND:
                    Console.WriteLine(
                        "\tOperation is resuming from a low-power state.This message is sent after PBT_APMRESUMEAUTOMATIC if the resume is triggered by user input, such as pressing a key.");
                    //Don't need to send two wakes
                    break;
                case PBT_APMSUSPEND:
                    Console.WriteLine("\tSystem is suspending operation.");
                    if (sendSleep) post_http_request(false).Wait();
                    break;
                case PBT_POWERSETTINGCHANGE:
                    Console.WriteLine("\tA power setting change event has been received. ");
                    break;
                default:
                    Console.WriteLine("unknown");
                    break;
            }

            return 0;
        }

        private void notifyIcon1_DoubleClick(object Sender, EventArgs e)
        {
            // Show the form when the user double clicks on the notify icon
            // Set the WindowState to normal if the form is minimized.

            if (WindowState == FormWindowState.Minimized)
                WindowState = FormWindowState.Normal;
            allowshowdisplay = true;
            Show();

            // Activate the form.
            Activate();
        }

        private void menuItem1_Click(object Sender, EventArgs e)
        {
            // Close the form, which closes the application.
            PowerUnregisterSuspendResumeNotification(ref registrationHandle);
            Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Settings.Default.Wake_up_send = checkBox1.Checked;
            Settings.Default.Sleep_send = checkBox2.Checked;
            Settings.Default.HomeBridgeIPAddress = textBox1.Text;
            Settings.Default.Auto_Start = checkBox3.Checked;
            Settings.Default.notificationID_value = textBox2.Text;
            Settings.Default.http_or_s = comboBox1.Text;
            Settings.Default.password = textBox3.Text;

            if (TestSetup)
            {
                Task.Run(() =>
                {
                    _hResultTask = post_http_request(true);
                    while (!_hResultTask.IsCompleted)
                    {
                        MessageBox.Show(string.Format("{0}", _hResultTask.Status.ToString()));
                    }

                    MessageBox.Show(String.Format("{0}", _hResultTask.Result.ToString()));
                });
                return;
            }

            auto_start_setup();
            Settings.Default.Save();
            _hResultTask = post_http_request(true);
            Hide();
        }

        private void auto_start_setup()
        {
            var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

            if (key == null) return;

            if (checkBox3.Checked)
                key.SetValue("HomeBridgeConnect.exe", Application.ExecutablePath);
            else
                key.DeleteValue("HomeBridgeConnect.exe", false);
        }

        private delegate int DeviceNotifyCallbackRoutine(IntPtr context, int type, IntPtr setting);

        [StructLayout(LayoutKind.Sequential)]
        private struct DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS
        {
            public DeviceNotifyCallbackRoutine Callback;
            public IntPtr Context;
        }

        private class Json_packet
        {
            public string characteristic { get; set; }
            public bool value { get; set; }
            public string notificationID { get; set; }
            public string password { get; set; }
            public string accessory { get; set; }
            public string service { get; set; }
        }

        private void testSetupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (TestSetup == false)
            {
                TestSetup = true;
                this.Text = "HomeBridge Connect Test Mode";
                this.button1.Text = "Test";
            }
            else
            {
                TestSetup = false;
                this.Text = "HomeBridge Connect";
                this.button1.Text = "Save Settings";
            }
        }
    }
}