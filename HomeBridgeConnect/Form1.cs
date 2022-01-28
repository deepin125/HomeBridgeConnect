using Newtonsoft.Json;
using System;
using System.Drawing;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HomeBridgeConnect
{
    public partial class Form1 : Form
    {
        private System.Windows.Forms.NotifyIcon notifyIcon1;
        private System.Windows.Forms.ContextMenu contextMenu1;
        private System.Windows.Forms.MenuItem menuItem1;

        [DllImport("Powrprof.dll", SetLastError = true)]
        static extern uint PowerRegisterSuspendResumeNotification(uint flags, ref DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS receipient, ref IntPtr registrationHandle);
        [DllImport("Powrprof.dll", SetLastError = true)]
        static extern uint PowerUnregisterSuspendResumeNotification(ref IntPtr registrationHandle);


        private const int WM_POWERBROADCAST = 536; // (0x218)
        private const int PBT_APMPOWERSTATUSCHANGE = 10; // (0xA) - Power status has changed.
        private const int PBT_APMRESUMEAUTOMATIC = 18; // (0x12) - Operation is resuming automatically from a low-power state.This message is sent every time the system resumes.
        private const int PBT_APMRESUMESUSPEND = 7; // (0x7) - Operation is resuming from a low-power state.This message is sent after PBT_APMRESUMEAUTOMATIC if the resume is triggered by user input, such as pressing a key.
        private const int PBT_APMSUSPEND = 4; // (0x4) - System is suspending operation.
        private const int PBT_POWERSETTINGCHANGE = 32787; // (0x8013) - A power setting change event has been received.
        private const int DEVICE_NOTIFY_CALLBACK = 2;

        delegate int DeviceNotifyCallbackRoutine(IntPtr context, int type, IntPtr setting);
        private static DeviceNotifyCallbackRoutine callbackDelegate;
        private static readonly HttpClient client = new HttpClient();
        public static IntPtr registrationHandle = new IntPtr();

        [StructLayout(LayoutKind.Sequential)]
        struct DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS
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

        [STAThread]
        static void Run()
        {
            Application.Run(new Form1());
        }

        public Form1()
        {
            InitializeComponent();
            this.components = new System.ComponentModel.Container();
            this.contextMenu1 = new System.Windows.Forms.ContextMenu();
            this.menuItem1 = new System.Windows.Forms.MenuItem();

            // Initialize contextMenu1
            this.contextMenu1.MenuItems.AddRange(
                        new System.Windows.Forms.MenuItem[] { this.menuItem1 });

            // Initialize menuItem1
            this.menuItem1.Index = 0;
            this.menuItem1.Text = "E&xit";
            this.menuItem1.Click += new System.EventHandler(this.menuItem1_Click);

            // Set up how the form should be displayed.
            //this.ClientSize = new System.Drawing.Size(600, 266);
            this.Text = "Waiting for ACPI events...";

            // Create the NotifyIcon.
            this.notifyIcon1 = new System.Windows.Forms.NotifyIcon(this.components);

            // The Icon property sets the icon that will appear
            // in the systray for this application.
            notifyIcon1.Icon = new Icon("homebridge.ico");

            // The ContextMenu property sets the menu that will
            // appear when the systray icon is right clicked.
            notifyIcon1.ContextMenu = this.contextMenu1;

            // The Text property sets the text that will be displayed,
            // in a tooltip, when the mouse hovers over the systray icon.
            notifyIcon1.Text = "Waiting for ACPI events...";
            notifyIcon1.Visible = true;

            this.Hide();
            // Handle the DoubleClick event to activate the form.
            notifyIcon1.DoubleClick += new System.EventHandler(this.notifyIcon1_DoubleClick);

            this.checkBox1.Checked = Properties.Settings.Default.Wake_up_send;
            this.checkBox2.Checked = Properties.Settings.Default.Sleep_send;
            this.textBox1.Text = Properties.Settings.Default.HomeBridgeIPAddress;


            DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS recipient = new DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS();

            if (callbackDelegate != null)
            {
                recipient.Callback = callbackDelegate;
            }
            else
            {
                recipient.Callback = new DeviceNotifyCallbackRoutine(DeviceNotifyCallback);
            }
            if (callbackDelegate == null)
            {
                callbackDelegate = recipient.Callback;
            }

            recipient.Context = IntPtr.Zero;

            IntPtr pRecipient = Marshal.AllocHGlobal(Marshal.SizeOf(recipient));
            Marshal.StructureToPtr(recipient, pRecipient, false);

            uint result = PowerRegisterSuspendResumeNotification(DEVICE_NOTIFY_CALLBACK, ref recipient, ref registrationHandle);

            if (result != 0)
                Console.WriteLine("Error registering for power notifications: " + Marshal.GetLastWin32Error());
            else
                Console.WriteLine("Successfully Registered for power notifications!");

        }

        private static async Task<int> post_http_request(bool computerState)
        {
            int rc = 0;
            string homebridgeIP = Properties.Settings.Default.HomeBridgeIPAddress;
            Properties.Settings.Default.HomeBridgeIPAddress = homebridgeIP;

            string url;

            Json_packet json_pk = new Json_packet
            {
                characteristic = "On",
                value = computerState,
                notificationID = "my-switch",
                password = "superSecretPassword",
                accessory = "HTTP-SWITCH",
                service = "switch-service"
            };

            string json_serialized = JsonConvert.SerializeObject(json_pk, Formatting.Indented);

            if (computerState)
            {
                url = "http://" + homebridgeIP + ":8080/my-switch";
                var response = await client.PostAsync(url, new StringContent(json_serialized, Encoding.UTF8, "application/json"));
            }
            else
            {
                url = "http://" + homebridgeIP + ":8080/my-switch";
                var response = await client.PostAsync(url, new StringContent(json_serialized, Encoding.UTF8, "application/json"));
            }
            return rc;
        }

        private static int DeviceNotifyCallback(IntPtr context, int type, IntPtr setting)
        {
            Console.WriteLine("Device notify callback called: ");
            Task<int> rc;
            bool sendWake = Properties.Settings.Default.Wake_up_send;
            bool sendSleep = Properties.Settings.Default.Sleep_send;

            switch (type)
            {
                case PBT_APMPOWERSTATUSCHANGE:
                    Console.WriteLine("\tPower status has changed.");
                    break;

                case PBT_APMRESUMEAUTOMATIC:
                    Console.WriteLine("\tOperation is resuming automatically from a low-power state.This message is sent every time the system resumes.");
                    if (sendWake)
                    {
                        rc = post_http_request(true);
                    }
                    break;

                case PBT_APMRESUMESUSPEND:
                    Console.WriteLine("\tOperation is resuming from a low-power state.This message is sent after PBT_APMRESUMEAUTOMATIC if the resume is triggered by user input, such as pressing a key.");
                    if (sendSleep)
                    {
                        rc = post_http_request(true);

                    }

                    break;
                case PBT_APMSUSPEND:
                    Console.WriteLine("\tSystem is suspending operation.");
                    if (sendSleep)
                    {
                        rc = post_http_request(false);
                    }

                    break;
                case PBT_POWERSETTINGCHANGE:
                    Console.WriteLine("\tA power setting change event has been received. ");
                    break;
                default:
                    Console.WriteLine("unknown");
                    break;
            }

            // do something here
            return 0;
        }

        private void notifyIcon1_DoubleClick(object Sender, EventArgs e)
        {
            // Show the form when the user double clicks on the notify icon
            // Set the WindowState to normal if the form is minimized.

            if (this.WindowState == FormWindowState.Minimized)
                this.WindowState = FormWindowState.Normal;
            this.Show();

            // Activate the form.
            this.Activate();
        }

        private void menuItem1_Click(object Sender, EventArgs e)
        {
            // Close the form, which closes the application.
            PowerUnregisterSuspendResumeNotification(ref registrationHandle);
            this.Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Task<int> rc = null;
            Properties.Settings.Default.Wake_up_send = this.checkBox1.Checked;
            Properties.Settings.Default.Sleep_send = this.checkBox2.Checked;
            Properties.Settings.Default.HomeBridgeIPAddress = this.textBox1.Text;
            Properties.Settings.Default.Save();

            rc = post_http_request(true);


            //this.Hide();
        }
    }
}
