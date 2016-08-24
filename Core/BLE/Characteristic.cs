using System;
using System.Collections.Generic;
using System.Text;
using Android.Bluetooth;
using System.Linq;
using Java.Util;
using Android.Media;
using System.Threading.Tasks;

namespace Motion.Mobile.Core.BLE
{
	public class Characteristic : ICharacteristic
	{
		public event EventHandler<CharacteristicReadEventArgs> ValueUpdated = delegate {};
		public event EventHandler<CharacteristicReadEventArgs> NotificationStateValueUpdated = delegate { };

		protected BluetoothGattCharacteristic _nativeCharacteristic;
		/// <summary>
		/// we have to keep a reference to this because Android's api is weird and requires
		/// the GattServer in order to do nearly anything, including enumerating services
		/// </summary>
		protected BluetoothGatt _gatt;
		/// <summary>
		/// we also track this because of gogole's weird API. the gatt callback is where
		/// we'll get notified when services are enumerated
		/// </summary>
		protected GattCallback _gattCallback;


		public Characteristic (BluetoothGattCharacteristic nativeCharacteristic, BluetoothGatt gatt, GattCallback gattCallback)
		{
			this._nativeCharacteristic = nativeCharacteristic;
			this._gatt = gatt;
			this._gattCallback = gattCallback;

			//if (this._gattCallback != null) {
			//	// wire up the characteristic value updating on the gattcallback
			//	this._gattCallback.CharacteristicValueUpdated += (object sender, CharacteristicReadEventArgs e) => {
			//		// it may be other characteristics, so we need to test
			//		Console.WriteLine("Value updated for Characteristic ID-: "+ e.Characteristic.ID);
			//		//if(e.Characteristic.ID == this.ID) {
			//			// update our underlying characteristic (this one will have a value)
			//			//TODO: is this necessary? probably the underlying reference is the same.
			//			//this._nativeCharacteristic = e.Characteristic;

			//			this.ValueUpdated (this, e);
			//		//}
			//	};
			//}
		}

		public string Uuid {
			get { return this._nativeCharacteristic.Uuid.ToString (); }
		}

		public Guid ID {
			get { return Guid.Parse( this._nativeCharacteristic.Uuid.ToString() ); }
		}

		public byte[] Value {
			get { return this._nativeCharacteristic.GetValue (); }
		}

		public string StringValue 
		{
			get 
			{
				if (this.Value == null)
					return String.Empty;
				else
					return System.Text.Encoding.UTF8.GetString (this.Value);
			}
		}

		public int ValueUpdatedSubscribers
		{
			get { return this.ValueUpdated.GetInvocationList().Length; }
		}

		public string Name {
			get { return KnownCharacteristics.Lookup (this.ID).Name; }
		}

		public CharacteristicPropertyType Properties {
			get {
				return (CharacteristicPropertyType)(int)this._nativeCharacteristic.Properties;
			}
		}

		public IList<IDescriptor> Descriptors {
			get {
				// if we haven't converted them to our xplat objects
				if (this._descriptors == null) {
					this._descriptors = new List<IDescriptor> ();
					// convert the internal list of them to the xplat ones
					foreach (var item in this._nativeCharacteristic.Descriptors) {
						this._descriptors.Add (new Descriptor (item));
					}
				}
				return this._descriptors;
			}
		} protected IList<IDescriptor> _descriptors;

		public object NativeCharacteristic {
			get {
				return this._nativeCharacteristic;
			}
		}

		public bool CanRead {get{return (this.Properties & CharacteristicPropertyType.Read) != 0; }}
		public bool CanUpdate {get{return (this.Properties & CharacteristicPropertyType.Notify) != 0; }}
		//NOTE: why this requires Apple, we have no idea. BLE stands for Mystery.
		public bool CanWrite {get{return (this.Properties & CharacteristicPropertyType.WriteWithoutResponse | CharacteristicPropertyType.AppleWriteWithoutResponse) != 0; }}


		// HACK: UNTESTED - this API has only been tested on iOS
		public Task<ICharacteristic> ReadAsync()
		{
			var tcs = new TaskCompletionSource<ICharacteristic>();

			if (!CanRead) {
				throw new InvalidOperationException ("Characteristic does not support READ");
			}

			//this._gattCallback.CharacteristicValueUpdated += (object sender, CharacteristicReadEventArgs e) => {
			//	Console.WriteLine("Characteristic Value Received. ");
			//	this.ValueUpdated (this, e);
			//};

			EventHandler<CharacteristicReadEventArgs> updated = null;
			updated = (object sender, CharacteristicReadEventArgs e) => {
				// it may be other characteristics, so we need to test
				var c = e.Characteristic;
				tcs.SetResult(c);
				if (this._gattCallback != null) {
					// wire up the characteristic value updating on the gattcallback
					this._gattCallback.CharacteristicValueUpdated -= updated;
				}
			};


			if (this._gattCallback != null) {
				// wire up the characteristic value updating on the gattcallback
				this._gattCallback.CharacteristicValueUpdated += updated;
			}

			Console.WriteLine(".....ReadAsync");
			this._gatt.ReadCharacteristic (this._nativeCharacteristic);

			//Task.Delay(500);

			return tcs.Task;
		}

