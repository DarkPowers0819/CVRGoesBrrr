# CVRGoesBrrr
Toy manager for CVR
## Adult Toy API
this is a utility mod to facilitate other mods using your toys. It can use either Intiface Central or an embeded Intiface CLI tool. Before launching the embeded Intiface CLI tool the mod will check to see if an instance of Intiface Central is running and try to use that instead. 
### Configuration
- UseEmbeddedCLI: (bool) determins if the mod should be able to launch the embedded Intiface CLI tool.
- IntifaceServerURI: (string) the URI of the Intiface Central app, usually "WS://localhost"
- SecondsBetweenConnectionAttempts: (int) how often to attempt to connect to Intiface central or the Intiface CLI tool.
- DeviceCommandTimeInterval: (int) the minimum number of miliseconds between commands to your toy.
- Debug: (bool) enables debug logging which my be useful when issues are encountered.
- IntifaceServerPort: (int) the TCP/IP port of the intiface server, usually 12345.
- UseLovenseConnect: (bool) should the embeded Intiface CLI use Lovense Connect or not.
- UseBluetoothLE: (bool) should the embeded Intiface CLI use any Bluetooth LE service or not.
- UseSerial: (bool) should the embeded Intiface CLI serial port devices or not.
- UseHID: (bool) should the embeded Intiface CLI HID devices or not.
- UseLovenseDongle: (bool) should the embeded Intiface CLI use Lovense Dongle or not.
- UseXinput: (bool) should the embeded Intiface CLI use Xinput or not.
- UseDeviceWebsocketServer: (bool) should the embeded Intiface CLI use a websocket server or not.
- DeviceWebsocketServerPort: (int) TCP/IP port number of the websocket server
- RestartIntiface: (bool) when set to true this will cause the mod to shut down & restart the embeded intiface CLI tool, this will NOT restart Intiface Central.
## CVR Gooes Brrr
this is a mod to let you use your toys based on actions in CVR.\
### Configuration
- Active: (bool) not yet implemented
- Touch Vibrations: (bool) enables the use of the Orthographic Cameras to sense avatars touching
- Thrust Vibrations: (bool) enables the use of DPS objects
- JustUseMyToys: (bool) when enabled CVR Goes Brrr will ignore the "type" (giver/taker) of devices and will bind them to any sensor
- Audio Vibrations: (bool) not yet implemented
- Touch Feedback: (bool) when enabled this will cause your controllers to vibrate when in proximity to a DPS or touch zone
- Idle Vibrations: (bool) enable the Idle Vibrations
- Idle Vibration Intensity %: (float) sets the Idle Vibration level
- Min Vibration Intensity %: (float) not yet implemented)
- Max Vibration Intensity %: (float) sets the maximum vibration level
- Expression Parameters Enabled: (bool) not yet implemented
- Setup Mode: (bool) drives some legacy functions such as allowing you to trigger your own touch zones for testing.
- Debug: (bool) enables debug logging which my be useful when issues are encountered.
- Debug Performance: (bool) enables logging of performance indicators. Mostly for development and testing.
- Update Frequency: (float) the frequency of how often to update vibrations (unit for this is Hz)
- Intensity Curve Exponent: (float) an exponent that can be applied to the virtual 'sensors' to increase the vibration intensity.
- XSOverlay Notifications: (bool) not yet implemented
- Scan Duration: (float) needs to be removed
- Scan Wait Duration: (float) needs to be removed
## Recomendations
I Highly recomend using an Intel Bluetooth 5.0 or better adapter. The Lovense dongle, while functional, has limitations. If you have a lovense dongle and want to use it then make use to set the update fequency to something low like 15 or lower. Additionaly you will want to set the DeviceCommandTimeInterval to something like 50-100 milliseconds. Using more aggressive settings may cause the dongle to timeout and lose connection.
