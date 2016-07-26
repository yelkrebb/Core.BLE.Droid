using System;
using Android.Bluetooth;
using Android.Bluetooth.LE;

namespace Motion.Mobile.Core.BLE
{
	public class LollipopScanCallback : ScanCallback
	{

		public delegate void DeviceFoundEventArgs(BluetoothDevice bleDevice, int rssi, byte[] scanRecord);
		public event DeviceFoundEventArgs deviceFoundEvent;

		public override void OnScanResult(ScanCallbackType callbackType, ScanResult result)
		{
			String name = (result.Device.Name == null ? "null" : result.Device.Name);

			Console.WriteLine("Lollipop: Device Discovered: " + name);

			if (this.deviceFoundEvent != null)
			{
				this.deviceFoundEvent(result.Device, result.Rssi, null);
			}
		}

		public override void OnScanFailed(ScanFailure errorCode)
		{
			Console.WriteLine("Stop Scan Failed: " + errorCode.ToString());
		}
	}

}

