# HomeBridgeConnect

This program is designed to run in the background of Windows and listen for ACPI sleep/wake events using a callback on the power state in Windows. 
When this occurs a POST packet is sent to the homebridge appliance, and will forward the event to the homebridge http switch.This allows you to monitor 
if the computer is sleeping or awake, and thus make cool automations. For example, when I wake my computer from sleep, it will turn on my computer, 
notify my thermostat to heat the basement, etc. It could be used to monitor if your child is on the computer, or any other number of things where knowing 
if your computer is on is useful.

Future development into waking/sleeping the computer using a listening event within this program might be coming.  I also could extend the setup to control 
other accessories in homekit using my windows computer, however I haven't thought of a use case that I wouldn't just use Siri or my phone to do those things.

*Compile/Install:*
Compile code in VS, run application.  The application will appear in the notification tray.  Double click the Homebridge Icon to get the setup window. 
Make sure to setup HomeBridge with the prerequisites.

*HomeBridge Prerequisites:*

* homebridge-http-switch is a plugin with which you can configure
HomeKit switches which forward any requests to a defined http server. This comes in handy when you already have home
automated equipment which can be controlled via http requests. Or you have built your own equipment, for example some sort
of lightning controlled with an wifi enabled Arduino board which than can be integrated via this plugin into Homebridge.
* homebridge-http-switch supports three different type of switches. A normal stateful switch and two variants of
stateless switches (stateless and stateless-reverse) which differ in their original position. For stateless switches
you can specify multiple urls to be targeted when the switch is turned On/Off.

* homebridge-http-notification-server is needed in order to receive
updates when the state changes at your external program. For details on how to implement those updates and how to
install and configure homebridge-http-notification-server, please refer to the
README of the repository first.

Essentially with these two programs, you create a http listening server on the homebridge, and then a program for controlling switches as accessories.


Checkout this website for configuration details, I used the following:

        {
            "accessory": "HTTP-SWITCH",
            "name": "Zephyrus",
            "notificationID": "my-switch",
            "notificationPassword": "password",
            "switchType": "toggle",
            "onUrl": "http://localhost/api/switchOn",
            "offUrl": "http://localhost/api/switchOff"
        }

I found that using the toggle switchType was important, as we have no way to get status using a stateful switch.

You also need to create and configure a file in /var/lib/homebridge/notification-server.json.  It can look like this:
        {
            "hostname": "0.0.0.0"
            "port": 8080
        }
There are additional options and features like ssl that can be added. The homebridge-http-server is pretty simple to setup, make sure to restart the 
homebridge after installation and configuration.

*HomeBridge Connect Application setup:*

* You need to select whether your homebridge-http-notification-server uses ssl (https:// vs http://) and then add the IP or hostname. 
* Add the notificationID, as this is what the homebridge-http-switch program is registering with the notification server to listen for.  You can call it 
whatever is needed, as long as the notificationID in the homebridge config matches what is setup in the windows application. 
* Setting up the password needs to match once again what is setup in the http-switch on the homebridge.
* "Send Message on Wake" should be checked if you want to send messages when computer wakes.
* "Send Message on Sleep" should be checked if you want to send messages when computer sleeps.
* "Setup Auto-Start" will ensure the program is started and running the background on reboots.
