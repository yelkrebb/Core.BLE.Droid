using System;
using System.Collections.Generic;
using Android.Bluetooth;
using System.Threading.Tasks;
using Android.App;
using System.Linq;
using Android.Content;
using Android.OS;
using Android.Bluetooth.LE;
using Java.Util;


namespace Motion.Mobile.Core.BLE
{
	/// <summary>
	/// TODO: this really should be a singleton.
	/// </summary>
	/// 

	[BroadcastReceiver(Enabled = true)]
	[IntentFilter(new string[]{ BluetoothAdapter.ActionStateChanged, BluetoothAdapter.ActionConnectionStateChanged})]
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
		protected LollipopScanCallback _lollipopCallback;

		//for android version 5.0 and above
		private BluetoothLeScanner _bleScanner;
		private ScanSettings _scanSettings;

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
			Console.WriteLine("Initializing Adapter");
			var appContext = Application.Context;
			// get a reference to the bluetooth system service
			this._manager = (BluetoothManager) appContext.GetSystemService("bluetooth");
			this._adapter = this._manager.Adapter;

			if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
			{
				this.setLollipopProperty();
			}

			this._gattCallback = new GattCallback (this);

			this._gattCallback.DeviceConnected += (object sender, DeviceConnectionEventArgs e) => {
				Console.WriteLine("Device Connected: "+ e.Device.Name);

				this._connectedDevices.Add ( e.Device);
				this.DeviceConnected (this, e);
			};

			this._gattCallback.DeviceDisconnected += (object sender, DeviceConnectionEventArgs e) => {
				Console.WriteLine("Device Disconnected: " + e.Device.Name);
				// TODO: remove the disconnected device from the _connectedDevices list
				// i don't think this will actually work, because i'm created a new underlying device here.
				if(this._connectedDevices.Contains(e.Device))
				{
					this._connectedDevices.Remove(e.Device);
				}
				this.DeviceDisconnected (this, e);
			};

			this._gattCallback.CharacteristicValueUpdated += Handle_ValueUpdated;

		}

		private void setLollipopProperty()
		{
			Console.WriteLine("Setting up lollipop property");
			this._bleScanner = this._adapter.BluetoothLeScanner;
			this._lollipopCallback = new LollipopScanCallback();
			this._lollipopCallback.deviceFoundEvent += OnLeScan;

			//ScanMode is declared in Util object due to namespace conflict of ScanMode object in both Android.Bluetooth and Android.Bluetooth.LE
			this._scanSettings = new ScanSettings.Builder().SetScanMode(Util.ScanModeLowLatency).Build();

		}

		public void onReceive(Context context, Intent intent) {
			int prevState = intent.GetIntExtra (BluetoothAdapter.ExtraPreviousState, -1); 
			int newState = intent.GetIntExtra(BluetoothAdapter.ExtraState, -1);

			Console.WriteLine ("Bluetooth state change from {0} to {1}", (State)prevState, (State)newState);
		}

		//TODO: scan for specific service type eg. HeartRateMonitor
		public async void StartScanningForDevices (List<Guid> serviceUuid)
		{
			if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
			{

				if (serviceUuid != null && serviceUuid.Count > 0)
				{
					List<ScanFilter> filterList = new List<ScanFilter>();
					foreach (Guid guid in serviceUuid)
					{
						UUID uuid = UUID.FromString(guid.ToString());
						ScanFilter filter = new ScanFilter.Builder().SetServiceUuid(new ParcelUuid(uuid)).Build();
						filterList.Add(filter);
					}
					this.StartScanningForDevices(filterList);
				}
			}
			else {
				Console.WriteLine("Adapter: Starting scan for API 20 and below.");
				StartScanningForDevices();
			}
		}

		public async void StartScanningForDevices(List<ScanFilter> filters)
		{
			Console.WriteLine("Start BLE Scanning for 5.0 or up");
			this._discoveredDevices = new List<IDevice>();
			this._discoveredDevices.Clear();

			this._isScanning = true;
			this._bleScanner.StartScan(filters, _scanSettings, this._lollipopCallback);

			// in 10 seconds, stop the scan
			await Task.Delay(10000);

			// if it is still scanning
			if (this._isScanning)
			{
				Console.WriteLine("BluetoothLEManager 5.0: Scan timeout has elapses.");
				this.StopScanningForDevices();
			}
		}

		public async void StartScanningForDevices ()
		{
			// clear out the list
			this._discoveredDevices = new List<IDevice> ();
			this._discoveredDevices.Clear ();
			// start scanning
			this._isScanning = true;
			this._adapter.StartLeScan(this);

			// in 10 seconds, stop the scan
			await Task.Delay (10000);

			// if we're still scanning
			if (this._isScanning) {
				Console.WriteLine ("BluetoothLEManager: Scan timeout has elapsed.");
				//this._adapter.StopLeScan (this);
				this.StopScanningForDevices();
			}
		}

		public void StopScanningForDevices ()
		{
			Console.WriteLine("Adapter: Stopping the scan for devices.");
			this._isScanning = false;

			if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
			{
				this._bleScanner.StopScan(this._lollipopCallback);
			}
			else {
				this._adapter.StopLeScan(this);
			}

			this.ScanCompleted(this, new EventArgs());
		}

		public void OnLeScan (BluetoothDevice bleDevice, int rssi, byte[] scanRecord)
		{
			Console.WriteLine ("Adapter.LeScanCallback: " + bleDevice.Name + " : " + bleDevice.Address);
			// TODO: for some reason, this doesn't work, even though they have the same pointer,
			// it thinks that the item doesn't exist. so i had to write my own implementation
//			if(!this._discoveredDevices.Contains(device) ) {
//				this._discoveredDevices.Add (device );
//			}
			Device device = new Device (bleDevice, null, null, rssi);

			if (!DeviceExistsInDiscoveredList (bleDevice)) {
				this._discoveredDevices.Add	(device);
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
			//handle.ValueUpdated += Handle_ValueUpdated;

			if (handle.CanWrite)
			{
				Console.WriteLine("Adapter: Sending Command");
				handle.Write(command);
			}
			else {
				Console.WriteLine("Unable to write to handle");
			}
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

