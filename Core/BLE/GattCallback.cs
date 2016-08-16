using System;
using Android.Bluetooth;
using Android.OS;

namespace Motion.Mobile.Core.BLE
{
	public class GattCallback : BluetoothGattCallback
	{

		public event EventHandler<DeviceConnectionEventArgs> DeviceConnected = delegate {};
		public event EventHandler<DeviceConnectionEventArgs> DeviceDisconnected = delegate {};
		public event EventHandler<ServicesDiscoveredEventArgs> ServicesDiscovered = delegate {};
		public event EventHandler<CharacteristicReadEventArgs> CharacteristicValueUpdated = delegate {};
		public event EventHandler<CharacteristicReadEventArgs> DescriptorWrite = delegate {};

		protected Adapter _adapter;

		public GattCallback (Adapter adapter)
		{
			this._adapter = adapter;
		}

		public override void OnConnectionStateChange (BluetoothGatt gatt, GattStatus status, ProfileState newState)
		{
			Console.WriteLine ("OnConnectionStateChange: ");
			base.OnConnectionStateChange (gatt, status, newState);

			//TODO: need to pull the cached RSSI in here, or read it (requires the callback)
			Device device = new Device (gatt.Device, gatt, this, 0);

			switch (newState) {
			// disconnected
			case ProfileState.Disconnected:
				Console.WriteLine ("disconnected");
					if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
				{
					Console.WriteLine("Changing connection priority to balanced");
					gatt.RequestConnectionPriority(BluetoothGatt.ConnectionPriorityBalanced);
				}
				this.DeviceDisconnected (this, new DeviceConnectionEventArgs () { Device = device });
				break;
				// connecting
			case ProfileState.Connecting:
				Console.WriteLine ("Connecting");
				break;
				// connected
			case ProfileState.Connected:
				Console.WriteLine ("Connected");
					if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
				{
					Console.WriteLine("Changing connection priority to high");
					gatt.RequestConnectionPriority(BluetoothGatt.ConnectionPriorityHigh);
				}
				this.DeviceConnected (this, new DeviceConnectionEventArgs () { Device = device });
				break;
				// disconnecting
			case ProfileState.Disconnecting:
				Console.WriteLine ("Disconnecting");
				break;
			}
		}

		public override void OnServicesDiscovered (BluetoothGatt gatt, GattStatus status)
		{
			base.OnServicesDiscovered (gatt, status);

			Console.WriteLine ("GattCallBack: OnServicesDiscovered: " + status.ToString ());

			this.ServicesDiscovered (this, new ServicesDiscoveredEventArgs ());
		}

		public override void OnDescriptorRead (BluetoothGatt gatt, BluetoothGattDescriptor descriptor, GattStatus status)
		{
			base.OnDescriptorRead (gatt, descriptor, status);

			Console.WriteLine ("GattCallBack: OnDescriptorRead: " + descriptor.ToString());

		}

		public override void OnDescriptorWrite(BluetoothGatt gatt, BluetoothGattDescriptor descriptor, GattStatus status)
		{
			base.OnDescriptorWrite(gatt, descriptor, status);

			Console.WriteLine("GattCallBack: OnDescriptorWrite: " + descriptor.ToString());
			this.DescriptorWrite(this, new CharacteristicReadEventArgs() 
			{ 
				Characteristic = new Characteristic(descriptor.Characteristic, gatt, this) 
			});
		}

		public override void OnCharacteristicRead (BluetoothGatt gatt, BluetoothGattCharacteristic characteristic, GattStatus status)
		{
			base.OnCharacteristicRead (gatt, characteristic, status);

			Console.WriteLine ("GattCallBack: OnCharacteristicRead: " + characteristic.GetStringValue (0));

			this.CharacteristicValueUpdated (this, new CharacteristicReadEventArgs () { 
				Characteristic = new Characteristic (characteristic, gatt, this) }
			);
		}

		public override void OnCharacteristicWrite(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic, GattStatus status)
		{
			base.OnCharacteristicWrite(gatt, characteristic, status);

			Console.WriteLine("GattCallBack: OnCharacteristicWrite: " + characteristic.GetStringValue(0));
		}

		public override void OnCharacteristicChanged (BluetoothGatt gatt, BluetoothGattCharacteristic characteristic)
		{
			base.OnCharacteristicChanged (gatt, characteristic);

			Console.WriteLine ("GattCallBack: OnCharacteristicChanged: ");

			this.CharacteristicValueUpdated (this, new CharacteristicReadEventArgs () { 
				Characteristic = new Characteristic (characteristic, gatt, this) }
			);
		}

	}
}

