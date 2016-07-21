using System;
using System.Collections.Generic;
using Android.Bluetooth;
using System.Threading.Tasks;
using Android.App;
using System.Linq;
using Android.Content;
using Android.OS;

namespace Motion.Mobile.Core.BLE
{
	/// <summary>
	/// TODO: this really should be a singleton.
	/// </summary>
	/// 

	[BroadcastReceiver(Enabled = true)]
	[IntentFilter(new string[]{ BluetoothAdapter.ActionStateChanged,BluetoothAdapter.ActionConnectionStateChanged})]
	public class Adapter : Java.Lang.Object, BluetoothAdapter.ILeScanCallback, IAdapter
	{
		// events
		public event EventHandler<DeviceDiscoveredEventArgs> DeviceDiscovered = delegate {};
		public event EventHandler<DeviceConnectionEventArgs> DeviceConnected = delegate {};
		public event EventHandler<DeviceConnectionEventArgs> DeviceDisconnected = delegate {};
		public event EventHandler<DeviceConnectionEventArgs> DeviceFailedToConnect = delegate {};
		public event EventHandler<CommandResponseEventArgs> CommandResponse = delegate {};
		public event EventHandler ScanCompleted = delegate {};
		public event EventHandler<BluetoothStateEventArgs> BluetoothStateUpdated = delegate {};

		// class members
		protected BluetoothManager _manager;
		protected BluetoothAdapter _adapter;
		protected GattCallback _gattCallback;

		public bool IsConnected {
			get { return GetConnectionState();}
		} protected bool _isConnected;

		public bool IsScanning {
			get { return this._isScanning; }
		} protected bool _isScanning;

		public IList<IDevice> DiscoveredDevices {
			get {
				return this._discoveredDevices;
			}
		} protected IList<IDevice> _discoveredDevices = new List<IDevice> ();

		public IList<IDevice> ConnectedDevices {
			get {
				return this._connectedDevices;
			}
		} protected IList<IDevice> _connectedDevices = new List<IDevice>();


		public Adapter ()
		{
			var appContext = Application.Context;
			// get a reference to the bluetooth system service
			this._manager = (BluetoothManager) appContext.GetSystemService("bluetooth");
			this._adapter = this._manager.Adapter;

			this._gattCallback = new GattCallback (this);



			this._gattCallback.DeviceConnected += (object sender, DeviceConnectionEventArgs e) => {
				Console.WriteLine("Device Connected: "+ e.Device.Name);

				this._connectedDevices.Add ( e.Device);
				this.DeviceConnected (this, e);
			};

			this._gattCallback.DeviceDisconnected += (object sender, DeviceConnectionEventArgs e) => {
				// TODO: remove the disconnected device from the _connectedDevices list
				// i don't think this will actually work, because i'm created a new underlying device here.
				if(this._connectedDevices.Contains(e.Device))
				{
					this._connectedDevices.Remove(e.Device);
				}
				this.DeviceDisconnected (this, e);
			};
		}

		public void onReceive(Context context, Intent intent) {
			int prevState = intent.GetIntExtra (BluetoothAdapter.ExtraPreviousState, -1); 
			int newState = intent.GetIntExtra(BluetoothAdapter.ExtraState, -1);

			Console.WriteLine ("Bluetooth state change from {0} to {1}", (State)prevState, (State)newState);
		}

		//TODO: scan for specific service type eg. HeartRateMonitor
		public async void StartScanningForDevices (Guid serviceUuid)
		{
			StartScanningForDevices ();
//			throw new NotImplementedException ("Not implemented on Android yet, look at _adapter.StartLeScan() overload");
		}

		public async void StartScanningForDevices ()
		{
			Console.WriteLine ("Adapter: Starting a scan for devices.");

			// clear out the list
			this._discoveredDevices = new List<IDevice> ();
			this._discoveredDevices.Clear ();
			// start scanning
			this._isScanning = true;
			this._adapter.StartLeScan (this);

			// in 10 seconds, stop the scan
			await Task.Delay (10000);

			// if we're still scanning
			if (this._isScanning) {
				Console.WriteLine ("BluetoothLEManager: Scan timeout has elapsed.");
				this._adapter.StopLeScan (this);
				this.ScanCompleted (this, new EventArgs ());
			}
		}

		public void StopScanningForDevices ()
		{
			Console.WriteLine ("Adapter: Stopping the scan for devices.");
			this._isScanning = false;	
			this._adapter.StopLeScan (this);
		}

		public void OnLeScan (BluetoothDevice bleDevice, int rssi, byte[] scanRecord)
		{
			Console.WriteLine ("Adapter.LeScanCallback: " + bleDevice.Name);
			// TODO: for some reason, this doesn't work, even though they have the same pointer,
			// it thinks that the item doesn't exist. so i had to write my own implementation
//			if(!this._discoveredDevices.Contains(device) ) {
//				this._discoveredDevices.Add (device );
//			}
			Device device = new Device (bleDevice, null, null, rssi);


			if (!DeviceExistsInDiscoveredList (bleDevice)) {
				this._discoveredDevices.Add	(device);

				var parsedRecords = scanRecord;

				try {

					parsedRecords = scanRecord.Skip (34).Take (7).ToArray();

				} catch (Exception) {

				}
			}
			// TODO: in the cross platform API, cache the RSSI
			// TODO: shouldn't i only raise this if it's not already in the list?
			this.DeviceDiscovered (this, new DeviceDiscoveredEventArgs { Device = device, RSSI = rssi });
		}

		protected bool DeviceExistsInDiscoveredList(BluetoothDevice device)
		{
			foreach (var d in this._discoveredDevices) {
				// TODO: verify that address is unique
				if (device.Address == ((BluetoothDevice)d.NativeDevice).Address) {
					//Console.WriteLine ("Device already exist.");
					return true;
				}
			}
			return false;
		}

		public void ConnectToDevice (IDevice device)
		{
			// returns the BluetoothGatt, which is the API for BLE stuff
			// TERRIBLE API design on the part of google here.

			if (!ConnectedDevices.Contains(device)) {
				Console.WriteLine ("Connect to device: " + device.Name);
				((BluetoothDevice)device.NativeDevice).ConnectGatt (Application.Context, true, this._gattCallback);
			}
		}

		public void DisconnectDevice (IDevice device)
		{
			//isConnecting = false;
			((Device) device).Disconnect();
		}

		public void SendCommand (ICharacteristic handle, byte[] command)
		{
			handle.ValueUpdated += Handle_ValueUpdated;

			if (handle.CanWrite)
				handle.Write (command);
		}

		void Handle_ValueUpdated (object sender, CharacteristicReadEventArgs e)
		{
			CommandResponse (this, new CommandResponseEventArgs () { Data = e.Characteristic.Value });
		}

		bool GetConnectionState()
		{
			bool isConnected = false;
			if (this._manager.GetConnectionState ((BluetoothDevice)this.ConnectedDevices [0].NativeDevice, ProfileType.Gatt) == ProfileState.Connected) {
				isConnected = true;
			}
			return isConnected;
		}

	}
}

