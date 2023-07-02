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