		public void StartUpdates()
		{
			BluetoothGattDescriptor descriptor = _nativeCharacteristic.GetDescriptor(UUID.FromString("00002902-0000-1000-8000-00805f9b34fb"));
			if (descriptor == null)
			{
				descriptor = _nativeCharacteristic.GetDescriptor(UUID.FromString("00002901-0000-1000-8000-00805f9b34fb"));
			}
			//BluetoothGattDescriptor descriptor = _nativeCharacteristic.GetDescriptor(UUID.FromString("00002901-0000-1000-8000-00805f9b34fb"));

			foreach (BluetoothGattDescriptor desc in _nativeCharacteristic.Descriptors)
			{
				Console.WriteLine("Descriptor UUID: " + desc.Uuid);
			}

			this._gattCallback.DescriptorWrite += (object sender, CharacteristicReadEventArgs e) =>
			{
				Console.WriteLine("Descriptor Write Received. ");
				this.NotificationStateValueUpdated(this, e);
			};

			//this._gattCallback.CharacteristicValueUpdated += (object sender, CharacteristicReadEventArgs e) =>
			//{
			//	Console.WriteLine("Notification/Indication Response Recevied.");
			//	this.ValueUpdated(this, e);
			//};

			Console.WriteLine("Properties: " + (int) _nativeCharacteristic.Properties);

			if ((_nativeCharacteristic.Properties & GattProperty.Indicate) == GattProperty.Indicate)
			{
				Console.WriteLine("Enabling indication");

				descriptor.SetValue(BluetoothGattDescriptor.EnableIndicationValue.ToArray());
			}

			if ((_nativeCharacteristic.Properties & GattProperty.Notify) == GattProperty.Notify)
			{
				Console.WriteLine("Enabling notification");

				_gatt.SetCharacteristicNotification(_nativeCharacteristic, true);

				descriptor.SetValue(BluetoothGattDescriptor.EnableNotificationValue.ToArray());
			}

			if ((_nativeCharacteristic.Properties & GattProperty.WriteNoResponse) == GattProperty.WriteNoResponse)
			{
				Console.WriteLine("Enabling notification/write no response");

				_gatt.SetCharacteristicNotification(_nativeCharacteristic, true);

				descriptor.SetValue(BluetoothGattDescriptor.EnableNotificationValue.ToArray());
			}
			_gatt.WriteDescriptor(descriptor);

		}

		// HACK: UNTESTED - this API has only been tested on iOS
		public void Write(byte[] data)
		{
			if (!CanWrite)
			{
				throw new InvalidOperationException("Characteristic does not support WRITE");
			}

			Console.WriteLine("Writing to characteristic: " + _nativeCharacteristic.Uuid);
			_nativeCharacteristic.SetValue(data);
			this._gatt.WriteCharacteristic(this._nativeCharacteristic);
		}

		void UpdatedNotificationState(object sender, CharacteristicReadEventArgs e)
		{
			Console.WriteLine("Notification state event " + e.Characteristic.ID);
		}
		/*public void StartUpdates ()
		{
			// TODO: should be bool RequestValue? compare iOS API for commonality
			bool successful = false;
			if (CanRead) {
				Console.WriteLine ("Characteristic.RequestValue, PropertyType = Read, requesting updates");
				successful = this._gatt.ReadCharacteristic (this._nativeCharacteristic);
			}
			if (CanUpdate) {
				Console.WriteLine ("Characteristic.RequestValue, PropertyType = Notify, requesting updates");
				
				successful = this._gatt.SetCharacteristicNotification (this._nativeCharacteristic, true);

				// [TO20131211@1634] It seems that setting the notification above isn't enough. You have to set the NOTIFY
				// descriptor as well, otherwise the receiver will never get the updates. I just grabbed the first (and only)
				// descriptor that is associated with the characteristic, which is the NOTIFY descriptor. This seems like a really
				// odd way to do things to me, but I'm a Bluetooth newbie. Google has a example here (but ono real explaination as
				// to what is going on):
				// http://developer.android.com/guide/topics/connectivity/bluetooth-le.html#notification
				//
				// HACK: further detail, in the Forms client this only seems to work with a breakpoint on it
				// (ie. it probably needs to wait until the above 'SetCharacteristicNofication' is done before doing this...?????? [CD]
				System.Threading.Thread.Sleep(200); // HACK: did i mention this was a hack?????????? [CD] 50ms was too short, 100ms seems to work

				Console.WriteLine ("Descriptor count: " + _nativeCharacteristic.Descriptors.Count);
				if (_nativeCharacteristic.Descriptors.Count > 0) {
					BluetoothGattDescriptor descriptor = _nativeCharacteristic.Descriptors [0];
					descriptor.SetValue (BluetoothGattDescriptor.EnableNotificationValue.ToArray ());
					_gatt.WriteDescriptor (descriptor);
				} else {
					Console.WriteLine ("RequestValue, FAILED: _nativeCharacteristic.Descriptors was empty, not sure why");
				}
			}

			Console.WriteLine ("RequestValue, Succesful: " + successful.ToString());
		}*/




		public void StopUpdates ()
		{
			bool successful = false;
			if (CanUpdate) {
				successful = this._gatt.SetCharacteristicNotification (this._nativeCharacteristic, false);
				//TODO: determine whether 
				Console.WriteLine ("Characteristic.RequestValue, PropertyType = Notify, STOP updates");
			}
		}
	}
}

